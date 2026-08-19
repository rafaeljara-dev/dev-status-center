using System.Collections.Frozen;

namespace DevStatusCenter.Application.Configuration;

/// <summary>
/// Configuración de un provider tal como queda en disco. Nunca contiene el secreto: sólo la
/// referencia lógica con la que <see cref="Abstractions.ISecretStore"/> lo recupera (FR-063).
/// </summary>
public sealed record ProviderOptions(
    bool Enabled,
    string? CredentialReference,
    string? AccountId)
{
    public static ProviderOptions Disabled { get; } = new(false, null, null);
}

/// <summary>
/// Opciones efectivas de la aplicación. Inmutable y ya validada: quien la recibe no vuelve a
/// comprobar rangos ni a expandir variables de entorno.
/// </summary>
public sealed class AppOptions
{
    public const string FileName = "appsettings.json";

    private AppOptions(
        string databasePath,
        string secretsPath,
        string displayCurrency,
        int normalConcurrency,
        int historyRetentionDays,
        FrozenDictionary<string, ProviderOptions> providers)
    {
        DatabasePath = databasePath;
        SecretsPath = secretsPath;
        DisplayCurrency = displayCurrency;
        NormalConcurrency = normalConcurrency;
        HistoryRetentionDays = historyRetentionDays;
        Providers = providers;
    }

    public string DatabasePath { get; }

    public string SecretsPath { get; }

    /// <summary>Código ISO 4217 en el que se presenta el dashboard.</summary>
    public string DisplayCurrency { get; }

    /// <summary>Providers refrescados en paralelo en modo Normal.</summary>
    public int NormalConcurrency { get; }

    public int HistoryRetentionDays { get; }

    public TimeSpan HistoryRetention => TimeSpan.FromDays(HistoryRetentionDays);

    public FrozenDictionary<string, ProviderOptions> Providers { get; }

    public ProviderOptions For(string providerId) =>
        Providers.GetValueOrDefault(providerId, ProviderOptions.Disabled);

    public bool IsEnabled(string providerId) => For(providerId).Enabled;

    /// <summary>
    /// Construye las opciones aplicando los valores por defecto y recortando lo que venga fuera
    /// de rango. Un archivo corrupto o a medio editar degrada a algo utilizable en vez de
    /// impedir el arranque: la aplicación vive en el tray y no puede quedarse sin UI por una
    /// coma mal puesta.
    /// </summary>
    public static AppOptions Create(
        string localRoot,
        string? databasePath = null,
        string? secretsPath = null,
        string? displayCurrency = null,
        int? normalConcurrency = null,
        int? historyRetentionDays = null,
        IEnumerable<KeyValuePair<string, ProviderOptions>>? providers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localRoot);

        return new AppOptions(
            ResolvePath(databasePath, localRoot, "dev-status-center.db"),
            ResolvePath(secretsPath, localRoot, "secrets"),
            NormalizeCurrency(displayCurrency),

            // Más de un puñado de peticiones simultáneas no acelera nada -- los providers
            // limitan por rate limit, no por ancho de banda -- y sí multiplica los picos de CPU.
            Math.Clamp(normalConcurrency ?? 3, 1, 8),

            // Mínimo 7 días para que las anomalías tengan de dónde comparar; máximo 10 años
            // para que un cero mal tecleado no signifique "guardar para siempre".
            Math.Clamp(historyRetentionDays ?? 400, 7, 3_650),
            (providers ?? []).ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Devuelve una copia con un provider reemplazado. Las opciones son inmutables: editar es
    /// producir una instancia nueva, nunca mutar la que ya están usando el scheduler y la UI.
    /// </summary>
    public AppOptions WithProvider(string providerId, ProviderOptions provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(provider);

        var merged = Providers
            .Where(x => !string.Equals(x.Key, providerId, StringComparison.OrdinalIgnoreCase))
            .Append(KeyValuePair.Create(providerId, provider));

        return new AppOptions(
            DatabasePath,
            SecretsPath,
            DisplayCurrency,
            NormalConcurrency,
            HistoryRetentionDays,
            merged.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));
    }

    private static string ResolvePath(string? configured, string localRoot, string fallbackLeaf)
    {
        var raw = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(localRoot, fallbackLeaf)
            : configured;
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(raw));
    }

    private static string NormalizeCurrency(string? value)
    {
        var trimmed = value?.Trim();
        return trimmed is { Length: 3 } && trimmed.All(char.IsAsciiLetter)
            ? trimmed.ToUpperInvariant()
            : "USD";
    }
}
