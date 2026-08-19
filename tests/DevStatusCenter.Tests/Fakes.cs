using System.Net;
using System.Text;
using DevStatusCenter.Application.Abstractions;

namespace DevStatusCenter.Tests;

/// <summary>Secret store en memoria: las pruebas no tocan DPAPI ni el disco.</summary>
internal sealed class FakeSecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

    public static FakeSecretStore With(string reference, string secret)
    {
        var store = new FakeSecretStore();
        store._secrets[reference] = secret;
        return store;
    }

    public Task SetAsync(string credentialReference, string secret, CancellationToken cancellationToken)
    {
        _secrets[credentialReference] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string credentialReference, CancellationToken cancellationToken) =>
        Task.FromResult(_secrets.GetValueOrDefault(credentialReference));

    public Task DeleteAsync(string credentialReference, CancellationToken cancellationToken)
    {
        _secrets.Remove(credentialReference);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Handler HTTP guionizado. Cada regla se elige por una subcadena de la URL, así se prueba el
/// parsing y el mapeo del provider sin red y sin credenciales reales.
/// </summary>
internal sealed class ScriptedHttpHandler : HttpMessageHandler
{
    private readonly List<(string UrlContains, Func<int, HttpResponseMessage> Respond)> _rules = [];
    private readonly Dictionary<string, int> _hits = new(StringComparer.Ordinal);

    public List<Uri> Requests { get; } = [];

    public string? LastAuthorization { get; private set; }

    public ScriptedHttpHandler Json(string urlContains, params string[] payloadsInOrder)
    {
        _rules.Add((urlContains, hit => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                payloadsInOrder[Math.Min(hit, payloadsInOrder.Length - 1)],
                Encoding.UTF8,
                "application/json")
        }));
        return this;
    }

    public ScriptedHttpHandler Status(string urlContains, HttpStatusCode status, string body = "{}")
    {
        _rules.Add((urlContains, _ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        }));
        return this;
    }

    /// <summary>Falla las primeras <paramref name="times"/> veces y luego responde bien.</summary>
    public ScriptedHttpHandler FailThenJson(
        string urlContains,
        HttpStatusCode status,
        int times,
        string payload)
    {
        _rules.Add((urlContains, hit => hit < times
            ? new HttpResponseMessage(status) { Content = new StringContent("{}", Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            }));
        return this;
    }

    public int HitsFor(string urlContains) => _hits.GetValueOrDefault(urlContains);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        Requests.Add(request.RequestUri!);
        LastAuthorization = request.Headers.Authorization?.ToString();

        foreach (var (pattern, respond) in _rules)
        {
            if (url.Contains(pattern, StringComparison.Ordinal))
            {
                var hit = _hits.GetValueOrDefault(pattern);
                _hits[pattern] = hit + 1;
                return Task.FromResult(respond(hit));
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"sin regla para {url}", Encoding.UTF8, "text/plain")
        });
    }
}
