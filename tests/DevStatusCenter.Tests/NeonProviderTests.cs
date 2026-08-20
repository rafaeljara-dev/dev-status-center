using System.Net;
using DevStatusCenter.Application.Networking;
using DevStatusCenter.Application.Providers;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Providers.Neon;

namespace DevStatusCenter.Tests;

/// <summary>
/// Las respuestas son fixtures con la forma documentada de la Neon API v2 (19-ago-2026). Fijan
/// el parsing, la conversión de unidades, el cálculo de costo y el mapeo de errores sin necesidad
/// de una credencial real. Lo que NO pueden verificar es que Neon siga devolviendo exactamente
/// esta forma: eso exige una llamada real con token, y está anotado en CONNECTIONS_TODO.md.
/// </summary>
public sealed class NeonProviderTests
{
    private const string Reference = "neon-personal";

    private static readonly DateTimeOffset Now = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    private const string OrganizationsPayload = """
        {
          "organizations": [
            { "id": "org-happy-lab-12345678", "name": "Personal", "plan": "launch" }
          ]
        }
        """;

    private const string ProjectsPayload = """
        {
          "projects": [
            { "id": "proj-alpha-1111", "name": "alpha", "org_id": "org-happy-lab-12345678" },
            { "id": "proj-beta-2222",  "name": "beta",  "org_id": "org-happy-lab-12345678" }
          ],
          "pagination": { "cursor": "" }
        }
        """;

    // 36.000 compute-segundos = 10 CU-hours. 4e9 bytes-mes = 4 GB-month.
    // 2e9 bytes de salida pública = 2 GB, por debajo de la franquicia de 500 GB.
    private const string ConsumptionPayload = """
        {
          "projects": [
            {
              "project_id": "proj-alpha-1111",
              "periods": [
                {
                  "period_id": "period-1",
                  "period_plan": "launch",
                  "period_start": "2026-08-01T00:00:00Z",
                  "consumption": [
                    {
                      "timeframe_start": "2026-08-01T00:00:00Z",
                      "timeframe_end": "2026-08-19T12:00:00Z",
                      "metrics": [
                        { "metric_name": "compute_unit_seconds", "value": 36000 },
                        { "metric_name": "root_branch_bytes_month", "value": 4000000000 },
                        { "metric_name": "public_network_transfer_bytes", "value": 2000000000 }
                      ]
                    }
                  ]
                }
              ]
            }
          ],
          "pagination": { "cursor": "" }
        }
        """;

    [Fact]
    public async Task RefreshAsync_MapsProjectsUsageAndCalculatedCost()
    {
        var (provider, handler) = Build(new ScriptedHttpHandler()
            .Json("users/me/organizations", OrganizationsPayload)
            .Json("api/v2/projects?", ProjectsPayload)
            .Json("consumption_history", ConsumptionPayload));

        var result = await provider.RefreshAsync(Context(), CancellationToken.None);

        Assert.Equal("neon", result.ProviderId);
        var account = Assert.Single(result.Accounts);
        Assert.Equal("Personal", account.DisplayName);
        Assert.Equal("org-happy-lab-12345678", account.ExternalAccountId);
        Assert.Equal(Reference, account.CredentialReference);

        // Neon se presenta como una sola fila con todo sumado, no una por proyecto: con dieciocho
        // proyectos el desglose convierte el popup en una lista. El dato por proyecto sigue
        // pidiéndose y llegando; lo que se agrega es la presentación.
        var alpha = Assert.Single(result.Observations);
        Assert.Equal("Neon", alpha.Service.Name);
        Assert.Equal(ServiceCategory.Infrastructure, alpha.Service.Category);
        Assert.Equal(CostBehavior.Variable, alpha.Service.CostBehavior);

        var compute = alpha.Usage.Single(x => x.Metric.Code == "compute");
        Assert.Equal(10m, compute.Value);                 // 36.000 s / 3600
        Assert.Equal("CU-hours", compute.Metric.Unit);

        var storage = alpha.Usage.Single(x => x.Metric.Code == "storage-root");
        Assert.Equal(4m, storage.Value);                  // 4e9 bytes / 1e9

        // 10 CU-h x 0,106 + 4 GB-mes x 0,35 = 1,06 + 1,40 = 2,46. La salida pública queda
        // dentro de la franquicia y no suma nada.
        var billing = Assert.Single(alpha.Billing);
        Assert.Equal(2.46m, billing.Amount.Amount);
        Assert.Equal("USD", billing.Amount.Currency);

        // El consumo es dato oficial; el importe lo calculamos nosotros. No se mezclan.
        Assert.All(alpha.Usage, x => Assert.Equal(DataAccuracy.ProviderReported, x.Accuracy));
        Assert.Equal(DataSourceKind.OfficialUsageApi, billing.Source);
        Assert.Equal(DataAccuracy.Calculated, billing.Accuracy);

        Assert.Equal("Bearer token-de-prueba", handler.LastAuthorization);
    }

    [Fact]
    public async Task RefreshAsync_RequestsEveryBillableMetricForTheCurrentMonth()
    {
        var (provider, handler) = Build(new ScriptedHttpHandler()
            .Json("users/me/organizations", OrganizationsPayload)
            .Json("api/v2/projects?", ProjectsPayload)
            .Json("consumption_history", ConsumptionPayload));

        await provider.RefreshAsync(Context(), CancellationToken.None);

        var consumption = handler.Requests.Single(x => x.ToString().Contains("consumption_history", StringComparison.Ordinal));
        var query = Uri.UnescapeDataString(consumption.Query);

        Assert.Contains("from=2026-08-01T00:00:00Z", query, StringComparison.Ordinal);

        // El "to" es el fin del periodo, no el instante del refresh. Con granularidad mensual
        // Neon trunca los dos extremos al inicio de su mes: recortar a "ahora" hacia que from y
        // to cayeran en el mismo instante y la API devolvia 400. Esta linea es la que impide que
        // esa optimizacion aparentemente inofensiva vuelva a entrar.
        Assert.Contains("to=2026-09-01T00:00:00Z", query, StringComparison.Ordinal);
        Assert.Contains("org_id=org-happy-lab-12345678", query, StringComparison.Ordinal);
        Assert.All(
            NeonBillableUnits.RequestedMetrics,
            metric => Assert.Contains($"metrics={metric}", query, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefreshAsync_FailsAsAuthenticationWhenThereIsNoStoredToken()
    {
        var handler = new ScriptedHttpHandler();
        var provider = new NeonProvider(
            new ResilientHttpExecutor(new HttpClient(handler), TimeProvider.System),
            new FakeSecretStore(),
            new NeonProviderOptions(Reference),
            TimeProvider.System);

        var error = await Assert.ThrowsAsync<ProviderRefreshException>(
            () => provider.RefreshAsync(Context(), CancellationToken.None));

        Assert.Equal(ProviderFailureKind.Authentication, error.Kind);
        Assert.Equal("credential_missing", error.ErrorCode);

        // Sin credencial no se toca la red: nada de disparar 401 contra Neon en cada ciclo.
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ProviderFailureKind.Authentication)]
    [InlineData(HttpStatusCode.Forbidden, ProviderFailureKind.Authentication)]
    [InlineData(HttpStatusCode.TooManyRequests, ProviderFailureKind.RateLimited)]
    [InlineData(HttpStatusCode.NotFound, ProviderFailureKind.Permanent)]
    public async Task RefreshAsync_ClassifiesHttpFailures(
        HttpStatusCode status,
        ProviderFailureKind expected)
    {
        var (provider, _) = Build(
            new ScriptedHttpHandler().Status("users/me/organizations", status),
            maximumRetries: 0);

        var error = await Assert.ThrowsAsync<ProviderRefreshException>(
            () => provider.RefreshAsync(Context(), CancellationToken.None));

        Assert.Equal(expected, error.Kind);
        Assert.Equal("neon", error.ProviderId);
    }

    [Fact]
    public async Task RefreshAsync_RetriesATransientServerError()
    {
        var (provider, handler) = Build(new ScriptedHttpHandler()
            .FailThenJson("users/me/organizations", HttpStatusCode.ServiceUnavailable, times: 1, OrganizationsPayload)
            .Json("api/v2/projects?", ProjectsPayload)
            .Json("consumption_history", ConsumptionPayload));

        var result = await provider.RefreshAsync(Context(), CancellationToken.None);

        Assert.Equal(2, handler.HitsFor("users/me/organizations"));
        Assert.Single(result.Observations);
    }

    [Fact]
    public async Task RefreshAsync_ReportsAMalformedResponseWithoutLeakingTheBody()
    {
        var (provider, _) = Build(new ScriptedHttpHandler()
            .Json("users/me/organizations", """{"organizations": [ {"id": """));

        var error = await Assert.ThrowsAsync<ProviderRefreshException>(
            () => provider.RefreshAsync(Context(), CancellationToken.None));

        Assert.Equal(ProviderFailureKind.InvalidResponse, error.Kind);
        Assert.DoesNotContain("organizations", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshAsync_SkipsOrganizationDiscoveryWhenTheAccountIsConfigured()
    {
        var (provider, handler) = Build(
            new ScriptedHttpHandler()
                .Json("api/v2/projects?", ProjectsPayload)
                .Json("consumption_history", ConsumptionPayload),
            options: new NeonProviderOptions(Reference, OrganizationId: "org-fijada"));

        await provider.RefreshAsync(Context(), CancellationToken.None);

        Assert.DoesNotContain(handler.Requests, x => x.ToString().Contains("organizations", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, x => x.Query.Contains("org_id=org-fijada", StringComparison.Ordinal));
    }

    [Fact]
    public void Descriptor_RespectsNeonsDocumentedPollingFloor()
    {
        var (provider, _) = Build(new ScriptedHttpHandler());

        // Neon documenta ~50 req/min por cuenta y desaconseja sondear por debajo de 15 minutos.
        Assert.Equal(TimeSpan.FromMinutes(15), provider.Descriptor.RefreshPolicy.MinimumInterval);
        Assert.True(provider.Descriptor.RefreshPolicy.NormalInterval >= TimeSpan.FromMinutes(15));
        Assert.Equal(
            ProviderCapabilities.Usage | ProviderCapabilities.Billing,
            provider.Descriptor.Capabilities);
    }

    [Fact]
    public void BillableUnits_ConvertRawMetricsIntoNeonsBillingUnits()
    {
        var units = NeonBillableUnits.FromRawMetrics(new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["compute_unit_seconds"] = 7_200m,
            ["root_branch_bytes_month"] = 1_000_000_000m,
            ["extra_branches_month"] = 744m,
            ["private_network_transfer_bytes"] = 500_000_000m
        });

        Assert.Equal(2m, units.ComputeUnitHours);
        Assert.Equal(1m, units.RootStorageGbMonths);
        Assert.Equal(1m, units.ExtraBranchMonths);
        Assert.Equal(0.5m, units.PrivateTransferGb);

        // Una métrica ausente vale cero, no rompe.
        Assert.Equal(0m, units.SnapshotStorageGbMonths);
    }

    [Fact]
    public void CostIn_AppliesTheFreePublicTransferAllowancePerProject()
    {
        var units = NeonBillableUnits.FromRawMetrics(new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["public_network_transfer_bytes"] = 600_000_000_000m   // 600 GB
        });

        // La franquicia se descuenta antes de sumar proyectos, no dentro del calculo del costo:
        // es por proyecto, y aplicarla despues de agregar convertiria dieciocho franquicias en
        // una sola. CostIn cobra lo que le llega.
        var facturable = units.WithFreeTransferApplied(NeonPricing.Launch);

        // 600 GB - 500 GB de franquicia = 100 GB facturables x 0,10 = 10,00
        Assert.Equal(10.00m, facturable.CostIn(NeonPricing.Launch));

        // Sin franquicia se cobraría todo: 600 x 0,10 = 60,00
        Assert.Equal(
            60.00m,
            units
                .WithFreeTransferApplied(NeonPricing.Launch with { FreePublicTransferGbPerProject = 0m })
                .CostIn(NeonPricing.Launch));
    }

    [Fact]
    public void Pricing_UsesScaleRatesForScaleAndEnterprisePlans()
    {
        Assert.Equal(0.106m, NeonPricing.ForPlan("launch").ComputeUnitHour);
        Assert.Equal(0.222m, NeonPricing.ForPlan("scale").ComputeUnitHour);
        Assert.Equal(0.222m, NeonPricing.ForPlan("ENTERPRISE").ComputeUnitHour);

        // Un plan desconocido cae a la tarifa más baja: subestimar es preferible a inventar caro.
        Assert.Equal(0.106m, NeonPricing.ForPlan("plan-que-no-existe").ComputeUnitHour);
    }

    private static ProviderRefreshContext Context() => new(Now, IsManual: false, DisplayCurrency: "USD");

    private static (NeonProvider Provider, ScriptedHttpHandler Handler) Build(
        ScriptedHttpHandler handler,
        int maximumRetries = 1,
        NeonProviderOptions? options = null)
    {
        var provider = new NeonProvider(
            new ResilientHttpExecutor(
                new HttpClient(handler),
                TimeProvider.System,
                requestTimeout: TimeSpan.FromSeconds(5),
                maximumRetries: maximumRetries),
            FakeSecretStore.With(Reference, "token-de-prueba"),
            options ?? new NeonProviderOptions(Reference),
            TimeProvider.System);
        return (provider, handler);
    }
}
