using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using DevStatusCenter.Application.Abstractions;
using DevStatusCenter.Application.Networking;
using DevStatusCenter.Application.Providers;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;
using DevStatusCenter.Domain.ValueObjects;

namespace DevStatusCenter.Providers.Neon;

public sealed record NeonProviderOptions(
    string CredentialReference,
    string? OrganizationId = null,
    NeonPricing? Pricing = null);

/// <summary>
/// Provider de Neon.
///
/// Neon entrega <b>consumo</b>, no importe facturado, así que este provider produce dos cosas
/// distintas y las marca distinto:
/// <list type="bullet">
/// <item>los <see cref="UsageSnapshot"/> son datos oficiales
/// (<see cref="DataSourceKind.OfficialUsageApi"/>, <see cref="DataAccuracy.ProviderReported"/>);</item>
/// <item>el <see cref="BillingRecord"/> es un cálculo a partir de esas unidades y una tabla de
/// precios (<see cref="DataAccuracy.Calculated"/>). No es una factura y la UI no debe presentarlo
/// como tal.</item>
/// </list>
/// </summary>
public sealed class NeonProvider : IProvider, IUsageProvider, IBillingProvider
{
    public const string ProviderId = "neon";

    private const string BaseAddress = "https://console.neon.tech/api/v2/";

    /// <summary>
    /// Neon documenta un limitador compartido de ~50 peticiones por minuto por cuenta y recomienda
    /// no sondear por debajo de 15 minutos. Ese es el mínimo, no una sugerencia.
    /// </summary>
    private static readonly RefreshPolicy Policy = new(
        minimumInterval: TimeSpan.FromMinutes(15),
        normalInterval: TimeSpan.FromMinutes(30),
        ecoInterval: TimeSpan.FromHours(3));

    /// <summary>Tope de páginas para que un cursor que no avanza no gire para siempre.</summary>
    private const int MaximumPages = 20;

    private const int PageSize = 100;

    private readonly ResilientHttpExecutor _http;
    private readonly ISecretStore _secrets;
    private readonly NeonProviderOptions _options;
    private readonly TimeProvider _timeProvider;

    public NeonProvider(
        ResilientHttpExecutor http,
        ISecretStore secrets,
        NeonProviderOptions options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.CredentialReference);

        _http = http;
        _secrets = secrets;
        _options = options;
        _timeProvider = timeProvider;
    }

    public ProviderDescriptor Descriptor { get; } = new(
        ProviderId,
        "Neon",
        ProviderCapabilities.Usage | ProviderCapabilities.Billing,
        Policy);

    public async Task<ProviderRefreshResult> RefreshAsync(
        ProviderRefreshContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var token = await ReadTokenAsync(cancellationToken);
        var organization = await ResolveOrganizationAsync(token, cancellationToken);
        var pricing = _options.Pricing ?? NeonPricing.ForPlan(organization.Plan);
        var period = CurrentMonth(context.RequestedAt);

        var projects = await ReadProjectsAsync(organization.Id, token, cancellationToken);
        var consumption = await ReadConsumptionAsync(organization.Id, period, token, cancellationToken);

        var account = new ProviderAccount(
            AccountIdFor(organization.Id),
            ProviderId,
            organization.Name,
            organization.Id,
            _options.CredentialReference);

        // Una sola fila para toda la organización, no una por proyecto. Con dieciocho proyectos,
        // desglosar convierte el popup en una lista que hay que leer entera para saber lo único
        // que se pregunta de un vistazo: cuánto va este mes. El desglose por proyecto está
        // anotado como posible paso siguiente en CLAUDE.md.
        //
        // La franquicia de salida pública es por proyecto, así que se aplica antes de sumar: si
        // se sumara primero, dieciocho franquicias de 500 GB se volverían una sola y aparecería
        // un cargo de red que Neon no cobra.
        var total = NeonBillableUnits.Empty;
        foreach (var (projectId, _) in projects)
        {
            var units = consumption.TryGetValue(projectId, out var raw)
                ? NeonBillableUnits.FromRawMetrics(raw)
                : NeonBillableUnits.Empty;
            total = total.Add(units.WithFreeTransferApplied(pricing));
        }

        var observation = BuildObservation(
            account.Id,
            organization.Id,
            "Neon",
            total,
            pricing,
            context.RequestedAt,
            period);

        return new ProviderRefreshResult(
            ProviderId,
            context.RequestedAt,
            [account],
            [observation],
            [],
            []);
    }

    public async Task<IReadOnlyList<ServiceObservation>> GetUsageAsync(
        ProviderRefreshContext context,
        CancellationToken cancellationToken)
    {
        var result = await RefreshAsync(context, cancellationToken);
        return [.. result.Observations.Select(x => x with { Billing = [] })];
    }

    public async Task<IReadOnlyList<ServiceObservation>> GetBillingAsync(
        ProviderRefreshContext context,
        CancellationToken cancellationToken)
    {
        var result = await RefreshAsync(context, cancellationToken);
        return [.. result.Observations.Select(x => x with { Usage = [] })];
    }

    private static string AccountIdFor(string organizationId) => $"{ProviderId}:{organizationId}";

    private async Task<string> ReadTokenAsync(CancellationToken cancellationToken)
    {
        var token = await _secrets.GetAsync(_options.CredentialReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ProviderRefreshException(
                ProviderFailureKind.Authentication,
                ProviderId,
                "credential_missing",
                $"No hay token guardado para '{_options.CredentialReference}'. " +
                "Añádelo en Providers & credentials.");
        }

        return token;
    }

    private async Task<(string Id, string Name, string? Plan)> ResolveOrganizationAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.OrganizationId))
        {
            return (_options.OrganizationId, _options.OrganizationId, null);
        }

        var response = await GetAsync(
            "users/me/organizations",
            token,
            NeonJsonContext.Default.NeonOrganizationsResponse,
            cancellationToken);

        var organization = response?.Organizations?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Id));
        if (organization?.Id is not { } id)
        {
            throw new ProviderRefreshException(
                ProviderFailureKind.InvalidResponse,
                ProviderId,
                "organization_not_found",
                "El token no da acceso a ninguna organización de Neon. " +
                "Fija el Account ID manualmente en Providers & credentials.");
        }

        return (id, string.IsNullOrWhiteSpace(organization.Name) ? id : organization.Name, organization.Plan);
    }

    private async Task<IReadOnlyList<(string Id, string Name)>> ReadProjectsAsync(
        string organizationId,
        string token,
        CancellationToken cancellationToken)
    {
        var projects = new List<(string Id, string Name)>();
        string? cursor = null;

        for (var page = 0; page < MaximumPages; page++)
        {
            var query = new QueryBuilder()
                .Add("org_id", organizationId)
                .Add("limit", PageSize.ToString(CultureInfo.InvariantCulture))
                .Add("cursor", cursor);

            var response = await GetAsync(
                $"projects{query}",
                token,
                NeonJsonContext.Default.NeonProjectsResponse,
                cancellationToken);

            var batch = response?.Projects ?? [];
            foreach (var project in batch)
            {
                if (project.Id is { Length: > 0 } id)
                {
                    projects.Add((id, string.IsNullOrWhiteSpace(project.Name) ? id : project.Name));
                }
            }

            cursor = response?.Pagination?.Cursor;
            if (string.IsNullOrEmpty(cursor) || batch.Count < PageSize)
            {
                break;
            }
        }

        return projects;
    }

    private async Task<Dictionary<string, Dictionary<string, decimal>>> ReadConsumptionAsync(
        string organizationId,
        BillingPeriod period,
        string token,
        CancellationToken cancellationToken)
    {
        var totals = new Dictionary<string, Dictionary<string, decimal>>(StringComparer.Ordinal);
        string? cursor = null;

        for (var page = 0; page < MaximumPages; page++)
        {
            var query = new QueryBuilder()
                .Add("org_id", organizationId)
                .Add("from", Rfc3339(period.StartsAt))
                // El "to" es siempre el fin del periodo, nunca "ahora". Con granularidad mensual
                // Neon trunca ambos extremos al inicio de su mes, asi que recortar a hoy hacia
                // que "from" y "to" cayeran en el mismo instante y la API respondia 400 con
                // "'from' must be before 'to'". Pedir el mes entero devuelve el consumo hasta la
                // fecha igual: la API no rellena el futuro.
                .Add("to", Rfc3339(period.EndsAt))
                .Add("granularity", "monthly")
                .Add("limit", PageSize.ToString(CultureInfo.InvariantCulture))
                .Add("cursor", cursor);

            foreach (var metric in NeonBillableUnits.RequestedMetrics)
            {
                query.Add("metrics", metric);
            }

            var response = await GetAsync(
                $"consumption_history/v2/projects{query}",
                token,
                NeonJsonContext.Default.NeonConsumptionResponse,
                cancellationToken);

            var batch = response?.Projects ?? [];
            foreach (var project in batch)
            {
                if (project.ProjectId is not { Length: > 0 } projectId)
                {
                    continue;
                }

                if (!totals.TryGetValue(projectId, out var metrics))
                {
                    metrics = new Dictionary<string, decimal>(StringComparer.Ordinal);
                    totals[projectId] = metrics;
                }

                // Se suman todos los buckets del periodo. Con granularity=monthly suele venir uno
                // solo, pero un periodo de facturación que cambia a mitad de mes produce varios.
                foreach (var bucket in project.Periods?.SelectMany(x => x.Consumption ?? []) ?? [])
                {
                    foreach (var metric in bucket.Metrics ?? [])
                    {
                        if (metric.MetricName is { Length: > 0 } name)
                        {
                            metrics[name] = metrics.GetValueOrDefault(name) + metric.Value;
                        }
                    }
                }
            }

            cursor = response?.Pagination?.Cursor;
            if (string.IsNullOrEmpty(cursor) || batch.Count < PageSize)
            {
                break;
            }
        }

        return totals;
    }

    private static ServiceObservation BuildObservation(
        string accountId,
        string projectId,
        string projectName,
        NeonBillableUnits units,
        NeonPricing pricing,
        DateTimeOffset capturedAt,
        BillingPeriod period)
    {
        var serviceId = $"{ProviderId}:{accountId}:{projectId}";
        var service = new Service(
            serviceId,
            ProviderId,
            accountId,
            projectId,
            projectName,
            ServiceCategory.Infrastructure,
            CostBehavior.Variable);

        var usage = new List<UsageSnapshot>(8);
        AddUsage(usage, serviceId, "compute", "Compute", MetricKind.Compute, "CU-hours", units.ComputeUnitHours, capturedAt, period);
        AddUsage(usage, serviceId, "storage-root", "Root storage", MetricKind.Storage, "GB-month", units.RootStorageGbMonths, capturedAt, period);
        AddUsage(usage, serviceId, "storage-child", "Branch storage", MetricKind.Storage, "GB-month", units.ChildStorageGbMonths, capturedAt, period);
        AddUsage(usage, serviceId, "instant-restore", "Instant restore", MetricKind.Storage, "GB-month", units.InstantRestoreGbMonths, capturedAt, period);
        AddUsage(usage, serviceId, "snapshots", "Snapshots", MetricKind.Storage, "GB-month", units.SnapshotStorageGbMonths, capturedAt, period);
        AddUsage(usage, serviceId, "transfer-public", "Public transfer", MetricKind.DataTransfer, "GB", units.PublicTransferGb, capturedAt, period);
        AddUsage(usage, serviceId, "transfer-private", "Private transfer", MetricKind.DataTransfer, "GB", units.PrivateTransferGb, capturedAt, period);
        AddUsage(usage, serviceId, "extra-branches", "Extra branches", MetricKind.Custom, "branch-month", units.ExtraBranchMonths, capturedAt, period);

        var billing = new BillingRecord(
            $"{serviceId}:billing:{capturedAt.ToUnixTimeMilliseconds()}",
            serviceId,
            new Money(units.CostIn(pricing), pricing.Currency),
            period,
            capturedAt,

            // Origen oficial, pero el importe lo calculamos nosotros: nunca "billed".
            DataSourceKind.OfficialUsageApi,
            DataAccuracy.Calculated);

        return new ServiceObservation(service, usage, [billing]);
    }

    private static void AddUsage(
        List<UsageSnapshot> target,
        string serviceId,
        string code,
        string name,
        MetricKind kind,
        string unit,
        decimal value,
        DateTimeOffset capturedAt,
        BillingPeriod period)
    {
        if (value <= 0m)
        {
            return;
        }

        target.Add(new UsageSnapshot(
            $"{serviceId}:{code}:{capturedAt.ToUnixTimeMilliseconds()}",
            serviceId,
            new UsageMetric(code, name, kind, unit),
            decimal.Round(value, 6, MidpointRounding.AwayFromZero),
            capturedAt,
            period,
            DataSourceKind.OfficialUsageApi,
            DataAccuracy.ProviderReported));
    }

    private async Task<T?> GetAsync<T>(
        string relativeUrl,
        string token,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        using var response = await _http.SendAsync(
            ProviderId,
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, BaseAddress + relativeUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                return request;
            },
            cancellationToken);

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken);
        }
        catch (JsonException ex)
        {
            // El mensaje no incluye el cuerpo: podría arrastrar datos de la cuenta a un log (FR-062).
            throw new ProviderRefreshException(
                ProviderFailureKind.InvalidResponse,
                ProviderId,
                "invalid_json",
                "Neon devolvió una respuesta que no coincide con el esquema esperado.",
                ex);
        }
    }

    private static BillingPeriod CurrentMonth(DateTimeOffset now)
    {
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return new BillingPeriod(start, start.AddMonths(1), "UTC");
    }

    private static string Rfc3339(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>Query string con escapado correcto y parámetros repetibles (<c>metrics</c>).</summary>
    private sealed class QueryBuilder
    {
        private readonly System.Text.StringBuilder _builder = new();

        public QueryBuilder Add(string name, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return this;
            }

            _builder.Append(_builder.Length == 0 ? '?' : '&')
                .Append(Uri.EscapeDataString(name))
                .Append('=')
                .Append(Uri.EscapeDataString(value));
            return this;
        }

        public override string ToString() => _builder.ToString();
    }
}
