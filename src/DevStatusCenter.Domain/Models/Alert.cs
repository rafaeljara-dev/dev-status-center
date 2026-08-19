using DevStatusCenter.Domain.Common;
using DevStatusCenter.Domain.Enums;

namespace DevStatusCenter.Domain.Models;

/// <summary>
/// Algo que merece interrumpir al usuario. El <see cref="Id"/> es determinista y se deriva de la
/// regla que la produjo, no del instante: eso es lo que permite reconocer "esta misma alerta" en
/// el siguiente ciclo y respetar el enfriamiento en vez de notificar cada quince minutos.
/// </summary>
public sealed record Alert
{
    public Alert(
        string id,
        AlertSeverity severity,
        string ruleType,
        string title,
        string body,
        decimal threshold,
        string? serviceId = null)
    {
        Id = Guard.NotBlank(id, nameof(id));
        Severity = severity;
        RuleType = Guard.NotBlank(ruleType, nameof(ruleType));
        Title = Guard.NotBlank(title, nameof(title));
        Body = Guard.NotBlank(body, nameof(body));
        Threshold = threshold;
        ServiceId = serviceId?.Trim();
    }

    public string Id { get; }

    public AlertSeverity Severity { get; }

    /// <summary>Familia de la regla: <c>budget</c>, <c>forecast</c>, <c>payment</c>, <c>provider</c>.</summary>
    public string RuleType { get; }

    public string Title { get; }

    public string Body { get; }

    /// <summary>Umbral que la disparó. Se guarda para poder explicar por qué saltó.</summary>
    public decimal Threshold { get; }

    public string? ServiceId { get; }
}
