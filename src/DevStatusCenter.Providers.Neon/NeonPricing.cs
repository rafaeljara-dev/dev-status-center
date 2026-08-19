namespace DevStatusCenter.Providers.Neon;

/// <summary>
/// Precios por unidad facturable de Neon.
///
/// Neon expone <b>consumo</b>, no importe: su API devuelve unidades (compute-segundos, bytes-mes,
/// bytes transferidos), nunca dólares. El costo que muestra el dashboard es por tanto un
/// <i>cálculo</i>, no una factura, y se marca como tal
/// (<see cref="Domain.Enums.DataAccuracy.Calculated"/>).
///
/// Los valores por defecto son las tarifas de lista publicadas por Neon para el plan Launch,
/// consultadas el <b>19-ago-2026</b>. No son una fuente de verdad: descuentos, plan Scale,
/// contratos anuales o cambios de tarifa hacen que difieran de tu factura real.
///
/// Hoy el plan se deduce de la organización y de ahí sale la tarifa. Exponerlos en
/// <c>appsettings.json</c> está pendiente; hasta entonces, contrasta el importe calculado contra
/// la factura del mes antes de fiarte de él.
/// </summary>
public sealed record NeonPricing(
    decimal ComputeUnitHour,
    decimal RootStorageGbMonth,
    decimal ChildStorageGbMonth,
    decimal InstantRestoreGbMonth,
    decimal SnapshotStorageGbMonth,
    decimal PublicTransferGb,
    decimal PrivateTransferGb,
    decimal ExtraBranchMonth,
    decimal FreePublicTransferGbPerProject,
    string Currency)
{
    /// <summary>Tarifas de lista del plan Launch (19-ago-2026).</summary>
    public static NeonPricing Launch { get; } = new(
        ComputeUnitHour: 0.106m,
        RootStorageGbMonth: 0.35m,
        ChildStorageGbMonth: 0.35m,
        InstantRestoreGbMonth: 0.20m,
        SnapshotStorageGbMonth: 0.09m,
        PublicTransferGb: 0.10m,
        PrivateTransferGb: 0m,
        ExtraBranchMonth: 1.50m,

        // Los planes de pago incluyen 500 GB de salida pública gratis por proyecto.
        FreePublicTransferGbPerProject: 500m,
        Currency: "USD");

    /// <summary>Tarifas de lista del plan Scale (19-ago-2026).</summary>
    public static NeonPricing Scale { get; } = Launch with
    {
        ComputeUnitHour = 0.222m,
        PrivateTransferGb = 0.01m
    };

    public static NeonPricing ForPlan(string? plan) => plan?.Trim().ToUpperInvariant() switch
    {
        "SCALE" or "ENTERPRISE" or "BUSINESS" => Scale,
        _ => Launch
    };
}

/// <summary>
/// Unidades facturables de Neon ya convertidas desde los valores crudos de la API. La conversión
/// vive aquí, en un solo sitio, para que ni el mapeo ni la UI tengan que recordar que
/// <c>compute_unit_seconds</c> se divide entre 3600 y los bytes entre 1e9.
/// </summary>
public readonly record struct NeonBillableUnits(
    decimal ComputeUnitHours,
    decimal RootStorageGbMonths,
    decimal ChildStorageGbMonths,
    decimal InstantRestoreGbMonths,
    decimal SnapshotStorageGbMonths,
    decimal PublicTransferGb,
    decimal PrivateTransferGb,
    decimal ExtraBranchMonths)
{
    private const decimal SecondsPerHour = 3_600m;

    /// <summary>Neon factura en GB decimales (10^9), no en GiB.</summary>
    private const decimal BytesPerGigabyte = 1_000_000_000m;

    /// <summary>Horas de un mes de facturación de Neon.</summary>
    private const decimal HoursPerBranchMonth = 744m;

    public static NeonBillableUnits FromRawMetrics(IReadOnlyDictionary<string, decimal> raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        return new NeonBillableUnits(
            Raw(raw, "compute_unit_seconds") / SecondsPerHour,
            Raw(raw, "root_branch_bytes_month") / BytesPerGigabyte,
            Raw(raw, "child_branch_bytes_month") / BytesPerGigabyte,
            Raw(raw, "instant_restore_bytes_month") / BytesPerGigabyte,
            Raw(raw, "snapshot_storage_bytes_month") / BytesPerGigabyte,
            Raw(raw, "public_network_transfer_bytes") / BytesPerGigabyte,
            Raw(raw, "private_network_transfer_bytes") / BytesPerGigabyte,
            Raw(raw, "extra_branches_month") / HoursPerBranchMonth);
    }

    /// <summary>
    /// Costo calculado del proyecto. La franquicia de salida pública se aplica por proyecto,
    /// que es como la concede Neon.
    /// </summary>
    public decimal CostIn(NeonPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        var billablePublicTransfer = Math.Max(
            0m,
            PublicTransferGb - pricing.FreePublicTransferGbPerProject);

        var total =
            (ComputeUnitHours * pricing.ComputeUnitHour) +
            (RootStorageGbMonths * pricing.RootStorageGbMonth) +
            (ChildStorageGbMonths * pricing.ChildStorageGbMonth) +
            (InstantRestoreGbMonths * pricing.InstantRestoreGbMonth) +
            (SnapshotStorageGbMonths * pricing.SnapshotStorageGbMonth) +
            (billablePublicTransfer * pricing.PublicTransferGb) +
            (PrivateTransferGb * pricing.PrivateTransferGb) +
            (ExtraBranchMonths * pricing.ExtraBranchMonth);

        return decimal.Round(Math.Max(0m, total), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal Raw(IReadOnlyDictionary<string, decimal> raw, string metric) =>
        raw.TryGetValue(metric, out var value) && value > 0m ? value : 0m;

    /// <summary>Nombres de métrica que se piden a la API, en el orden del query string.</summary>
    public static IReadOnlyList<string> RequestedMetrics { get; } =
    [
        "compute_unit_seconds",
        "root_branch_bytes_month",
        "child_branch_bytes_month",
        "instant_restore_bytes_month",
        "snapshot_storage_bytes_month",
        "public_network_transfer_bytes",
        "private_network_transfer_bytes",
        "extra_branches_month"
    ];
}
