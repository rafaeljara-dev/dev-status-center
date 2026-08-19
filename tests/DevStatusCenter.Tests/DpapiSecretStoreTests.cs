using System.Text;
using DevStatusCenter.Infrastructure.Security;

namespace DevStatusCenter.Tests;

public sealed class DpapiSecretStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "dev-status-center-secrets",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task SetAndGet_RoundTripsTheSecret()
    {
        var store = new DpapiSecretStore(_root);

        await store.SetAsync("neon-personal", "napi_token_value", CancellationToken.None);

        Assert.Equal("napi_token_value", await store.GetAsync("neon-personal", CancellationToken.None));
    }

    [Fact]
    public async Task Get_ReturnsNullForAnUnknownReference()
    {
        var store = new DpapiSecretStore(_root);

        Assert.Null(await store.GetAsync("never-stored", CancellationToken.None));
    }

    [Fact]
    public async Task Set_OverwritesAPreviousValue()
    {
        var store = new DpapiSecretStore(_root);

        await store.SetAsync("vercel-personal", "first", CancellationToken.None);
        await store.SetAsync("vercel-personal", "second", CancellationToken.None);

        Assert.Equal("second", await store.GetAsync("vercel-personal", CancellationToken.None));
    }

    [Fact]
    public async Task Delete_RemovesTheSecretAndIsIdempotent()
    {
        var store = new DpapiSecretStore(_root);
        await store.SetAsync("cloudflare-personal", "token", CancellationToken.None);

        await store.DeleteAsync("cloudflare-personal", CancellationToken.None);
        await store.DeleteAsync("cloudflare-personal", CancellationToken.None);

        Assert.Null(await store.GetAsync("cloudflare-personal", CancellationToken.None));
    }

    [Fact]
    public async Task StoredFile_NeverContainsThePlaintextOrTheReference()
    {
        const string Secret = "super-secret-token-value";
        var store = new DpapiSecretStore(_root);

        await store.SetAsync("neon-personal", Secret, CancellationToken.None);

        var files = Directory.GetFiles(_root);
        var file = Assert.Single(files);

        // El nombre es un hash: la carpeta no revela qué providers están configurados.
        Assert.DoesNotContain("neon", Path.GetFileName(file), StringComparison.OrdinalIgnoreCase);

        // Y el contenido está cifrado, no ofuscado.
        var bytes = await File.ReadAllBytesAsync(file, CancellationToken.None);
        Assert.DoesNotContain(Secret, Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_FailsWhenTheEntropyDoesNotMatchTheReference()
    {
        var store = new DpapiSecretStore(_root);
        await store.SetAsync("neon-personal", "token", CancellationToken.None);

        // Renombrar el archivo al hash de otra referencia no permite descifrarlo: la entropía
        // va ligada al nombre lógico, así que mover archivos entre providers no funciona.
        var original = Directory.GetFiles(_root).Single();
        var decoy = new DpapiSecretStore(_root);
        await decoy.SetAsync("vercel-personal", "otro", CancellationToken.None);
        var target = Directory.GetFiles(_root).First(x => x != original);

        File.Copy(original, target, overwrite: true);

        await Assert.ThrowsAnyAsync<System.Security.Cryptography.CryptographicException>(
            () => decoy.GetAsync("vercel-personal", CancellationToken.None));
    }
}
