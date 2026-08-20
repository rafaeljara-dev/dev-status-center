using System.Text.Json;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;

namespace DevStatusCenter.Application.Health;

/// <summary>
/// Lee las paginas de estado y traduce lo que publican a un indicador comun.
///
/// Ninguna de estas llamadas lleva credencial: son feeds publicos. Eso significa que activar esta
/// pestana no expone nada, pero tambien que <b>el proveedor sabe que lo estas mirando</b>, igual
/// que si abrieras su pagina en el navegador.
///
/// Un fallo al leer una pagina no es una caida del servicio: si la peticion revienta, se deja el
/// indicador en <see cref="HealthIndicator.Unknown"/> y se dice. Pintarlo de rojo convertiria un
/// corte de tu propia red en una alarma falsa, que es la peor forma de que un panel pierda
/// credibilidad.
/// </summary>
public sealed class HealthMonitor(HttpClient httpClient, TimeProvider timeProvider)
{
    /// <summary>
    /// Cuantas paginas se consultan a la vez. Cuatro es suficiente para que dieciseis tarden un
    /// par de segundos sin abrir una reata de conexiones desde una app de bandeja.
    /// </summary>
    private const int Concurrency = 4;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public async Task<IReadOnlyList<ServiceHealth>> CheckAsync(
        IReadOnlyList<HealthTarget> targets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
        {
            return [];
        }

        using var gate = new SemaphoreSlim(Concurrency, Concurrency);
        var results = await Task.WhenAll(targets.Select(async target =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await CheckOneAsync(target, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }));

        return [.. results.OrderByDescending(x => x.Indicator).ThenBy(x => x.DisplayName, StringComparer.Ordinal)];
    }

    private async Task<ServiceHealth> CheckOneAsync(HealthTarget target, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(Timeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, target.ApiUrl);
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            return target.Kind switch
            {
                HealthFeedKind.Statuspage => await ReadStatuspageAsync(target, stream, now, timeoutCts.Token),
                HealthFeedKind.Instatus => await ReadInstatusAsync(target, stream, now, timeoutCts.Token),
                _ => await ReadGoogleAsync(target, stream, now, timeoutCts.Token)
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException
                                       or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return Unknown(target, now, "Status page unreachable");
        }
    }

    private static async Task<ServiceHealth> ReadStatuspageAsync(
        HealthTarget target,
        Stream stream,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var summary = await JsonSerializer.DeserializeAsync(
            stream,
            HealthJsonContext.Default.StatuspageSummary,
            cancellationToken);
        if (summary?.Status is null)
        {
            return Unknown(target, now, "Status page returned no indicator");
        }

        // Un indicador que no reconocemos se trata como desconocido, no como sano: si Statuspage
        // añade un nivel nuevo, prefiero un hueco visible a un verde inventado.
        var indicator = summary.Status.Indicator?.ToLowerInvariant() switch
        {
            "none" => HealthIndicator.Operational,
            "maintenance" => HealthIndicator.Maintenance,
            "minor" => HealthIndicator.Degraded,
            "major" => HealthIndicator.PartialOutage,
            "critical" => HealthIndicator.MajorOutage,
            _ => HealthIndicator.Unknown
        };

        var incident = summary.Incidents is { Count: > 0 } list ? list[0] : null;
        return new ServiceHealth(
            target.Key,
            target.DisplayName,
            indicator,
            summary.Status.Description ?? string.Empty,
            target.PageUrl,
            now,
            incident?.Name,
            incident?.Shortlink);
    }

    private static async Task<ServiceHealth> ReadInstatusAsync(
        HealthTarget target,
        Stream stream,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var summary = await JsonSerializer.DeserializeAsync(
            stream,
            HealthJsonContext.Default.InstatusSummary,
            cancellationToken);
        var status = summary?.Page?.Status?.ToUpperInvariant();
        var indicator = status switch
        {
            "UP" => HealthIndicator.Operational,
            "UNDERMAINTENANCE" => HealthIndicator.Maintenance,
            "HASISSUES" => HealthIndicator.Degraded,
            _ => HealthIndicator.Unknown
        };

        return new ServiceHealth(
            target.Key,
            target.DisplayName,
            indicator,
            indicator switch
            {
                HealthIndicator.Operational => "All systems operational",
                HealthIndicator.Maintenance => "Under maintenance",
                HealthIndicator.Degraded => "Reported issues",
                _ => "Status unavailable"
            },
            target.PageUrl,
            now);
    }

    /// <summary>
    /// Google no publica un indicador global, solo incidentes. Un incidente sin <c>end</c> es uno
    /// que sigue abierto; si no hay ninguno, el servicio esta bien.
    /// </summary>
    private static async Task<ServiceHealth> ReadGoogleAsync(
        HealthTarget target,
        Stream stream,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var incidents = await JsonSerializer.DeserializeAsync(
            stream,
            HealthJsonContext.Default.IReadOnlyListGoogleIncident,
            cancellationToken);
        var open = incidents?
            .Where(x => x.End is null)
            .OrderByDescending(x => x.Begin ?? DateTimeOffset.MinValue)
            .ToArray() ?? [];

        if (open.Length == 0)
        {
            return new ServiceHealth(
                target.Key,
                target.DisplayName,
                HealthIndicator.Operational,
                "No open incidents",
                target.PageUrl,
                now);
        }

        var worst = open.Any(x => string.Equals(x.Severity, "high", StringComparison.OrdinalIgnoreCase))
            ? HealthIndicator.PartialOutage
            : HealthIndicator.Degraded;
        var first = open[0];
        return new ServiceHealth(
            target.Key,
            target.DisplayName,
            worst,
            open.Length == 1 ? "1 open incident" : $"{open.Length} open incidents",
            target.PageUrl,
            now,
            first.ExternalDescription ?? first.ServiceName,
            first.Uri is null ? target.PageUrl : $"{target.PageUrl}/{first.Uri}");
    }

    private static ServiceHealth Unknown(HealthTarget target, DateTimeOffset now, string reason) =>
        new(target.Key, target.DisplayName, HealthIndicator.Unknown, reason, target.PageUrl, now);
}
