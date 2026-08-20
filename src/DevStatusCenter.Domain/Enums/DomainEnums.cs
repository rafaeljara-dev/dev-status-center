namespace DevStatusCenter.Domain.Enums;

public enum ServiceCategory
{
    Ai,
    Infrastructure,
    Development,
    Subscription,
    Domain,
    Other
}

public enum CostBehavior
{
    Variable,
    Fixed,
    Mixed,

    /// <summary>
    /// Plan de tarifa plana en el que lo que importa es la cuota consumida, no el dinero: Claude
    /// Code y Codex. Estos servicios <b>no suman al gasto del mes ni al presupuesto</b>; mezclar
    /// una mensualidad fija con el consumo variable de la nube hace que ninguna de las dos cifras
    /// signifique nada.
    /// </summary>
    PlanQuota
}

public enum BillingCadence
{
    OneTime,
    Weekly,
    Monthly,
    Quarterly,
    Yearly,
    Custom
}

public enum DataSourceKind
{
    OfficialBillingApi,
    OfficialUsageApi,
    Invoice,
    Manual,
    Mock
}

public enum DataAccuracy
{
    Exact,
    ProviderReported,
    Calculated,
    Estimated,
    Manual,
    Stale
}

public enum ProviderStatus
{
    Healthy,
    Refreshing,
    Stale,
    RateLimited,
    AuthenticationRequired,
    Error,
    Disabled
}

public enum PowerMode
{
    Normal,
    Eco,
    Paused,
    Gaming
}

public enum MetricKind
{
    TokensInput,
    TokensOutput,
    TokensCached,
    Compute,
    Storage,
    DataTransfer,
    Requests,
    QuotaConsumed,
    Custom
}

public enum AlertSeverity
{
    Info,
    Warning,
    Important,
    Critical
}

public enum PaymentStatus
{
    Scheduled,
    Paid,
    Skipped,
    Failed
}


/// <summary>
/// Estado de salud de un servicio de terceros, normalizado desde la pagina de estado que publique
/// cada proveedor. Es deliberadamente mas grueso que lo que ofrece Statuspage: lo que cambia una
/// decision es "puedo trabajar o no", no el matiz exacto del incidente.
/// </summary>
public enum HealthIndicator
{
    Unknown = 0,
    Operational = 1,
    Maintenance = 2,
    Degraded = 3,
    PartialOutage = 4,
    MajorOutage = 5
}
