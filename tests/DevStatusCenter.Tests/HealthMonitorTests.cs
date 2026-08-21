using DevStatusCenter.Application.Health;
using DevStatusCenter.Domain.Enums;

namespace DevStatusCenter.Tests;

public sealed class HealthMonitorTests
{
    private static readonly HealthTarget Cloudflare = new(
        "cloudflare",
        "Cloudflare",
        HealthFeedKind.Statuspage,
        "https://www.cloudflarestatus.com/api/v2/summary.json",
        "https://www.cloudflarestatus.com",
        true);

    /// <summary>
    /// El caso real que dejaba a Cloudflare en amarillo permanente: su indicador global agrega el
    /// estado de cientos de datacenters y casi nunca vale "none", pero la lista de incidentes sin
    /// resolver viene vacia. Sin incidente no esta pasando nada.
    /// </summary>
    [Fact]
    public async Task CheckAsync_TreatsMinorWithoutOpenIncidentsAsOperational()
    {
        var handler = new ScriptedHttpHandler().Json(
            "cloudflarestatus.com",
            """{"status":{"indicator":"minor","description":"Minor Service Outage"},"incidents":[]}""");
        using var client = new HttpClient(handler);
        var monitor = new HealthMonitor(client, TimeProvider.System);

        var result = await monitor.CheckAsync([Cloudflare], CancellationToken.None);

        var health = Assert.Single(result);
        Assert.Equal(HealthIndicator.Operational, health.Indicator);
        Assert.False(health.IsDisrupted);
    }

    [Fact]
    public async Task CheckAsync_KeepsMinorAsDegradedWhenAnIncidentIsOpen()
    {
        var handler = new ScriptedHttpHandler().Json(
            "cloudflarestatus.com",
            """
            {"status":{"indicator":"minor","description":"Minor Service Outage"},
             "incidents":[{"name":"Elevated errors in LHR","impact":"minor","status":"investigating"}]}
            """);
        using var client = new HttpClient(handler);
        var monitor = new HealthMonitor(client, TimeProvider.System);

        var result = await monitor.CheckAsync([Cloudflare], CancellationToken.None);

        var health = Assert.Single(result);
        Assert.Equal(HealthIndicator.Degraded, health.Indicator);
        Assert.Equal("Elevated errors in LHR", health.IncidentTitle);
    }

    /// <summary>Una pagina de estado ilegible no es una caida: no puede disparar una alarma.</summary>
    [Fact]
    public async Task CheckAsync_ReportsUnknownWhenTheStatusPageFails()
    {
        var handler = new ScriptedHttpHandler().Status(
            "cloudflarestatus.com",
            System.Net.HttpStatusCode.ServiceUnavailable);
        using var client = new HttpClient(handler);
        var monitor = new HealthMonitor(client, TimeProvider.System);

        var result = await monitor.CheckAsync([Cloudflare], CancellationToken.None);

        Assert.Equal(HealthIndicator.Unknown, Assert.Single(result).Indicator);
    }
}
