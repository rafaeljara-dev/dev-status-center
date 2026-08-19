using System.Windows;
using System.Windows.Controls;
using DevStatusCenter.Application.Abstractions;
using DevStatusCenter.Domain.Models;
using Forms = System.Windows.Forms;

namespace DevStatusCenter.Desktop.Views;

public partial class QuickAccessManagerWindow : Window
{
    private readonly ILocalStore _store;
    private readonly Func<Task> _afterChange;
    private IReadOnlyList<QuickAccessEntry> _entries = [];
    private QuickAccessEntry? _selected;

    public QuickAccessManagerWindow(ILocalStore store, Func<Task> afterChange)
    {
        InitializeComponent();
        _store = store;
        _afterChange = afterChange;
        KindBox.ItemsSource = Enum.GetValues<QuickAccessKind>();
        ActionBox.ItemsSource = Enum.GetValues<QuickAccessAction>();
        KindBox.SelectedItem = QuickAccessKind.Project;
        ActionBox.SelectedItem = QuickAccessAction.Editor;
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _entries = await _store.ReadQuickAccessAsync(CancellationToken.None);
        EntriesList.ItemsSource = _entries;
        RefreshParents();
    }

    private void RefreshParents()
    {
        ParentBox.ItemsSource = _entries
            .Where(x => x.Kind == QuickAccessKind.Group && x.Id != _selected?.Id)
            .OrderBy(x => x.DisplayName)
            .ToArray();
    }

    private void EntriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = EntriesList.SelectedItem as QuickAccessEntry;
        if (_selected is null)
        {
            return;
        }

        NameBox.Text = _selected.DisplayName;
        KindBox.SelectedItem = _selected.Kind;
        RefreshParents();
        ParentBox.SelectedValue = _selected.ParentId;
        PathBox.Text = _selected.Path ?? string.Empty;
        ActionBox.SelectedItem = _selected.DefaultAction;
    }

    private void KindBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var isGroup = KindBox.SelectedItem is QuickAccessKind.Group;
        PathBox.IsEnabled = !isGroup;
        BrowseButton.IsEnabled = !isGroup;
        ActionBox.IsEnabled = !isGroup;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose the folder or project to pin",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            PathBox.Text = dialog.SelectedPath;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NameBox.Text = Path.GetFileName(dialog.SelectedPath);
            }
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var kind = KindBox.SelectedItem is QuickAccessKind selectedKind
                ? selectedKind
                : QuickAccessKind.Project;
            var action = ActionBox.SelectedItem is QuickAccessAction selectedAction
                ? selectedAction
                : QuickAccessAction.Explorer;
            var parentId = ParentBox.SelectedValue as string;
            if (_selected is not null && parentId is not null && IsDescendant(parentId, _selected.Id))
            {
                throw new InvalidOperationException("A group cannot be moved inside one of its descendants.");
            }

            var entry = new QuickAccessEntry(
                _selected?.Id ?? $"quick:{Guid.NewGuid():N}",
                NameBox.Text,
                kind,
                kind == QuickAccessKind.Group ? null : PathBox.Text,
                parentId,
                action,
                _selected?.SortOrder ?? _entries.Count,
                isPinned: true);
            await _store.UpsertQuickAccessAsync(entry, CancellationToken.None);
            await ReloadAsync();
            await _afterChange();
            MessageText.Text = "Saved. Changes are visible in the tray and dashboard.";
            _selected = entry;
        }
        catch (Exception ex)
        {
            MessageText.Text = ex.Message;
        }
    }

    private bool IsDescendant(string candidateId, string ancestorId)
    {
        var byId = _entries.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var current = candidateId;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (visited.Add(current) && byId.TryGetValue(current, out var entry))
        {
            if (entry.Id == ancestorId)
            {
                return true;
            }

            if (entry.ParentId is null)
            {
                return false;
            }

            current = entry.ParentId;
        }

        return false;
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _selected = null;
        EntriesList.SelectedItem = null;
        NameBox.Clear();
        PathBox.Clear();
        ParentBox.SelectedItem = null;
        KindBox.SelectedItem = QuickAccessKind.Project;
        ActionBox.SelectedItem = QuickAccessAction.Editor;
        MessageText.Text = string.Empty;
        RefreshParents();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"Delete '{_selected.DisplayName}'? Nested items will also be removed.",
            "Delete quick access",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        await _store.DeleteQuickAccessAsync(_selected.Id, CancellationToken.None);
        New_Click(sender, e);
        await ReloadAsync();
        await _afterChange();
    }
}
