using System.Collections.Frozen;

namespace DevStatusCenter.Application.Health;

/// <summary>Formato de la pagina de estado. Determina como se lee la respuesta, no que se lee.</summary>
public enum HealthFeedKind
{
    /// <summary>Atlassian Statuspage, <c>/api/v2/summary.json</c>. El formato mas extendido.</summary>
    Statuspage,

    /// <summary>Instatus, <c>/summary.json</c>. Devuelve un solo estado por pagina.</summary>
    Instatus,

    /// <summary>Google, <c>incidents.json</c>: una lista de incidentes; abiertos son los que no tienen fin.</summary>
    GoogleIncidents
}

public sealed record HealthTarget(
    string Key,
    string DisplayName,
    HealthFeedKind Kind,
    string ApiUrl,
    string PageUrl,
    bool EnabledByDefault);

/// <summary>
/// Las paginas de estado de los servicios que se usan en este equipo.
///
/// Cada URL de esta lista se comprobo devolviendo JSON de verdad antes de escribirla; ninguna esta
/// aqui de memoria. Lo que no aparece es porque no publica un feed legible: <b>Railway</b> sirve su
/// estado como HTML renderizado en el cliente, y sacarlo exigiria raspar la pagina, que se rompe en
/// cuanto cambien el marcado y mentiria en silencio mientras tanto.
///
/// Solo un puñado viene activado: comprobar dieciseis paginas cada pocos minutos para mirar dos es
/// gasto de red sin nada a cambio. El resto se enciende desde <c>appsettings.json</c>.
/// </summary>
public static class HealthCatalog
{
    public static readonly FrozenDictionary<string, HealthTarget> All = new HealthTarget[]
    {
        // Statuspage. La ruta es siempre {pagina}/api/v2/summary.json.
        new("github", "GitHub", HealthFeedKind.Statuspage,
            "https://www.githubstatus.com/api/v2/summary.json", "https://www.githubstatus.com", true),
        new("vercel", "Vercel", HealthFeedKind.Statuspage,
            "https://www.vercel-status.com/api/v2/summary.json", "https://www.vercel-status.com", true),
        new("cloudflare", "Cloudflare", HealthFeedKind.Statuspage,
            "https://www.cloudflarestatus.com/api/v2/summary.json", "https://www.cloudflarestatus.com", true),

        // status.anthropic.com redirige aqui con un 301; se apunta al destino para ahorrar el salto.
        new("anthropic", "Claude", HealthFeedKind.Statuspage,
            "https://status.claude.com/api/v2/summary.json", "https://status.claude.com", true),
        new("openai", "OpenAI", HealthFeedKind.Statuspage,
            "https://status.openai.com/api/v2/summary.json", "https://status.openai.com", true),
        new("npm", "npm", HealthFeedKind.Statuspage,
            "https://status.npmjs.org/api/v2/summary.json", "https://status.npmjs.org", true),
        new("supabase", "Supabase", HealthFeedKind.Statuspage,
            "https://status.supabase.com/api/v2/summary.json", "https://status.supabase.com", false),
        new("clerk", "Clerk", HealthFeedKind.Statuspage,
            "https://status.clerk.com/api/v2/summary.json", "https://status.clerk.com", false),
        new("sanity", "Sanity", HealthFeedKind.Statuspage,
            "https://www.sanity-status.com/api/v2/summary.json", "https://www.sanity-status.com", false),
        new("elevenlabs", "ElevenLabs", HealthFeedKind.Statuspage,
            "https://status.elevenlabs.io/api/v2/summary.json", "https://status.elevenlabs.io", false),
        new("mercadopago", "Mercado Pago", HealthFeedKind.Statuspage,
            "https://status.mercadopago.com/api/v2/summary.json", "https://status.mercadopago.com", false),
        new("alpaca", "Alpaca", HealthFeedKind.Statuspage,
            "https://status.alpaca.markets/api/v2/summary.json", "https://status.alpaca.markets", false),

        // Neon no sirve Statuspage: su pagina publica es Instatus, con otro formato y otra ruta.
        new("neon", "Neon", HealthFeedKind.Instatus,
            "https://neon.instatus.com/summary.json", "https://neonstatus.com", true),

        // Google publica incidentes, no un indicador global: el estado se deduce de los abiertos.
        new("firebase", "Firebase", HealthFeedKind.GoogleIncidents,
            "https://status.firebase.google.com/incidents.json", "https://status.firebase.google.com", false),
        new("gcp", "Google Cloud", HealthFeedKind.GoogleIncidents,
            "https://status.cloud.google.com/incidents.json", "https://status.cloud.google.com", false),
    }.ToFrozenDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<HealthTarget> Defaults =>
        [.. All.Values.Where(x => x.EnabledByDefault).OrderBy(x => x.DisplayName, StringComparer.Ordinal)];

    /// <summary>
    /// Resuelve las claves configuradas. Una clave desconocida se ignora en vez de reventar el
    /// arranque: un error tipografico en el archivo de opciones no puede dejar la app sin abrir.
    /// </summary>
    public static IReadOnlyList<HealthTarget> Resolve(IReadOnlyCollection<string>? keys)
    {
        if (keys is null || keys.Count == 0)
        {
            return Defaults;
        }

        return
        [
            .. keys
                .Select(key => All.GetValueOrDefault(key.Trim()))
                .Where(target => target is not null)
                .Select(target => target!)
                .DistinctBy(target => target.Key, StringComparer.OrdinalIgnoreCase)
                .OrderBy(target => target.DisplayName, StringComparer.Ordinal)
        ];
    }
}
