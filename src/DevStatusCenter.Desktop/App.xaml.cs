using System.Windows;
using System.Windows.Threading;
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
using DevStatusCenter.Desktop.Diagnostics;
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

    /// <summary>Tope para detener el scheduler al salir. Salir nunca puede quedarse colgado.</summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);
    private BindingErrorListener? _bindingErrors;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        HookFailureLogging();

        // --selftest: arranca, renderiza el popup, verifica que ningun binding falle y sale.
        // Es lo que convierte "esperemos que la ventana abra" en un hecho comprobado, tanto en
        // CI como antes de entregar una build.
        var selfTest = e.Args.Contains("--selftest", StringComparer.OrdinalIgnoreCase);
        if (selfTest)
        {
            _bindingErrors = BindingErrorListener.Attach();
        }

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
            Directory.CreateDirectory(localRoot);
            CrashLog.Initialize(localRoot);

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

            // El cristal se recuerda entre sesiones, igual que el modo de energia: quien lo apaga
            // porque le estorba no deberia volver a apagarlo en el siguiente arranque.
            var glassEnabled = await ReadGlassPreferenceAsync(settings);
            _dashboardWindow.SetGlass(glassEnabled);

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
                glassEnabled,
                enabled =>
                {
                    _dashboardWindow.SetGlass(enabled);
                    _ = settings.SetAsync(
                        "ui.glass",
                        enabled ? "true" : "false",
                        CancellationToken.None);
                },
                ExitApplication);

            _scheduler.SnapshotChanged += (_, _) =>
                _ = Dispatcher.InvokeAsync(() => _ = ReloadDashboardAsync(viewModel, store, _tray));
            await _scheduler.StartAsync(CancellationToken.None);

            // La pestana recordada se restaura antes del primer LoadAsync: si el snapshot trae una
            // cuota disparada, LoadAsync la sobreescribe a proposito y abre en esa categoria.
            await viewModel.RestoreTabAsync(CancellationToken.None);
            await viewModel.LoadAsync();

            if (optionsError is not null)
            {
                viewModel.ReportConfigurationProblem(optionsError);
            }

            if (selfTest)
            {
                await RunSelfTestAsync();
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

    /// <summary>
    /// Renderiza el popup de verdad -- construirlo no ejercita los bindings, mostrarlo si -- y sale
    /// con codigo distinto de cero si WPF reporto algun binding roto o si algo llego a crash.log.
    /// </summary>
    private async Task RunSelfTestAsync()
    {
        _dashboardWindow!.ShowNearTray();
        _dashboardWindow.UpdateLayout();

        // Los bindings se activan en una pasada posterior del dispatcher, no durante Show().
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        await Task.Delay(TimeSpan.FromMilliseconds(750));
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);

        var errors = _bindingErrors?.Errors ?? [];
        foreach (var error in errors)
        {
            CrashLog.Write("Binding", new InvalidOperationException(error));
        }

        _dashboardWindow.AllowClose();
        Shutdown(errors.Count == 0 ? 0 : 2);
    }

    /// <summary>
    /// Sin consola y sin ventana principal, una excepcion no controlada se veria igual que estar
    /// funcionando en silencio. Se registra siempre, y las que llegan al dispatcher no matan el
    /// proceso: perder el monitoreo entero por un fallo de UI es peor que seguir con lo que hay.
    /// </summary>
    private void HookFailureLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            CrashLog.Write("AppDomain", args.ExceptionObject as Exception);

        DispatcherUnhandledException += (_, args) =>
        {
            CrashLog.Write("Dispatcher", args.Exception);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLog.Write("Task", args.Exception);
            args.SetObserved();
        };
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
            // Task.Run saca la espera del dispatcher: awaitear ahi y bloquear aqui es el
            // interbloqueo clasico de sync-sobre-async. El limite de tiempo garantiza ademas que
            // un provider atascado no deje el proceso vivo e invisible al salir.
            var scheduler = _scheduler;
            var stopped = Task.Run(async () => await scheduler.DisposeAsync().ConfigureAwait(false));
            if (!stopped.Wait(ShutdownTimeout))
            {
                CrashLog.Write(
                    "Shutdown",
                    new TimeoutException($"El scheduler no se detuvo en {ShutdownTimeout.TotalSeconds:N0} s."));
            }
        }

        _httpTransport?.Dispose();
        _connectionFactory?.Dispose();
        _singleInstance?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task<bool> ReadGlassPreferenceAsync(SqliteSettingsStore settings)
    {
        var saved = await settings.GetAsync("ui.glass", CancellationToken.None);
        return saved is null || (bool.TryParse(saved, out var enabled) && enabled);
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
