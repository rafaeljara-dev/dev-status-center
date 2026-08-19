using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using DevStatusCenter.Application.Abstractions;

namespace DevStatusCenter.Infrastructure.Security;

[SupportedOSPlatform("windows")]
public sealed class DpapiSecretStore : ISecretStore
{
    private readonly string _rootDirectory;

    public DpapiSecretStore(string rootDirectory)
    {
        _rootDirectory = Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(rootDirectory));
        Directory.CreateDirectory(_rootDirectory);
    }

    public async Task SetAsync(
        string credentialReference,
        string secret,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        var plainBytes = Encoding.UTF8.GetBytes(secret);
        byte[]? protectedBytes = null;
        try
        {
            protectedBytes = ProtectedData.Protect(
                plainBytes,
                Entropy(credentialReference),
                DataProtectionScope.CurrentUser);
            var destination = PathFor(credentialReference);
            var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            await File.WriteAllBytesAsync(temporary, protectedBytes, cancellationToken);
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plainBytes);
            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
    }

    public async Task<string?> GetAsync(
        string credentialReference,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialReference);
        var path = PathFor(credentialReference);
        if (!File.Exists(path))
        {
            return null;
        }

        var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
        byte[]? plainBytes = null;
        try
        {
            plainBytes = ProtectedData.Unprotect(
                protectedBytes,
                Entropy(credentialReference),
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
            if (plainBytes is not null)
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }
    }

    public Task DeleteAsync(
        string credentialReference,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialReference);
        cancellationToken.ThrowIfCancellationRequested();
        var path = PathFor(credentialReference);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string PathFor(string credentialReference)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(credentialReference));
        return Path.Combine(_rootDirectory, Convert.ToHexStringLower(hash) + ".secret");
    }

    private static byte[] Entropy(string credentialReference) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"DevStatusCenter/v1/{credentialReference}"));
}

