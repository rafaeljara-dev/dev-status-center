using DevStatusCenter.Application.Abstractions;
using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Application.Providers;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;
using DevStatusCenter.Domain.ValueObjects;
using Microsoft.Data.Sqlite;

namespace DevStatusCenter.Infrastructure.Persistence;

public sealed class SqliteLocalStore(
    SqliteConnectionFactory connectionFactory,
    SqliteMigrationRunner migrationRunner) : ILocalStore
{

    public Task InitializeAsync(CancellationToken cancellationToken) =>
        migrationRunner.RunAsync(cancellationToken);

    public async Task ApplyProviderRefreshAsync(
        ProviderRefreshResult result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        await connectionFactory.EnterWriteAsync(cancellationToken);
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();

            foreach (var account in result.Accounts)
            {
                await UpsertAccountAsync(connection, transaction, account, result.CompletedAt, cancellationToken);
            }

            foreach (var observation in result.Observations)
            {
                await UpsertServiceAsync(
                    connection,
                    transaction,
                    observation.Service,
                    result.CompletedAt,
                    cancellationToken);

                foreach (var usage in observation.Usage)
                {
                    await InsertUsageAsync(connection, transaction, usage, cancellationToken);
                }

                foreach (var billing in observation.Billing)
                {
                    await InsertBillingAsync(connection, transaction, billing, cancellationToken);
                }
            }

            foreach (var subscription in result.Subscriptions)
            {
                await UpsertSubscriptionAsync(
                    connection,
                    transaction,
                    subscription,
                    result.CompletedAt,
                    overwrite: true,
                    cancellationToken);
            }

            foreach (var payment in result.Payments)
            {
                await UpsertPaymentAsync(
                    connection,
                    transaction,
                    payment,
                    result.CompletedAt,
                    overwrite: true,
                    cancellationToken);
            }

            await using (var history = connection.CreateCommand())
            {
                history.Transaction = transaction;
                history.CommandText = """
                    INSERT INTO refresh_history(provider_id, completed_at_ms, observation_count)
                    VALUES ($providerId, $completedAt, $count);
                    """;
                history.Parameters.AddWithValue("$providerId", result.ProviderId);
                history.Parameters.AddWithValue("$completedAt", SqliteValue.Instant(result.CompletedAt));
                history.Parameters.AddWithValue("$count", result.Observations.Count);
                await history.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            connectionFactory.ExitWrite();
        }
    }

    public async Task<DashboardCacheData> ReadDashboardDataAsync(
        string displayCurrency,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        var usage = await ReadLatestUsageAsync(connection, cancellationToken);
        var services = await ReadLatestServiceCostsAsync(
            connection,
            displayCurrency,
            usage,
            cancellationToken);
        var budgets = await ReadBudgetsAsync(connection, displayCurrency, cancellationToken);
        var subscriptions = await ReadSubscriptionsAsync(connection, displayCurrency, cancellationToken);
        var payments = await ReadUpcomingPaymentsAsync(connection, displayCurrency, now, cancellationToken);
        var quickAccess = await ReadQuickAccessAsync(connection, cancellationToken);
        var states = await ReadProviderStatesAsync(connection, cancellationToken);
        var lastSuccess = states
            .Where(x => x.LastSuccessAt is not null)
            .Select(x => x.LastSuccessAt)
            .Max();

        return new DashboardCacheData(
            displayCurrency,
            services,
            budgets,
            subscriptions,
            payments,
            quickAccess,
            states,
            lastSuccess);
    }

    public async Task<ProviderState?> ReadProviderStateAsync(
        string providerId,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT provider_id, status, last_attempt_at_ms, last_success_at_ms,
                   next_refresh_at_ms, consecutive_failures, error_code, error_message
            FROM provider_states
            WHERE provider_id = $providerId;
            """;
        command.Parameters.AddWithValue("$providerId", providerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapProviderState(reader) : null;
    }

    public async Task WriteProviderStateAsync(
        ProviderState state,
        CancellationToken cancellationToken)
    {
        await connectionFactory.EnterWriteAsync(cancellationToken);
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO provider_states(
                    provider_id, status, last_attempt_at_ms, last_success_at_ms,
                    next_refresh_at_ms, consecutive_failures, error_code, error_message, updated_at_ms)
                VALUES (
                    $providerId, $status, $lastAttempt, $lastSuccess,
                    $nextRefresh, $failures, $errorCode, $errorMessage, $updatedAt)
                ON CONFLICT(provider_id) DO UPDATE SET
                    status = excluded.status,
                    last_attempt_at_ms = excluded.last_attempt_at_ms,
                    last_success_at_ms = excluded.last_success_at_ms,
                    next_refresh_at_ms = excluded.next_refresh_at_ms,
                    consecutive_failures = excluded.consecutive_failures,
                    error_code = excluded.error_code,
                    error_message = excluded.error_message,
                    updated_at_ms = excluded.updated_at_ms;
                """;
            command.Parameters.AddWithValue("$providerId", state.ProviderId);
            command.Parameters.AddWithValue("$status", (int)state.Status);
            command.Parameters.AddWithValue("$lastAttempt", SqliteValue.NullableInstant(state.LastAttemptAt));
            command.Parameters.AddWithValue("$lastSuccess", SqliteValue.NullableInstant(state.LastSuccessAt));
            command.Parameters.AddWithValue("$nextRefresh", SqliteValue.NullableInstant(state.NextRefreshAt));
            command.Parameters.AddWithValue("$failures", state.ConsecutiveFailures);
            command.Parameters.AddWithValue("$errorCode", (object?)state.ErrorCode ?? DBNull.Value);
            command.Parameters.AddWithValue("$errorMessage", (object?)state.ErrorMessage ?? DBNull.Value);
            command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            connectionFactory.ExitWrite();
        }
    }

    public async Task SeedReferenceDataAsync(
        IReadOnlyCollection<Budget> budgets,
        IReadOnlyCollection<Subscription> subscriptions,
        IReadOnlyCollection<Payment> payments,
        CancellationToken cancellationToken)
    {
        await connectionFactory.EnterWriteAsync(cancellationToken);
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();
            var now = DateTimeOffset.UtcNow;

            foreach (var budget in budgets)
            {
                await UpsertBudgetAsync(connection, transaction, budget, now, cancellationToken);
            }

            foreach (var subscription in subscriptions)
            {
                await UpsertSubscriptionAsync(
                    connection,
                    transaction,
                    subscription,
                    now,
                    overwrite: false,
                    cancellationToken);
            }

            foreach (var payment in payments)
            {
                await UpsertPaymentAsync(
                    connection,
                    transaction,
                    payment,
                    now,
                    overwrite: false,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            connectionFactory.ExitWrite();
        }
    }

    public async Task<IReadOnlyList<QuickAccessEntry>> ReadQuickAccessAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        return await ReadQuickAccessAsync(connection, cancellationToken);
    }

    public async Task UpsertQuickAccessAsync(
        QuickAccessEntry entry,
        CancellationToken cancellationToken)
    {
        await connectionFactory.EnterWriteAsync(cancellationToken);
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO quick_access_entries(
                    id, parent_id, display_name, kind, path, default_action,
                    sort_order, is_pinned, updated_at_ms)
                VALUES ($id, $parentId, $name, $kind, $path, $action, $sort, $pinned, $now)
                ON CONFLICT(id) DO UPDATE SET
                    parent_id = excluded.parent_id,
                    display_name = excluded.display_name,
                    kind = excluded.kind,
                    path = excluded.path,
                    default_action = excluded.default_action,
                    sort_order = excluded.sort_order,
                    is_pinned = excluded.is_pinned,
                    updated_at_ms = excluded.updated_at_ms;
                """;
            command.Parameters.AddWithValue("$id", entry.Id);
            command.Parameters.AddWithValue("$parentId", (object?)entry.ParentId ?? DBNull.Value);
            command.Parameters.AddWithValue("$name", entry.DisplayName);
            command.Parameters.AddWithValue("$kind", (int)entry.Kind);
            command.Parameters.AddWithValue("$path", (object?)entry.Path ?? DBNull.Value);
            command.Parameters.AddWithValue("$action", (int)entry.DefaultAction);
            command.Parameters.AddWithValue("$sort", entry.SortOrder);
            command.Parameters.AddWithValue("$pinned", entry.IsPinned ? 1 : 0);
            command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            connectionFactory.ExitWrite();
        }
    }

    public async Task<IReadOnlyList<Alert>> RecordAndFilterAlertsAsync(
        IReadOnlyCollection<Alert> candidates,
        TimeSpan cooldown,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
        {
            return [];
        }

        var cutoff = SqliteValue.Instant(now - cooldown);
        var stamp = SqliteValue.Instant(now);
        var due = new List<Alert>(candidates.Count);

        await connectionFactory.EnterWriteAsync(cancellationToken);
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;

            // Una alerta nueva se inserta y RETURNING la devuelve: hay que notificarla. Una ya
            // conocida sólo se actualiza si está activa y su enfriamiento venció; si el WHERE no
            // se cumple, no vuelve nada y no se notifica. Toda la decisión cabe en una sentencia.
            command.CommandText = """
                INSERT INTO alerts(
                    id, service_id, severity, rule_type, threshold_decimal,
                    is_enabled, last_triggered_at_ms)
                VALUES ($id, $serviceId, $severity, $ruleType, $threshold, 1, $now)
                ON CONFLICT(id) DO UPDATE SET
                    severity = excluded.severity,
                    threshold_decimal = excluded.threshold_decimal,
                    last_triggered_at_ms = excluded.last_triggered_at_ms
                WHERE alerts.is_enabled = 1
                  AND (alerts.last_triggered_at_ms IS NULL OR alerts.last_triggered_at_ms <= $cutoff)
                RETURNING id;
                """;

            var id = command.Parameters.Add("$id", SqliteType.Text);
            var serviceId = command.Parameters.Add("$serviceId", SqliteType.Text);
            var severity = command.Parameters.Add("$severity", SqliteType.Integer);
            var ruleType = command.Parameters.Add("$ruleType", SqliteType.Text);
            var threshold = command.Parameters.Add("$threshold", SqliteType.Text);
            command.Parameters.AddWithValue("$now", stamp);
            command.Parameters.AddWithValue("$cutoff", cutoff);

            foreach (var alert in candidates)
            {
                id.Value = alert.Id;
                serviceId.Value = (object?)alert.ServiceId ?? DBNull.Value;
                severity.Value = (int)alert.Severity;
                ruleType.Value = alert.RuleType;
                threshold.Value = SqliteValue.Decimal(alert.Threshold);

                if (await command.ExecuteScalarAsync(cancellationToken) is not null)
                {
                    due.Add(alert);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            connectionFactory.ExitWrite();
        }

        return due;
    }

    public async Task<int> PruneHistoryAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retention, TimeSpan.Zero);
        var cutoff = SqliteValue.Instant(DateTimeOffset.UtcNow - retention);

        await connectionFactory.EnterWriteAsync(cancellationToken);
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;

            // current_usage / current_billing no se tocan: son la proyeccion vigente y deben
            // sobrevivir aunque el snapshot que las origino ya no exista.
            command.CommandText = """
                DELETE FROM usage_snapshots WHERE captured_at_ms < $cutoff;
                DELETE FROM billing_records WHERE captured_at_ms < $cutoff;
                DELETE FROM refresh_history WHERE completed_at_ms < $cutoff;
                """;
            command.Parameters.AddWithValue("$cutoff", cutoff);
            var removed = await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return removed;
        }
        finally
        {
            connectionFactory.ExitWrite();
        }
    }

    public async Task DeleteQuickAccessAsync(string id, CancellationToken cancellationToken)
    {
        await connectionFactory.EnterWriteAsync(cancellationToken);
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM quick_access_entries WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            connectionFactory.ExitWrite();
        }
    }

    private static async Task UpsertAccountAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ProviderAccount account,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO provider_accounts(
                id, provider_id, display_name, external_account_id,
                credential_reference, is_enabled, updated_at_ms)
            VALUES ($id, $providerId, $name, $externalId, $credential, $enabled, $updatedAt)
            ON CONFLICT(id) DO UPDATE SET
                provider_id = excluded.provider_id,
                display_name = excluded.display_name,
                external_account_id = excluded.external_account_id,
                credential_reference = excluded.credential_reference,
                is_enabled = excluded.is_enabled,
                updated_at_ms = excluded.updated_at_ms;
            """;
        command.Parameters.AddWithValue("$id", account.Id);
        command.Parameters.AddWithValue("$providerId", account.ProviderId);
        command.Parameters.AddWithValue("$name", account.DisplayName);
        command.Parameters.AddWithValue("$externalId", (object?)account.ExternalAccountId ?? DBNull.Value);
        command.Parameters.AddWithValue("$credential", (object?)account.CredentialReference ?? DBNull.Value);
        command.Parameters.AddWithValue("$enabled", account.IsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAt", SqliteValue.Instant(updatedAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertServiceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Service service,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO services(
                id, provider_id, provider_account_id, external_id, name,
                category, cost_behavior, is_enabled, updated_at_ms)
            VALUES ($id, $providerId, $accountId, $externalId, $name,
                    $category, $costBehavior, $enabled, $updatedAt)
            ON CONFLICT(id) DO UPDATE SET
                provider_id = excluded.provider_id,
                provider_account_id = excluded.provider_account_id,
                external_id = excluded.external_id,
                name = excluded.name,
                category = excluded.category,
                cost_behavior = excluded.cost_behavior,
                is_enabled = excluded.is_enabled,
                updated_at_ms = excluded.updated_at_ms;
            """;
        command.Parameters.AddWithValue("$id", service.Id);
        command.Parameters.AddWithValue("$providerId", service.ProviderId);
        command.Parameters.AddWithValue("$accountId", service.ProviderAccountId);
        command.Parameters.AddWithValue("$externalId", service.ExternalId);
        command.Parameters.AddWithValue("$name", service.Name);
        command.Parameters.AddWithValue("$category", (int)service.Category);
        command.Parameters.AddWithValue("$costBehavior", (int)service.CostBehavior);
        command.Parameters.AddWithValue("$enabled", service.IsEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAt", SqliteValue.Instant(updatedAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertUsageAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        UsageSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // Dos sentencias en un solo comando y un solo binding de parametros: la fila
        // historica y la proyeccion vigente que lee el dashboard.
        command.CommandText = """
            INSERT INTO usage_snapshots(
                id, service_id, metric_code, metric_name, metric_kind, unit,
                value_decimal, captured_at_ms, period_start_ms, period_end_ms,
                period_time_zone, source, accuracy)
            VALUES ($id, $serviceId, $code, $name, $kind, $unit,
                    $value, $capturedAt, $periodStart, $periodEnd,
                    $timeZone, $source, $accuracy)
            ON CONFLICT(id) DO NOTHING;

            INSERT INTO current_usage(
                service_id, metric_code, snapshot_id, metric_name, metric_kind, unit,
                value_decimal, captured_at_ms, period_start_ms, period_end_ms,
                period_time_zone, source, accuracy)
            VALUES ($serviceId, $code, $id, $name, $kind, $unit,
                    $value, $capturedAt, $periodStart, $periodEnd,
                    $timeZone, $source, $accuracy)
            ON CONFLICT(service_id, metric_code) DO UPDATE SET
                snapshot_id = excluded.snapshot_id,
                metric_name = excluded.metric_name,
                metric_kind = excluded.metric_kind,
                unit = excluded.unit,
                value_decimal = excluded.value_decimal,
                captured_at_ms = excluded.captured_at_ms,
                period_start_ms = excluded.period_start_ms,
                period_end_ms = excluded.period_end_ms,
                period_time_zone = excluded.period_time_zone,
                source = excluded.source,
                accuracy = excluded.accuracy
            WHERE excluded.captured_at_ms >= current_usage.captured_at_ms;
            """;
        command.Parameters.AddWithValue("$id", snapshot.Id);
        command.Parameters.AddWithValue("$serviceId", snapshot.ServiceId);
        command.Parameters.AddWithValue("$code", snapshot.Metric.Code);
        command.Parameters.AddWithValue("$name", snapshot.Metric.DisplayName);
        command.Parameters.AddWithValue("$kind", (int)snapshot.Metric.Kind);
        command.Parameters.AddWithValue("$unit", snapshot.Metric.Unit);
        command.Parameters.AddWithValue("$value", SqliteValue.Decimal(snapshot.Value));
        command.Parameters.AddWithValue("$capturedAt", SqliteValue.Instant(snapshot.CapturedAt));
        command.Parameters.AddWithValue("$periodStart", SqliteValue.Instant(snapshot.Period.StartsAt));
        command.Parameters.AddWithValue("$periodEnd", SqliteValue.Instant(snapshot.Period.EndsAt));
        command.Parameters.AddWithValue("$timeZone", snapshot.Period.TimeZoneId);
        command.Parameters.AddWithValue("$source", (int)snapshot.Source);
        command.Parameters.AddWithValue("$accuracy", (int)snapshot.Accuracy);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertBillingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        BillingRecord record,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO billing_records(
                id, service_id, amount_decimal, currency, captured_at_ms,
                period_start_ms, period_end_ms, period_time_zone,
                source, accuracy, external_invoice_id)
            VALUES ($id, $serviceId, $amount, $currency, $capturedAt,
                    $periodStart, $periodEnd, $timeZone,
                    $source, $accuracy, $invoiceId)
            ON CONFLICT(id) DO NOTHING;

            INSERT INTO current_billing(
                service_id, currency, record_id, amount_decimal, captured_at_ms,
                period_start_ms, period_end_ms, period_time_zone,
                source, accuracy, external_invoice_id)
            VALUES ($serviceId, $currency, $id, $amount, $capturedAt,
                    $periodStart, $periodEnd, $timeZone,
                    $source, $accuracy, $invoiceId)
            ON CONFLICT(service_id, currency) DO UPDATE SET
                record_id = excluded.record_id,
                amount_decimal = excluded.amount_decimal,
                captured_at_ms = excluded.captured_at_ms,
                period_start_ms = excluded.period_start_ms,
                period_end_ms = excluded.period_end_ms,
                period_time_zone = excluded.period_time_zone,
                source = excluded.source,
                accuracy = excluded.accuracy,
                external_invoice_id = excluded.external_invoice_id
            WHERE excluded.captured_at_ms >= current_billing.captured_at_ms;
            """;
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$serviceId", record.ServiceId);
        command.Parameters.AddWithValue("$amount", SqliteValue.Decimal(record.Amount.Amount));
        command.Parameters.AddWithValue("$currency", record.Amount.Currency);
        command.Parameters.AddWithValue("$capturedAt", SqliteValue.Instant(record.CapturedAt));
        command.Parameters.AddWithValue("$periodStart", SqliteValue.Instant(record.Period.StartsAt));
        command.Parameters.AddWithValue("$periodEnd", SqliteValue.Instant(record.Period.EndsAt));
        command.Parameters.AddWithValue("$timeZone", record.Period.TimeZoneId);
        command.Parameters.AddWithValue("$source", (int)record.Source);
        command.Parameters.AddWithValue("$accuracy", (int)record.Accuracy);
        command.Parameters.AddWithValue("$invoiceId", (object?)record.ExternalInvoiceId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertSubscriptionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Subscription subscription,
        DateTimeOffset updatedAt,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO subscriptions(
                id, service_id, name, amount_decimal, currency, cadence,
                next_renewal_at_ms, source, is_active, updated_at_ms)
            VALUES ($id, $serviceId, $name, $amount, $currency, $cadence,
                    $nextRenewal, $source, $active, $updatedAt)
            """ + (overwrite
                ? """
                    ON CONFLICT(id) DO UPDATE SET
                        service_id = excluded.service_id,
                        name = excluded.name,
                        amount_decimal = excluded.amount_decimal,
                        currency = excluded.currency,
                        cadence = excluded.cadence,
                        next_renewal_at_ms = excluded.next_renewal_at_ms,
                        source = excluded.source,
                        is_active = excluded.is_active,
                        updated_at_ms = excluded.updated_at_ms;
                    """
                : " ON CONFLICT(id) DO NOTHING;");
        command.Parameters.AddWithValue("$id", subscription.Id);
        command.Parameters.AddWithValue("$serviceId", (object?)subscription.ServiceId ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", subscription.Name);
        command.Parameters.AddWithValue("$amount", SqliteValue.Decimal(subscription.Price.Amount));
        command.Parameters.AddWithValue("$currency", subscription.Price.Currency);
        command.Parameters.AddWithValue("$cadence", (int)subscription.Cadence);
        command.Parameters.AddWithValue("$nextRenewal", SqliteValue.Instant(subscription.NextRenewalAt));
        command.Parameters.AddWithValue("$source", (int)subscription.Source);
        command.Parameters.AddWithValue("$active", subscription.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAt", SqliteValue.Instant(updatedAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertPaymentAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Payment payment,
        DateTimeOffset updatedAt,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO payments(
                id, subscription_id, name, amount_decimal, currency,
                due_at_ms, status, updated_at_ms)
            VALUES ($id, $subscriptionId, $name, $amount, $currency,
                    $dueAt, $status, $updatedAt)
            """ + (overwrite
                ? """
                    ON CONFLICT(id) DO UPDATE SET
                        subscription_id = excluded.subscription_id,
                        name = excluded.name,
                        amount_decimal = excluded.amount_decimal,
                        currency = excluded.currency,
                        due_at_ms = excluded.due_at_ms,
                        status = excluded.status,
                        updated_at_ms = excluded.updated_at_ms;
                    """
                : " ON CONFLICT(id) DO NOTHING;");
        command.Parameters.AddWithValue("$id", payment.Id);
        command.Parameters.AddWithValue("$subscriptionId", (object?)payment.SubscriptionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", payment.Name);
        command.Parameters.AddWithValue("$amount", SqliteValue.Decimal(payment.Amount.Amount));
        command.Parameters.AddWithValue("$currency", payment.Amount.Currency);
        command.Parameters.AddWithValue("$dueAt", SqliteValue.Instant(payment.DueAt));
        command.Parameters.AddWithValue("$status", (int)payment.Status);
        command.Parameters.AddWithValue("$updatedAt", SqliteValue.Instant(updatedAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertBudgetAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Budget budget,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO budgets(
                id, service_id, category, name, amount_decimal, currency,
                warning_percent, important_percent, critical_percent, updated_at_ms)
            VALUES ($id, $serviceId, $category, $name, $amount, $currency,
                    $warning, $important, $critical, $updatedAt)
            ON CONFLICT(id) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$id", budget.Id);
        command.Parameters.AddWithValue("$serviceId", (object?)budget.ServiceId ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "$category",
            budget.Category is { } category ? (object)(int)category : DBNull.Value);
        command.Parameters.AddWithValue("$name", budget.Name);
        command.Parameters.AddWithValue("$amount", SqliteValue.Decimal(budget.Limit.Amount));
        command.Parameters.AddWithValue("$currency", budget.Limit.Currency);
        command.Parameters.AddWithValue("$warning", budget.WarningPercent);
        command.Parameters.AddWithValue("$important", budget.ImportantPercent);
        command.Parameters.AddWithValue("$critical", budget.CriticalPercent);
        command.Parameters.AddWithValue("$updatedAt", SqliteValue.Instant(updatedAt));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, IReadOnlyList<UsageSnapshot>>> ReadLatestUsageAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        // Escaneo de una tabla con una fila por (servicio, metrica): su tamano depende de
        // cuantos servicios hay, no de cuanto historico se acumulo.
        command.CommandText = """
            SELECT snapshot_id, service_id, metric_code, metric_name, metric_kind, unit,
                   value_decimal, captured_at_ms, period_start_ms, period_end_ms,
                   period_time_zone, source, accuracy
            FROM current_usage;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new Dictionary<string, List<UsageSnapshot>>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            var serviceId = reader.GetString(1);
            var period = new BillingPeriod(
                SqliteValue.ReadInstant(reader.GetInt64(8)),
                SqliteValue.ReadInstant(reader.GetInt64(9)),
                reader.GetString(10));
            var snapshot = new UsageSnapshot(
                reader.GetString(0),
                serviceId,
                new UsageMetric(
                    reader.GetString(2),
                    reader.GetString(3),
                    (MetricKind)reader.GetInt32(4),
                    reader.GetString(5)),
                SqliteValue.ReadDecimal(reader.GetString(6)),
                SqliteValue.ReadInstant(reader.GetInt64(7)),
                period,
                (DataSourceKind)reader.GetInt32(11),
                (DataAccuracy)reader.GetInt32(12));
            if (!rows.TryGetValue(serviceId, out var list))
            {
                list = [];
                rows[serviceId] = list;
            }

            list.Add(snapshot);
        }

        return rows.ToDictionary(
            x => x.Key,
            x => (IReadOnlyList<UsageSnapshot>)x.Value,
            StringComparer.Ordinal);
    }

    private static async Task<IReadOnlyList<CachedServiceCost>> ReadLatestServiceCostsAsync(
        SqliteConnection connection,
        string currency,
        IReadOnlyDictionary<string, IReadOnlyList<UsageSnapshot>> usage,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id, s.provider_id, s.provider_account_id, s.external_id,
                   s.name, s.category, s.cost_behavior, s.is_enabled,
                   b.amount_decimal, b.currency, b.captured_at_ms,
                   b.period_start_ms, b.period_end_ms, b.period_time_zone,
                   b.source, b.accuracy
            FROM services s
            JOIN current_billing b ON b.service_id = s.id AND b.currency = $currency
            WHERE s.is_enabled = 1
            ORDER BY s.category, CAST(b.amount_decimal AS REAL) DESC;
            """;
        command.Parameters.AddWithValue("$currency", currency);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<CachedServiceCost>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetString(0);
            var service = new Service(
                id,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                (ServiceCategory)reader.GetInt32(5),
                (CostBehavior)reader.GetInt32(6),
                reader.GetBoolean(7));
            var period = new BillingPeriod(
                SqliteValue.ReadInstant(reader.GetInt64(11)),
                SqliteValue.ReadInstant(reader.GetInt64(12)),
                reader.GetString(13));
            rows.Add(new CachedServiceCost(
                service,
                new Money(SqliteValue.ReadDecimal(reader.GetString(8)), reader.GetString(9)),
                period,
                SqliteValue.ReadInstant(reader.GetInt64(10)),
                (DataSourceKind)reader.GetInt32(14),
                (DataAccuracy)reader.GetInt32(15),
                usage.GetValueOrDefault(id, [])));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<Budget>> ReadBudgetsAsync(
        SqliteConnection connection,
        string currency,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, amount_decimal, currency, warning_percent,
                   important_percent, critical_percent, service_id, category
            FROM budgets
            WHERE currency = $currency;
            """;
        command.Parameters.AddWithValue("$currency", currency);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<Budget>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new Budget(
                reader.GetString(0),
                reader.GetString(1),
                new Money(SqliteValue.ReadDecimal(reader.GetString(2)), reader.GetString(3)),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : (ServiceCategory)reader.GetInt32(8)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<Subscription>> ReadSubscriptionsAsync(
        SqliteConnection connection,
        string currency,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, amount_decimal, currency, cadence,
                   next_renewal_at_ms, source, service_id, is_active
            FROM subscriptions
            WHERE currency = $currency AND is_active = 1
            ORDER BY next_renewal_at_ms;
            """;
        command.Parameters.AddWithValue("$currency", currency);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<Subscription>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new Subscription(
                reader.GetString(0),
                reader.GetString(1),
                new Money(SqliteValue.ReadDecimal(reader.GetString(2)), reader.GetString(3)),
                (BillingCadence)reader.GetInt32(4),
                SqliteValue.ReadInstant(reader.GetInt64(5)),
                (DataSourceKind)reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetBoolean(8)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<Payment>> ReadUpcomingPaymentsAsync(
        SqliteConnection connection,
        string currency,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, amount_decimal, currency, due_at_ms, status, subscription_id
            FROM payments
            WHERE currency = $currency AND status = $scheduled AND due_at_ms >= $now
            ORDER BY due_at_ms
            LIMIT 12;
            """;
        command.Parameters.AddWithValue("$currency", currency);
        command.Parameters.AddWithValue("$scheduled", (int)PaymentStatus.Scheduled);
        command.Parameters.AddWithValue("$now", SqliteValue.Instant(now));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<Payment>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new Payment(
                reader.GetString(0),
                reader.GetString(1),
                new Money(SqliteValue.ReadDecimal(reader.GetString(2)), reader.GetString(3)),
                SqliteValue.ReadInstant(reader.GetInt64(4)),
                (PaymentStatus)reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<QuickAccessEntry>> ReadQuickAccessAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, display_name, kind, path, parent_id, default_action, sort_order, is_pinned
            FROM quick_access_entries
            WHERE is_pinned = 1
            ORDER BY COALESCE(parent_id, ''), sort_order, display_name;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<QuickAccessEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new QuickAccessEntry(
                reader.GetString(0),
                reader.GetString(1),
                (QuickAccessKind)reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                (QuickAccessAction)reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetBoolean(7)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ProviderState>> ReadProviderStatesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT provider_id, status, last_attempt_at_ms, last_success_at_ms,
                   next_refresh_at_ms, consecutive_failures, error_code, error_message
            FROM provider_states
            ORDER BY provider_id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<ProviderState>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(MapProviderState(reader));
        }

        return rows;
    }

    private static ProviderState MapProviderState(SqliteDataReader reader) => new(
        reader.GetString(0),
        (ProviderStatus)reader.GetInt32(1),
        SqliteValue.ReadNullableInstant(reader.GetValue(2)),
        SqliteValue.ReadNullableInstant(reader.GetValue(3)),
        SqliteValue.ReadNullableInstant(reader.GetValue(4)),
        reader.GetInt32(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7));
}
