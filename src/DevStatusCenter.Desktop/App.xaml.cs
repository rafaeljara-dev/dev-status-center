using System.Windows;
using DevStatusCenter.Application.Abstractions;
using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Application.Forecast;
using DevStatusCenter.Application.Power;
using DevStatusCenter.Application.Providers;
using DevStatusCenter.Application.Scheduling;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;
using DevStatusCenter.Domain.ValueObjects;
using DevStatusCenter.Infrastructure.Persistence;
using DevStatusCenter.Infrastructure.Windows;
using DevStatusCenter.Providers.Mock;
using DevStatusCenter.Desktop.Tray;
using DevStatusCenter.Desktop.ViewModels;
using DevStatusCenter.Desktop.Views;
using DevStatusCenter.Desktop.Windows;

namespace DevStatusCenter.Desktop;

public partial class App : System.Windows.Application, IDisposable
{
    private SingleInstanceGuard? _singleInstance;
    private SqliteConnectionFactory? _connectionFactory;
    private RefreshScheduler? _scheduler;
    private TrayIconService? _tray;
    private DashboardWindow? _dashboardWindow;
    private QuickAccessManagerWindow? _quickAccessManager;
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
            var connectionFactory = new SqliteConnectionFactory(Path.Combine(localRoot, "dev-status-center.db"));
            _connectionFactory = connectionFactory;
            var store = new SqliteLocalStore(
                connectionFactory,
                new SqliteMigrationRunner(connectionFactory));
            await store.InitializeAsync(CancellationToken.None);
            var settings = new SqliteSettingsStore(connectionFactory);
            var initialMode = await ReadPowerModeAsync(settings);
            var powerManager = new PowerManager(initialMode);
            var launcher = new WindowsQuickAccessLauncher();
            var startupManager = new WindowsStartupManager();

            await SeedDemoReferenceDataAsync(store);
            await SeedQuickAccessAsync(store);

            IReadOnlyList<IProvider> providers = [new MockProvider()];
            _scheduler = new RefreshScheduler(
                providers,
                store,
                powerManager,
                TimeProvider.System,
                displayCurrency: "USD",
                maximumConcurrency: 3);
            var dashboardService = new DashboardService(
                store,
                TimeProvider.System,
                displayCurrency: "USD");
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
                ExitApplication);

            _scheduler.SnapshotChanged += (_, _) =>
                _ = Dispatcher.InvokeAsync(() => _ = ReloadDashboardAsync(viewModel));
            await _scheduler.StartAsync(CancellationToken.None);
            await viewModel.LoadAsync();
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

    private static Task SeedDemoReferenceDataAsync(SqliteLocalStore store)
    {
        Budget[] budgets = [new("budget:monthly", "Monthly total", new Money(200m, "USD"))];
        return store.SeedReferenceDataAsync(budgets, [], [], CancellationToken.None);
    }

    private static async Task ReloadDashboardAsync(DashboardViewModel viewModel)
    {
        try
        {
            await viewModel.LoadAsync();
        }
        catch
        {
            // Provider state remains available in SQLite; the next UI load can recover.
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
