using DevStatusCenter.Domain.Common;
using DevStatusCenter.Domain.Enums;

namespace DevStatusCenter.Domain.Models;

public sealed record Service
{
    public Service(
        string id,
        string providerId,
        string providerAccountId,
        string externalId,
        string name,
        ServiceCategory category,
        CostBehavior costBehavior,
        bool isEnabled = true)
    {
        Id = Guard.NotBlank(id, nameof(id));
        ProviderId = Guard.NotBlank(providerId, nameof(providerId));
        ProviderAccountId = Guard.NotBlank(providerAccountId, nameof(providerAccountId));
        ExternalId = Guard.NotBlank(externalId, nameof(externalId));
        Name = Guard.NotBlank(name, nameof(name));
        Category = category;
        CostBehavior = costBehavior;
        IsEnabled = isEnabled;
    }

    public string Id { get; }

    public string ProviderId { get; }

    public string ProviderAccountId { get; }

    public string ExternalId { get; }

    public string Name { get; }

    public ServiceCategory Category { get; }

    public CostBehavior CostBehavior { get; }

    public bool IsEnabled { get; }
}

