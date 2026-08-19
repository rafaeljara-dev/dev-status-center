using System.Windows;
using DevStatusCenter.Application.Abstractions;
using DevStatusCenter.Application.Alerts;
using DevStatusCenter.Application.Configuration;
using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Application.Forecast;
using DevStatusCenter.Application.Networking;
using DevStatusCenter.Application.Power;
using DevStatusCenter.Application.Providers;
using DevStatusCenter.Application.Scheduling;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;
using DevStatusCenter.Domain.ValueObjects;
using DevStatusCenter.Infrastructure.Configuration;
using DevStatusCenter.Infrastructure.Persistence;
using DevStatusCenter.Infrastructure.Security;
using DevStatusCenter.Infrastructure.Windows;
using DevStatusCenter.Providers.Mock;
using DevStatusCenter.Providers.Neon;
using DevStatusCenter.Desktop.Tray;
using DevStatusCenter.Desktop.ViewModels;
using DevStatusCenter.Desktop.Views;
using DevStatusCenter.Desktop.Windows;

namespace DevStatusCenter.Desktop;

public partial class App : System.Windows.Application, IDisposable
{
    private SingleInstanceGuard? _singleInstance;
    private SqliteConnectionFactory? _connectionFactory;
    private SharedHttpTransport? _httpTransport;
    private RefreshScheduler? _scheduler;
    private TrayIconService? _tray;
    private DashboardWindow? _dashboardWindow;
    private QuickAccessManagerWindow? _quickAccessManager;
    private ProviderSettingsWindow? _providerSettings;
    private bool _isDisposed;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = SingleInstanceGuard.TryAcquire();
        if (_singleInstance is null)
        {
            Shutdown();
            return;
        }

        try
        {
            var localRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DevStatusCenter");

            // La plantilla se escribe en el primer arranque para que exista un archivo real que
            // editar; a partir de ahi manda lo que haya en disco.
            AppOptionsStore.EnsureTemplate(localRoot, AppOptionsStore.Defaults(localRoot));
            var options = AppOptionsStore.Load(localRoot, out var optionsError);

            var connectionFactory = new SqliteConnectionFactory(options.DatabasePath);
            _connectionFactory = connectionFactory;
            var store = new SqliteLocalStore(
                connectionFactory,
                new SqliteMigrationRunner(connectionFactory));
            await store.InitializeAsync(CancellationToken.None);
            var settings = new SqliteSettingsStore(connectionFactory);
            var secrets = new DpapiSecretStore(options.SecretsPath);
            var initialMode = await ReadPowerModeAsync(settings);
            var powerManager = new PowerManager(initialMode);
            var launcher = new WindowsQuickAccessLauncher();
            var startupManager = new WindowsStartupManager();

            await SeedDemoReferenceDataAsync(store, options.DisplayCurrency);
            await SeedQuickAccessAsync(store);

            // Un solo SocketsHttpHandler para todos los providers: reutiliza conexiones y evita
            // el agotamiento de puertos que produce crear HttpClient por petición.
            var transport = new SharedHttpTransport();
            _httpTransport = transport;
            var http = new ResilientHttpExecutor(transport.Client, TimeProvider.System);

            var providers = await BuildProvidersAsync(options, secrets, http);
            _scheduler = new RefreshScheduler(
                providers,
                store,
                powerManager,
                TimeProvider.System,
                options.DisplayCurrency,
                options.NormalConcurrency,
                options.HistoryRetention);
            var dashboardService = new DashboardService(
                store,
                TimeProvider.System,
                options.DisplayCurrency);
            var viewModel = new DashboardViewModel(
                dashboardService,
                _scheduler,
                powerManager,
                launcher,
                settings);

            void ManageQuickAccess()
            {
                if (_quickAccessManager is { IsVisible: true })
                {
                    _quickAccessManager.Activate();
                    return;
                }

                _dashboardWindow?.SuppressAutoHide(true);
                _quickAccessManager = new QuickAccessManagerWindow(store, () => viewModel.LoadAsync());
                _quickAccessManager.Closed += (_, _) =>
                {
                    _dashboardWindow?.SuppressAutoHide(false);
                    _quickAccessManager = null;
                };
                _quickAccessManager.Show();
            }

            void ManageProviders()
            {
                if (_providerSettings is { IsVisible: true })
                {
                    _providerSettings.Activate();
                    return;
                }

                _dashboardWindow?.SuppressAutoHide(true);
                _providerSettings = new ProviderSettingsWindow(
                    options,
                    secrets,
                    localRoot,
                    updated => options = updated);
                _providerSettings.Closed += (_, _) =>
                {
                    _dashboardWindow?.SuppressAutoHide(false);
                    _providerSettings = null;
                };
                _providerSettings.Show();
            }

            _dashboardWindow = new DashboardWindow(viewModel, ManageQuickAccess);
            void ExitApplication()
            {
                _dashboardWindow.AllowClose();
                Shutdown();
            }

            _tray = new TrayIconService(
                _dashboardWindow,
                viewModel,
                powerManager,
                launcher,
                startupManager,
                ManageQuickAccess,
                ManageProviders,
                ExitApplication);

            _scheduler.SnapshotChanged += (_, _) =>
                _ = Dispatcher.InvokeAsync(() => _ = ReloadDashboardAsync(viewModel, store, _tray));
            await _scheduler.StartAsync(CancellationToken.None);
            await viewModel.LoadAsync();

            if (optionsError is not null)
            {
                viewModel.ReportConfigurationProblem(optionsError);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Dev Status Center could not start.\n\n{ex.Message}",
                "Startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _tray?.Dispose();
        if (_scheduler is not null)
        {
            _scheduler.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        _httpTransport?.Dispose();
        _connectionFactory?.Dispose();
        _singleInstance?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task<PowerMode> ReadPowerModeAsync(SqliteSettingsStore settings)
    {
        var saved = await settings.GetAsync("power.mode", CancellationToken.None);
        return Enum.TryParse<PowerMode>(saved, ignoreCase: true, out var mode)
            ? mode
            : PowerMode.Normal;
    }

    /// <summary>
    /// Instancia solo los providers habilitados en configuracion. Un provider real ademas exige
    /// que su credencial ya exista en el secret store: habilitarlo sin token dejaria al scheduler
    /// golpeando una API con 401 y marcando el provider en error en cada ciclo.
    /// </summary>
    private static async Task<IReadOnlyList<IProvider>> BuildProvidersAsync(
        AppOptions options,
        ISecretStore secrets,
        ResilientHttpExecutor http)
    {
        var providers = new List<IProvider>(options.Providers.Count);

        if (options.IsEnabled("mock"))
        {
            providers.Add(new MockProvider());
        }

        if (await HasCredentialAsync(options, secrets, NeonProvider.ProviderId))
        {
            var neon = options.For(NeonProvider.ProviderId);
            providers.Add(new NeonProvider(
                http,
                secrets,
                new NeonProviderOptions(neon.CredentialReference!, neon.AccountId),
                TimeProvider.System));
        }

        // Sin ningun provider habilitado el scheduler seguiria vivo pero nunca refrescaria, y el
        // popup se veria vacio sin explicacion. El de demostracion mantiene el pipeline visible.
        if (providers.Count == 0)
        {
            providers.Add(new MockProvider());
        }

        return providers;
    }

    private static async Task<bool> HasCredentialAsync(
        AppOptions options,
        ISecretStore secrets,
        string providerId)
    {
        var provider = options.For(providerId);
        if (!provider.Enabled || provider.CredentialReference is null)
        {
            return false;
        }

        return await secrets.GetAsync(provider.CredentialReference, CancellationToken.None) is not null;
    }

    private static Task SeedDemoReferenceDataAsync(SqliteLocalStore store, string currency)
    {
        Budget[] budgets = [new("budget:monthly", "Monthly total", new Money(200m, currency))];
        return store.SeedReferenceDataAsync(budgets, [], [], CancellationToken.None);
    }

    private static async Task ReloadDashboardAsync(
        DashboardViewModel viewModel,
        ILocalStore store,
        INotifier? notifier)
    {
        try
        {
            await viewModel.LoadAsync();
            await RaiseAlertsAsync(viewModel.Snapshot, store, notifier);
        }
        catch
        {
            // Provider state remains available in SQLite; the next UI load can recover.
        }
    }

    /// <summary>
    /// Evalúa las reglas contra el estado ya calculado y notifica sólo lo que el enfriamiento
    /// deja pasar. Va después de LoadAsync a propósito: la UI se actualiza primero y las alertas
    /// no pueden retrasar lo que el usuario ve.
    /// </summary>
    private static async Task RaiseAlertsAsync(
        DashboardSnapshot? snapshot,
        ILocalStore store,
        INotifier? notifier)
    {
        if (snapshot is null || notifier is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var candidates = AlertEvaluator.Evaluate(snapshot, now);
        if (candidates.Count == 0)
        {
            return;
        }

        var due = await store.RecordAndFilterAlertsAsync(
            candidates,
            AlertEvaluator.DefaultCooldown,
            now,
            CancellationToken.None);

        foreach (var alert in due)
        {
            notifier.Notify(alert);
        }
    }

    private static async Task SeedQuickAccessAsync(SqliteLocalStore store)
    {
        if ((await store.ReadQuickAccessAsync(CancellationToken.None)).Count > 0)
        {
            return;
        }

        var repositories = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "source",
            "repos");
        if (!Directory.Exists(repositories))
        {
            return;
        }

        var group = new QuickAccessEntry(
            "quick:projects",
            "Projects",
            QuickAccessKind.Group,
            path: null);
        await store.UpsertQuickAccessAsync(group, CancellationToken.None);
        await store.UpsertQuickAccessAsync(new QuickAccessEntry(
            "quick:repositories",
            "Repositories",
            QuickAccessKind.Folder,
            repositories,
            group.Id,
            QuickAccessAction.Explorer), CancellationToken.None);
    }
}
