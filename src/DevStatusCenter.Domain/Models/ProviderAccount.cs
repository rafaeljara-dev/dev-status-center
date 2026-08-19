using DevStatusCenter.Domain.Common;

namespace DevStatusCenter.Domain.Models;

public sealed record ProviderAccount
{
    public ProviderAccount(
        string id,
        string providerId,
        string displayName,
        string? externalAccountId,
        string? credentialReference,
        bool isEnabled = true)
    {
        Id = Guard.NotBlank(id, nameof(id));
        ProviderId = Guard.NotBlank(providerId, nameof(providerId));
        DisplayName = Guard.NotBlank(displayName, nameof(displayName));
        ExternalAccountId = externalAccountId?.Trim();
        CredentialReference = credentialReference?.Trim();
        IsEnabled = isEnabled;
    }

    public string Id { get; }

    public string ProviderId { get; }

    public string DisplayName { get; }

    public string? ExternalAccountId { get; }

    public string? CredentialReference { get; }

    public bool IsEnabled { get; }
}

