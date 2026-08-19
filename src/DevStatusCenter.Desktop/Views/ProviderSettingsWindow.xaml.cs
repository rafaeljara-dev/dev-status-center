using System.Windows;
using System.Windows.Controls;
using DevStatusCenter.Application.Abstractions;
using DevStatusCenter.Application.Configuration;
using DevStatusCenter.Infrastructure.Configuration;

namespace DevStatusCenter.Desktop.Views;

/// <summary>Fila de la lista de providers. Nunca contiene el secreto, sólo si existe.</summary>
internal sealed class ProviderRow(string id, ProviderOptions options, bool hasCredential)
{
    public string Id { get; } = id;

    public ProviderOptions Options { get; } = options;

    public bool HasCredential { get; } = hasCredential;

    public string DisplayName => Id;

    public string StatusLine
    {
        get
        {
            var refreshing = Options.Enabled ? "activo" : "en pausa";
            var credential = Options.CredentialReference is null
                ? "sin credencial requerida"
                : HasCredential ? "token guardado" : "falta token";
            return $"{refreshing} · {credential}";
        }
    }
}

/// <summary>
/// Punto único donde se introducen las credenciales. La ventana nunca lee un secreto de vuelta:
/// sólo pregunta si existe. Es la contraparte de <see cref="ISecretStore"/> en la UI.
/// </summary>
public partial class ProviderSettingsWindow : Window
{
    private readonly ISecretStore _secrets;
    private readonly string _localRoot;
    private readonly Action<AppOptions> _onSaved;
    private AppOptions _options;
    private ProviderRow? _selected;

    public ProviderSettingsWindow(
        AppOptions options,
        ISecretStore secrets,
        string localRoot,
        Action<AppOptions> onSaved)
    {
        InitializeComponent();
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _secrets = secrets;
        _localRoot = localRoot;
        _onSaved = onSaved;
        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync(string? keepSelectedId = null)
    {
        var rows = new List<ProviderRow>(_options.Providers.Count);
        foreach (var entry in _options.Providers.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var hasCredential = entry.Value.CredentialReference is { } reference &&
                await _secrets.GetAsync(reference, CancellationToken.None) is not null;
            rows.Add(new ProviderRow(entry.Key, entry.Value, hasCredential));
        }

        ProvidersList.ItemsSource = rows;
        ProvidersList.SelectedItem = rows.FirstOrDefault(x =>
            string.Equals(x.Id, keepSelectedId ?? _selected?.Id, StringComparison.OrdinalIgnoreCase))
            ?? rows.FirstOrDefault();
    }

    private void ProvidersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = ProvidersList.SelectedItem as ProviderRow;
        if (_selected is null)
        {
            return;
        }

        EnabledBox.IsChecked = _selected.Options.Enabled;
        ReferenceBox.Text = _selected.Options.CredentialReference ?? string.Empty;
        AccountBox.Text = _selected.Options.AccountId ?? string.Empty;
        TokenBox.Clear();
    }

    private async void SaveToken_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            return;
        }

        try
        {
            var reference = ReferenceBox.Text.Trim();
            if (reference.Length == 0)
            {
                MessageText.Text = "Define primero una credential reference para este provider.";
                return;
            }

            var token = TokenBox.Password;
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageText.Text = "Pega el token antes de guardar.";
                return;
            }

            await _secrets.SetAsync(reference, token, CancellationToken.None);
            TokenBox.Clear();
            ApplyFormToOptions();
            Persist();
            await ReloadAsync(_selected.Id);
            MessageText.Text = $"Token de '{reference}' guardado y cifrado con DPAPI. " +
                               "Reinicia Dev Status Center para que el provider entre al ciclo de refresh.";
        }
        catch (Exception ex)
        {
            MessageText.Text = ex.Message;
        }
    }

    private async void DeleteToken_Click(object sender, RoutedEventArgs e)
    {
        if (_selected?.Options.CredentialReference is not { } reference)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            $"¿Borrar el token guardado para '{reference}'?\n\n" +
            "Esto sólo lo elimina de este equipo. Revoca el token en el proveedor por separado.",
            "Borrar credencial",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _secrets.DeleteAsync(reference, CancellationToken.None);
            await ReloadAsync(_selected.Id);
            MessageText.Text = $"Token de '{reference}' eliminado de este equipo.";
        }
        catch (Exception ex)
        {
            MessageText.Text = ex.Message;
        }
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            return;
        }

        try
        {
            ApplyFormToOptions();
            Persist();
            await ReloadAsync(_selected.Id);
            MessageText.Text = "Ajustes guardados. Los cambios de provider se aplican al reiniciar.";
        }
        catch (Exception ex)
        {
            MessageText.Text = ex.Message;
        }
    }

    private void ApplyFormToOptions()
    {
        if (_selected is null)
        {
            return;
        }

        _options = _options.WithProvider(_selected.Id, new ProviderOptions(
            EnabledBox.IsChecked == true,
            NullIfBlank(ReferenceBox.Text),
            NullIfBlank(AccountBox.Text)));
    }

    private void Persist()
    {
        AppOptionsStore.Save(_localRoot, _options);
        _onSaved(_options);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
