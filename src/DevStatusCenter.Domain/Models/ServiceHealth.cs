using DevStatusCenter.Domain.Common;
using DevStatusCenter.Domain.Enums;

namespace DevStatusCenter.Domain.Models;

/// <summary>
/// Salud publicada de un servicio de terceros: GitHub, Vercel, Cloudflare, Claude, Codex.
///
/// Es informacion del proveedor sobre si mismo, no una medicion nuestra. No comprobamos si el
/// servicio responde desde esta maquina — eso confundiria un corte de tu wifi con una caida de
/// GitHub — sino que leemos lo que el proveedor declara en su pagina de estado.
/// </summary>
public sealed record ServiceHealth
{
    public ServiceHealth(
        string key,
        string displayName,
        HealthIndicator indicator,
        string description,
        string statusPageUrl,
        DateTimeOffset checkedAt,
        string? incidentTitle = null,
        string? incidentUrl = null)
    {
        Key = Guard.NotBlank(key, nameof(key));
        DisplayName = Guard.NotBlank(displayName, nameof(displayName));
        Indicator = indicator;
        Description = description ?? string.Empty;
        StatusPageUrl = Guard.NotBlank(statusPageUrl, nameof(statusPageUrl));
        CheckedAt = checkedAt.ToUniversalTime();
        IncidentTitle = incidentTitle;
        IncidentUrl = incidentUrl;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public HealthIndicator Indicator { get; }

    /// <summary>Lo que dice el proveedor, tal cual: "All Systems Operational", etc.</summary>
    public string Description { get; }

    public string StatusPageUrl { get; }

    public DateTimeOffset CheckedAt { get; }

    public string? IncidentTitle { get; }

    public string? IncidentUrl { get; }

    /// <summary>Todo lo que no sea operativo o mantenimiento merece aparecer arriba del todo.</summary>
    public bool IsDisrupted => Indicator is HealthIndicator.Degraded
        or HealthIndicator.PartialOutage
        or HealthIndicator.MajorOutage;
}
