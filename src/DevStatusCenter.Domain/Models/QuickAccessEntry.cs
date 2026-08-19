using DevStatusCenter.Domain.Common;

namespace DevStatusCenter.Domain.Models;

public enum QuickAccessKind
{
    Group,
    Folder,
    Project
}

public enum QuickAccessAction
{
    Explorer,
    Terminal,
    Editor
}

public sealed record QuickAccessEntry
{
    public QuickAccessEntry(
        string id,
        string displayName,
        QuickAccessKind kind,
        string? path,
        string? parentId = null,
        QuickAccessAction defaultAction = QuickAccessAction.Explorer,
        int sortOrder = 0,
        bool isPinned = true)
    {
        Id = Guard.NotBlank(id, nameof(id));
        DisplayName = Guard.NotBlank(displayName, nameof(displayName));
        Kind = kind;
        ParentId = parentId?.Trim();
        DefaultAction = defaultAction;
        SortOrder = sortOrder;
        IsPinned = isPinned;

        if (kind == QuickAccessKind.Group)
        {
            Path = null;
        }
        else
        {
            Path = Guard.NotBlank(path, nameof(path));
        }
    }

    public string Id { get; }

    public string DisplayName { get; }

    public QuickAccessKind Kind { get; }

    public string? Path { get; }

    public string? ParentId { get; }

    public QuickAccessAction DefaultAction { get; }

    public int SortOrder { get; }

    public bool IsPinned { get; }
}

