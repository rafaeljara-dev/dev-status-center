namespace DevStatusCenter.Application.Abstractions;

public interface ISecretStore
{
    Task SetAsync(string credentialReference, string secret, CancellationToken cancellationToken);

    Task<string?> GetAsync(string credentialReference, CancellationToken cancellationToken);

    Task DeleteAsync(string credentialReference, CancellationToken cancellationToken);
}

