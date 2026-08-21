using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Desktop.Branding;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;

// UseWindowsForms mete System.Drawing y System.Windows.Forms en los usings implicitos: sin
// estos alias, los tipos de abajo son ambiguos contra sus homonimos de WinForms.
using Brush = System.Windows.Media.Brush;

namespace DevStatusCenter.Desktop.ViewModels;

public sealed record ServiceRowViewModel(
    string Name,
    string Cost,
    string Amount,
    string Detail,
    string Confidence,
    decimal ProgressPercent,
    Geometry? Brand,
    GridLength ShareWidth,
    GridLength RestWidth,
    double BarOpacity)
{
    /// <param name="share">
    /// Proporcion del gasto de este servicio sobre el mayor de la lista, entre 0 y 1. Se compara
    /// contra el mayor y no contra el total: con seis servicios, todas las barras contra el total
    /// quedarian igual de cortas y no se distinguiria nada.
    /// </param>
    public static ServiceRowViewModel From(DashboardServiceRow row, CultureInfo culture, double share = 0d)
    {
        var quota = row.Usage.FirstOrDefault(x => x.Metric.Kind == MetricKind.QuotaConsumed);
        var percent = Math.Clamp(quota?.Value ?? 0m, 0m, 100m);

        // Un plan de tarifa plana no ensena dinero: lo que se quiere saber de Claude Code o Codex
        // es cuanto queda de la ventana, no cuanto cuesta la mensualidad, que ya se sabe.
        var detail = !row.TracksCost
            ? quota is null
                ? "No quota reported yet"
                : $"{100m - percent:0.#}% left"
            : quota is null
                ? $"Projected {FormatMoney(row.Projected.Amount, row.Projected.Currency, culture)}"
                : $"Quota {percent:0.#}% · projected {FormatMoney(row.Projected.Amount, row.Projected.Currency, culture)}";
        var clamped = Math.Clamp(row.TracksCost ? share : (double)percent / 100d, 0d, 1d);
        return new ServiceRowViewModel(
            row.Name,
            row.TracksCost ? FormatMoney(row.Current.Amount, row.Current.Currency, culture) : string.Empty,
            row.TracksCost
                ? FormatAmount(row.Current.Amount, culture)
                : quota is null ? "—" : $"{percent:0}%",
            detail,
            row.TracksCost ? ConfidenceLabel(row.Source, row.Accuracy) : QuotaLabel(row.Source, row.Accuracy),
            percent,
            BrandGlyphs.For(row.ProviderId, row.ExternalId),
            new GridLength(clamped, GridUnitType.Star),
            new GridLength(1d - clamped, GridUnitType.Star),
            // La barra se apaga con el importe: el ojo ordena antes por intensidad que por largo.
            0.32d + (0.68d * clamped));
    }

    /// <summary>
    /// Para los planes, la etiqueta dice de donde sale la cuota. Codex la publica exacta; para
    /// Claude Code la calculamos desde los transcripts, y llamar a eso "limite" seria inventar
    /// una precision que no tenemos.
    /// </summary>
    private static string QuotaLabel(DataSourceKind source, DataAccuracy accuracy) => (source, accuracy) switch
    {
        (_, DataAccuracy.ProviderReported) => "✓ Provider quota",
        (_, DataAccuracy.Calculated) => "≈ Computed from local logs",
        (DataSourceKind.Mock, _) => "◇ Demo data",
        _ => "≈ Estimated"
    };

    private static string ConfidenceLabel(DataSourceKind source, DataAccuracy accuracy) => (source, accuracy) switch
    {
        (DataSourceKind.OfficialBillingApi, _) => "✓ Provider billed",
        (DataSourceKind.OfficialUsageApi, DataAccuracy.Calculated or DataAccuracy.Estimated) => "≈ Usage estimate",
        (DataSourceKind.Invoice, _) => "✉ Invoice",
        (DataSourceKind.Manual, _) => "● Manual",
        (DataSourceKind.Mock, _) => "◇ Demo data",
        _ => accuracy.ToString()
    };

    internal static string FormatMoney(decimal amount, string currency, CultureInfo culture) =>
        string.Format(culture, "{0} {1:N2}", currency, amount);

    /// <summary>Solo la cifra: la moneda ya va una vez en el encabezado, repetirla es ruido.</summary>
    internal static string FormatAmount(decimal amount, CultureInfo culture) =>
        amount.ToString("N2", culture);
}

public sealed record PaymentRowViewModel(
    string Date,
    string Name,
    string Amount,
    string Relative)
{
    public static PaymentRowViewModel From(Payment payment, DateTimeOffset now, CultureInfo culture) => new(
        payment.DueAt.ToLocalTime().ToString("MMM d", culture),
        payment.Name,
        ServiceRowViewModel.FormatAmount(payment.Amount.Amount, culture),
        Countdown(payment.DueAt, now));

    private static string Countdown(DateTimeOffset dueAt, DateTimeOffset now)
    {
        var days = (int)Math.Ceiling((dueAt - now).TotalDays);
        return days switch
        {
            <= 0 => "due now",
            1 => "tomorrow",
            _ => $"in {days} days"
        };
    }
}

/// <summary>
/// Una fila de la pestaña de estado: lo que el proveedor dice de si mismo.
/// </summary>
public sealed record HealthRowViewModel(
    string Name,
    string Description,
    string? IncidentTitle,
    string Glyph,
    string Color,
    string Url,
    bool IsDisrupted,
    Geometry? Brand)
{
    public bool HasIncident => !string.IsNullOrWhiteSpace(IncidentTitle);

    /// <summary>
    /// La descripcion solo se muestra cuando aporta algo. "All Systems Operational" repetido en
    /// seis fichas es ruido; el punto verde ya lo dice.
    /// </summary>
    public bool ShowDescription => IsDisrupted || HasIncident;

    public static HealthRowViewModel From(ServiceHealth health)
    {
        ArgumentNullException.ThrowIfNull(health);

        // El glifo lleva el estado sin depender del color: sirve para daltonismo y sobrevive a
        // una captura de pantalla en blanco y negro.
        var (glyph, color) = health.Indicator switch
        {
            HealthIndicator.Operational => ("●", "#62D99C"),
            HealthIndicator.Maintenance => ("◔", "#99A4B5"),
            HealthIndicator.Degraded => ("▲", "#F6C85F"),
            HealthIndicator.PartialOutage => ("▲", "#F49A5A"),
            HealthIndicator.MajorOutage => ("■", "#F06464"),
            _ => ("?", "#6B7A90")
        };

        return new HealthRowViewModel(
            health.DisplayName,
            health.Description,
            health.IncidentTitle,
            glyph,
            color,
            health.IncidentUrl ?? health.StatusPageUrl,
            health.IsDisrupted,
            BrandGlyphs.For(health.Key, null));
    }
}

/// <summary>
/// Un bloque del medidor de presupuesto. Quince bloques en vez de una barra continua porque un
/// bloque es contable de un vistazo: se lee "ocho de quince" sin comparar longitudes.
/// </summary>
public sealed record MeterBlockViewModel(Brush Fill);

public sealed record QuickAccessRowViewModel(QuickAccessEntry Entry, int Depth)
{
    public string Name => Entry.DisplayName;

    public string Subtitle => Entry.Kind == QuickAccessKind.Group
        ? "Group"
        : Entry.Path ?? string.Empty;

    public string Glyph => Entry.Kind switch
    {
        QuickAccessKind.Group => "▾",
        QuickAccessKind.Project => "◆",
        _ => "▰"
    };

    public bool IsLaunchable => Entry.Kind != QuickAccessKind.Group;

    public Thickness Indent => new(Depth * 16d, 0, 0, 0);
}
