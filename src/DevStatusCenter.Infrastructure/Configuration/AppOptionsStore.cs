using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevStatusCenter.Application.Configuration;
using DevStatusCenter.Application.Health;

namespace DevStatusCenter.Infrastructure.Configuration;

/// <summary>
/// Forma en disco de <c>appsettings.json</c>. Es un DTO deliberadamente laxo: todo es anulable
/// para que un archivo escrito a mano y a medio llenar siga siendo legible, y la validación
/// ocurra una sola vez en <see cref="AppOptions.Create"/>.
/// </summary>
internal sealed class AppOptionsFile
{
    public string? DatabasePath { get; set; }

    public string? SecretsPath { get; set; }

    public string? DisplayCurrency { get; set; }

    public int? NormalConcurrency { get; set; }

    public int? HistoryRetentionDays { get; set; }

    [SuppressMessage(
        "Usage",
        "CA2227:Collection properties should be read only",
        Justification = "DTO de deserialización: System.Text.Json necesita poder asignar el diccionario. " +
                        "No se expone fuera de este ensamblado; el modelo público es AppOptions, inmutable.")]
    /// <summary>Claves de servicios cuya pagina de estado se vigila. Nulo o vacio = las de fabrica.</summary>
    public List<string>? HealthServices { get; set; }

    public Dictionary<string, ProviderOptionsFile>? Providers { get; set; }
}

internal sealed class ProviderOptionsFile
{
    public bool Enabled { get; set; }

    /// <summary>Referencia lógica, no el token. El secreto vive en DPAPI (FR-025, FR-063).</summary>
    public string? CredentialReference { get; set; }

    public string? AccountId { get; set; }
}

/// <summary>
/// Contexto de serialización generado en compilación: evita el arranque en frío del serializador
/// basado en reflexión y deja el ensamblado apto para trimming.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true)]
[JsonSerializable(typeof(AppOptionsFile))]
internal sealed partial class AppOptionsJsonContext : JsonSerializerContext;

/// <summary>
/// Carga las opciones desde <c>%LOCALAPPDATA%\DevStatusCenter\appsettings.json</c> y, si no
/// existe, escribe una plantilla con los valores por defecto para que haya algo que editar.
/// </summary>
public static class AppOptionsStore
{
    public static string PathFor(string localRoot) => Path.Combine(localRoot, AppOptions.FileName);

    /// <summary>
    /// Nunca lanza por un archivo ilegible. Un JSON roto degrada a los valores por defecto y se
    /// informa en <paramref name="loadError"/>, para que la UI lo muestre sin impedir el arranque.
    /// </summary>
    public static AppOptions Load(string localRoot, out string? loadError)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localRoot);
        loadError = null;

        var path = PathFor(localRoot);
        AppOptionsFile? file = null;

        if (File.Exists(path))
        {
            try
            {
                using var stream = File.OpenRead(path);
                file = JsonSerializer.Deserialize(stream, AppOptionsJsonContext.Default.AppOptionsFile);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                loadError = $"{AppOptions.FileName} no se pudo leer ({ex.Message}). Se usaron los valores por defecto.";
            }
        }

        return Materialize(localRoot, file);
    }

    /// <summary>
    /// Escribe la plantilla si todavía no existe. Devuelve <c>true</c> si la creó.
    /// Escritura atómica: primero un temporal, luego un <c>File.Move</c>.
    /// </summary>
    public static bool EnsureTemplate(string localRoot, AppOptions defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        var path = PathFor(localRoot);
        if (File.Exists(path))
        {
            return false;
        }

        Directory.CreateDirectory(localRoot);
        var file = new AppOptionsFile
        {
            DatabasePath = defaults.DatabasePath,
            SecretsPath = defaults.SecretsPath,
            DisplayCurrency = defaults.DisplayCurrency,
            NormalConcurrency = defaults.NormalConcurrency,
            HistoryRetentionDays = defaults.HistoryRetentionDays,
            HealthServices = [.. defaults.HealthServices],
            Providers = defaults.Providers.ToDictionary(
                x => x.Key,
                x => new ProviderOptionsFile
                {
                    Enabled = x.Value.Enabled,
                    CredentialReference = x.Value.CredentialReference,
                    AccountId = x.Value.AccountId
                },
                StringComparer.OrdinalIgnoreCase)
        };

        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        using (var stream = File.Create(temporary))
        {
            JsonSerializer.Serialize(stream, file, AppOptionsJsonContext.Default.AppOptionsFile);
        }

        File.Move(temporary, path, overwrite: true);
        return true;
    }

    /// <summary>
    /// Persiste las opciones. Escritura atómica: si el proceso muere a media escritura, el
    /// archivo anterior sigue intacto en vez de quedar truncado.
    /// </summary>
    public static void Save(string localRoot, AppOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localRoot);
        ArgumentNullException.ThrowIfNull(options);

        Directory.CreateDirectory(localRoot);
        var file = new AppOptionsFile
        {
            DatabasePath = options.DatabasePath,
            SecretsPath = options.SecretsPath,
            DisplayCurrency = options.DisplayCurrency,
            NormalConcurrency = options.NormalConcurrency,
            HistoryRetentionDays = options.HistoryRetentionDays,
            HealthServices = [.. options.HealthServices],
            Providers = options.Providers.ToDictionary(
                x => x.Key,
                x => new ProviderOptionsFile
                {
                    Enabled = x.Value.Enabled,
                    CredentialReference = x.Value.CredentialReference,
                    AccountId = x.Value.AccountId
                },
                StringComparer.OrdinalIgnoreCase)
        };

        var path = PathFor(localRoot);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        using (var stream = File.Create(temporary))
        {
            JsonSerializer.Serialize(stream, file, AppOptionsJsonContext.Default.AppOptionsFile);
        }

        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>
    /// Valores por defecto de fábrica: sólo el provider de demostración activo. Los reales se
    /// habilitan cuando existe una credencial guardada.
    /// </summary>
    public static AppOptions Defaults(string localRoot) => AppOptions.Create(
        localRoot,
        healthServices: HealthCatalog.Defaults.Select(x => x.Key),
        providers: BuildDefaultProviders());

    private static AppOptions Materialize(string localRoot, AppOptionsFile? file)
    {
        if (file is null)
        {
            return Defaults(localRoot);
        }

        var providers = file.Providers is null or { Count: 0 }
            ? BuildDefaultProviders()
            : file.Providers.Select(entry => KeyValuePair.Create(
                entry.Key,
                new ProviderOptions(
                    entry.Value.Enabled,
                    NullIfBlank(entry.Value.CredentialReference),
                    NullIfBlank(entry.Value.AccountId))));

        return AppOptions.Create(
            localRoot,
            file.DatabasePath,
            file.SecretsPath,
            file.DisplayCurrency,
            file.NormalConcurrency,
            file.HistoryRetentionDays,
            file.HealthServices,
            providers);
    }

    private static KeyValuePair<string, ProviderOptions>[] BuildDefaultProviders() =>
    [
        KeyValuePair.Create("mock", new ProviderOptions(true, null, "mock-personal")),
        KeyValuePair.Create("neon", new ProviderOptions(false, "neon-personal", null)),
        KeyValuePair.Create("vercel", new ProviderOptions(false, "vercel-personal", null)),
        KeyValuePair.Create("cloudflare", new ProviderOptions(false, "cloudflare-personal", null))
    ];

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
