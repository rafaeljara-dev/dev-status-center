using System.Text.Json.Serialization;

namespace DevStatusCenter.Application.Health;

// Solo los campos que se usan. Deserializar el resto de la pagina de estado — componentes,
// mantenimientos programados, historial — seria pagar por datos que nunca se muestran.

internal sealed record StatuspageSummary
{
    [JsonPropertyName("status")]
    public StatuspageStatus? Status { get; init; }

    [JsonPropertyName("incidents")]
    public IReadOnlyList<StatuspageIncident>? Incidents { get; init; }
}

internal sealed record StatuspageStatus
{
    /// <summary>none · minor · major · critical · maintenance</summary>
    [JsonPropertyName("indicator")]
    public string? Indicator { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

internal sealed record StatuspageIncident
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("shortlink")]
    public string? Shortlink { get; init; }

    [JsonPropertyName("impact")]
    public string? Impact { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

internal sealed record InstatusSummary
{
    [JsonPropertyName("page")]
    public InstatusPage? Page { get; init; }
}

internal sealed record InstatusPage
{
    /// <summary>UP · HASISSUES · UNDERMAINTENANCE</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

internal sealed record GoogleIncident
{
    [JsonPropertyName("service_name")]
    public string? ServiceName { get; init; }

    [JsonPropertyName("external_desc")]
    public string? ExternalDescription { get; init; }

    /// <summary>Nulo mientras el incidente sigue abierto. Es la unica senal de "esta pasando ahora".</summary>
    [JsonPropertyName("end")]
    public DateTimeOffset? End { get; init; }

    [JsonPropertyName("begin")]
    public DateTimeOffset? Begin { get; init; }

    [JsonPropertyName("severity")]
    public string? Severity { get; init; }

    /// <summary>Relativa a la pagina de estado; se compone con ella para tener un enlace usable.</summary>
    [JsonPropertyName("uri")]
    public string? Uri { get; init; }
}

/// <summary>
/// Generacion en tiempo de compilacion: sin esto, el primer arranque paga la reflexion de
/// System.Text.Json para dieciseis paginas de estado a la vez.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(StatuspageSummary))]
[JsonSerializable(typeof(InstatusSummary))]
[JsonSerializable(typeof(IReadOnlyList<GoogleIncident>))]
internal sealed partial class HealthJsonContext : JsonSerializerContext;
