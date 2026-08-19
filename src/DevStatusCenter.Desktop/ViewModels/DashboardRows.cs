using System.Globalization;
using System.Windows;
using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;

namespace DevStatusCenter.Desktop.ViewModels;

public sealed record ServiceRowViewModel(
    string Name,
    string Cost,
    string Detail,
    string Confidence,
    decimal ProgressPercent)
{
    public static ServiceRowViewModel From(DashboardServiceRow row, CultureInfo culture)
    {
        var quota = row.Usage.FirstOrDefault(x => x.Metric.Kind == MetricKind.QuotaConsumed);
        var detail = quota is null
            ? $"Projected {FormatMoney(row.Projected.Amount, row.Projected.Currency, culture)}"
            : $"Quota {quota.Value:0.#}% · projected {FormatMoney(row.Projected.Amount, row.Projected.Currency, culture)}";
        return new ServiceRowViewModel(
            row.Name,
            FormatMoney(row.Current.Amount, row.Current.Currency, culture),
            detail,
            ConfidenceLabel(row.Source, row.Accuracy),
            Math.Clamp(quota?.Value ?? 0m, 0m, 100m));
    }

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
}

public sealed record PaymentRowViewModel(string Date, string Name, string Amount)
{
    public static PaymentRowViewModel From(Payment payment, CultureInfo culture) => new(
        payment.DueAt.ToLocalTime().ToString("MMM d", culture),
        payment.Name,
        ServiceRowViewModel.FormatMoney(payment.Amount.Amount, payment.Amount.Currency, culture));
}

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
