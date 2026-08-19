using System.Text.Json.Serialization;

namespace DevStatusCenter.Providers.Neon;

// DTOs de la Neon API v2. Se mantienen dentro de este ensamblado a propósito: el modelo de
// dominio no debe conocer la forma de ningún proveedor (CONTRIBUTING, regla 3).
//
// Forma tomada de la referencia pública de Neon el 19-ago-2026:
//   GET /api/v2/users/me/organizations
//   GET /api/v2/projects?org_id=
//   GET /api/v2/consumption_history/v2/projects?org_id=&from=&to=&granularity=&metrics=

internal sealed class NeonOrganizationsResponse
{
    [JsonPropertyName("organizations")]
    public List<NeonOrganization>? Organizations { get; set; }
}

internal sealed class NeonOrganization
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("plan")]
    public string? Plan { get; set; }
}

internal sealed class NeonProjectsResponse
{
    [JsonPropertyName("projects")]
    public List<NeonProject>? Projects { get; set; }

    [JsonPropertyName("pagination")]
    public NeonPagination? Pagination { get; set; }
}

internal sealed class NeonProject
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("org_id")]
    public string? OrgId { get; set; }
}

internal sealed class NeonPagination
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

internal sealed class NeonConsumptionResponse
{
    [JsonPropertyName("projects")]
    public List<NeonProjectConsumption>? Projects { get; set; }

    [JsonPropertyName("pagination")]
    public NeonPagination? Pagination { get; set; }
}

internal sealed class NeonProjectConsumption
{
    [JsonPropertyName("project_id")]
    public string? ProjectId { get; set; }

    [JsonPropertyName("periods")]
    public List<NeonConsumptionPeriod>? Periods { get; set; }
}

internal sealed class NeonConsumptionPeriod
{
    [JsonPropertyName("period_id")]
    public string? PeriodId { get; set; }

    [JsonPropertyName("period_plan")]
    public string? PeriodPlan { get; set; }

    [JsonPropertyName("period_start")]
    public DateTimeOffset? PeriodStart { get; set; }

    [JsonPropertyName("period_end")]
    public DateTimeOffset? PeriodEnd { get; set; }

    [JsonPropertyName("consumption")]
    public List<NeonConsumptionBucket>? Consumption { get; set; }
}

internal sealed class NeonConsumptionBucket
{
    [JsonPropertyName("timeframe_start")]
    public DateTimeOffset? TimeframeStart { get; set; }

    [JsonPropertyName("timeframe_end")]
    public DateTimeOffset? TimeframeEnd { get; set; }

    [JsonPropertyName("metrics")]
    public List<NeonMetric>? Metrics { get; set; }
}

internal sealed class NeonMetric
{
    [JsonPropertyName("metric_name")]
    public string? MetricName { get; set; }

    [JsonPropertyName("value")]
    public decimal Value { get; set; }
}

/// <summary>
/// Contexto de serialización generado en compilación: sin arranque en frío del serializador por
/// reflexión y compatible con trimming.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(NeonOrganizationsResponse))]
[JsonSerializable(typeof(NeonProjectsResponse))]
[JsonSerializable(typeof(NeonConsumptionResponse))]
internal sealed partial class NeonJsonContext : JsonSerializerContext;
