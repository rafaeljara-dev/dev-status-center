using System.Globalization;
using System.Windows.Input;
using DevStatusCenter.Application.Abstractions;
using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Application.Power;
using DevStatusCenter.Application.Scheduling;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;
using DevStatusCenter.Desktop.Mvvm;

namespace DevStatusCenter.Desktop.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly DashboardService _dashboardService;
    private readonly RefreshScheduler _scheduler;
    private readonly PowerManager _powerManager;
    private readonly IQuickAccessLauncher _quickAccessLauncher;
    private readonly ISettingsStore _settingsStore;
    // El gate solo hace try-acquire (nunca espera), así que un int con Interlocked cubre
    // exactamente el mismo caso sin asignar un SemaphoreSlim ni volver disposable al VM.
    private int _isLoading;
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private IReadOnlyList<ServiceRowViewModel> _aiServices = [];
    private IReadOnlyList<ServiceRowViewModel> _cloudServices = [];
    private IReadOnlyList<PaymentRowViewModel> _payments = [];
    private IReadOnlyList<QuickAccessRowViewModel> _quickAccess = [];
    private string _currentSpend = "USD 0.00";
    private string _projectedSpend = "USD 0.00";
    private string _budgetText = "No budget";
    private decimal _budgetPercent;
    private string _lastSync = "Waiting for first sync";
    private string _statusText = "Loading local cache…";
    private bool _isBusy;
    private DashboardSnapshot? _snapshot;

    public DashboardViewModel(
        DashboardService dashboardService,
        RefreshScheduler scheduler,
        PowerManager powerManager,
        IQuickAccessLauncher quickAccessLauncher,
        ISettingsStore settingsStore)
    {
        _dashboardService = dashboardService;
        _scheduler = scheduler;
        _powerManager = powerManager;
        _quickAccessLauncher = quickAccessLauncher;
        _settingsStore = settingsStore;
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(), _ => !IsBusy);
        ToggleMonitoringCommand = new AsyncRelayCommand(_ => ToggleMonitoringAsync());
        OpenQuickAccessCommand = new AsyncRelayCommand(OpenQuickAccessAsync);
        _powerManager.ModeChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PowerMode));
            OnPropertyChanged(nameof(MonitoringLabel));
            OnPropertyChanged(nameof(StatusColor));
        };
    }

    public event EventHandler<DashboardSnapshot>? SnapshotApplied;

    public ICommand RefreshCommand { get; }

    public ICommand ToggleMonitoringCommand { get; }

    public ICommand OpenQuickAccessCommand { get; }

    public IReadOnlyList<ServiceRowViewModel> AiServices
    {
        get => _aiServices;
        private set => SetProperty(ref _aiServices, value);
    }

    public IReadOnlyList<ServiceRowViewModel> CloudServices
    {
        get => _cloudServices;
        private set => SetProperty(ref _cloudServices, value);
    }

    public IReadOnlyList<PaymentRowViewModel> Payments
    {
        get => _payments;
        private set => SetProperty(ref _payments, value);
    }

    public IReadOnlyList<QuickAccessRowViewModel> QuickAccess
    {
        get => _quickAccess;
        private set => SetProperty(ref _quickAccess, value);
    }

    public string CurrentSpend
    {
        get => _currentSpend;
        private set => SetProperty(ref _currentSpend, value);
    }

    public string ProjectedSpend
    {
        get => _projectedSpend;
        private set => SetProperty(ref _projectedSpend, value);
    }

    public string BudgetText
    {
        get => _budgetText;
        private set => SetProperty(ref _budgetText, value);
    }

    public decimal BudgetPercent
    {
        get => _budgetPercent;
        private set => SetProperty(ref _budgetPercent, value);
    }

    public string LastSync
    {
        get => _lastSync;
        private set => SetProperty(ref _lastSync, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value) && RefreshCommand is AsyncRelayCommand command)
            {
                command.RaiseCanExecuteChanged();
            }
        }
    }

    public PowerMode PowerMode => _powerManager.Mode;

    public string MonitoringLabel => PowerMode switch
    {
        PowerMode.Normal => "● NORMAL",
        PowerMode.Eco => "◐ ECO",
        PowerMode.Paused => "○ PAUSED",
        PowerMode.Gaming => "🎮 GAMING",
        _ => PowerMode.ToString().ToUpperInvariant()
    };

    public string StatusColor => PowerMode switch
    {
        PowerMode.Paused => "#778292",
        PowerMode.Gaming => "#A98AF4",
        _ when BudgetPercent >= 95m => "#F06464",
        _ when BudgetPercent >= 85m => "#F49A5A",
        _ when BudgetPercent >= 70m => "#F6C85F",
        _ => "#62D99C"
    };

    public DashboardSnapshot? Snapshot => _snapshot;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var snapshot = await _dashboardService.LoadAsync(cancellationToken);
            _snapshot = snapshot;
            CurrentSpend = ServiceRowViewModel.FormatMoney(
                snapshot.CurrentSpend.Amount,
                snapshot.Currency,
                _culture);
            ProjectedSpend = ServiceRowViewModel.FormatMoney(
                snapshot.ProjectedSpend.Amount,
                snapshot.Currency,
                _culture);
            BudgetText = snapshot.MonthlyBudget is { } budget
                ? $"{snapshot.BudgetPercent:0.#}% of {ServiceRowViewModel.FormatMoney(budget.Amount, budget.Currency, _culture)}"
                : "No monthly budget";
            BudgetPercent = Math.Clamp(snapshot.BudgetPercent ?? 0m, 0m, 100m);
            AiServices = snapshot.Services
                .Where(x => x.Category == ServiceCategory.Ai)
                .Select(x => ServiceRowViewModel.From(x, _culture))
                .ToArray();
            CloudServices = snapshot.Services
                .Where(x => x.Category == ServiceCategory.Infrastructure)
                .Select(x => ServiceRowViewModel.From(x, _culture))
                .ToArray();
            Payments = snapshot.UpcomingPayments
                .Take(3)
                .Select(x => PaymentRowViewModel.From(x, _culture))
                .ToArray();
            QuickAccess = FlattenQuickAccess(snapshot.QuickAccess);
            LastSync = snapshot.LastSuccessfulSync is { } sync
                ? $"Last sync {RelativeTime(sync)}"
                : "Waiting for first sync";
            StatusText = snapshot.IsStale ? "Showing cached data" : "Cache is current";
            OnPropertyChanged(nameof(StatusColor));
            SnapshotApplied?.Invoke(this, snapshot);
        }
        finally
        {
            Volatile.Write(ref _isLoading, 0);
        }
    }

    /// <summary>
    /// Muestra en la barra de estado un problema al leer la configuracion. La aplicacion ya
    /// arranco con valores por defecto; esto solo evita que el fallo pase inadvertido.
    /// </summary>
    public void ReportConfigurationProblem(string message) => StatusText = message;

    public async Task SetPowerModeAsync(PowerMode mode, CancellationToken cancellationToken = default)
    {
        _powerManager.SetMode(mode);
        await _settingsStore.SetAsync("power.mode", mode.ToString(), cancellationToken);
        StatusText = mode switch
        {
            PowerMode.Paused => "All background activity stopped",
            PowerMode.Gaming => "Gaming mode: cache only",
            PowerMode.Eco => "Eco monitoring enabled",
            _ => "Normal monitoring enabled"
        };
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            StatusText = "Refreshing providers…";
            var result = await _scheduler.RequestRefreshAsync();
            StatusText = result.Message;
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Refresh cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task ToggleMonitoringAsync() =>
        SetPowerModeAsync(PowerMode is PowerMode.Paused or PowerMode.Gaming
            ? PowerMode.Normal
            : PowerMode.Paused);

    private async Task OpenQuickAccessAsync(object? parameter)
    {
        if (parameter is not QuickAccessRowViewModel row || !row.IsLaunchable)
        {
            return;
        }

        try
        {
            await _quickAccessLauncher.OpenAsync(row.Entry);
            StatusText = $"Opened {row.Name}";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    private string RelativeTime(DateTimeOffset timestamp)
    {
        var elapsed = DateTimeOffset.UtcNow - timestamp;
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{(int)elapsed.TotalMinutes}m ago";
        }

        return elapsed < TimeSpan.FromDays(1)
            ? $"{(int)elapsed.TotalHours}h ago"
            : timestamp.ToLocalTime().ToString("MMM d", _culture);
    }

    private static List<QuickAccessRowViewModel> FlattenQuickAccess(
        IReadOnlyList<QuickAccessEntry> entries)
    {
        var roots = entries
            .Where(x => x.ParentId is null)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .ToArray();
        var children = entries
            .Where(x => x.ParentId is not null)
            .GroupBy(x => x.ParentId!, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(y => y.SortOrder).ThenBy(y => y.DisplayName).ToArray(),
                StringComparer.Ordinal);
        var result = new List<QuickAccessRowViewModel>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void AddChildren(string? parentId, int depth)
        {
            var nodes = parentId is null
                ? roots
                : children.GetValueOrDefault(parentId, []);
            if (nodes.Length == 0)
            {
                return;
            }

            foreach (var node in nodes)
            {
                if (!visited.Add(node.Id))
                {
                    continue;
                }

                result.Add(new QuickAccessRowViewModel(node, depth));
                AddChildren(node.Id, depth + 1);
            }
        }

        AddChildren(null, 0);
        return result;
    }
}
