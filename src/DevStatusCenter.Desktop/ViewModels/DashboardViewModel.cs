using System.Globalization;
using System.Windows.Input;
using System.Windows.Media;
using DevStatusCenter.Application.Abstractions;
using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Application.Power;
using DevStatusCenter.Application.Scheduling;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;
using DevStatusCenter.Desktop.Mvvm;

// UseWindowsForms mete System.Drawing y System.Windows.Forms en los usings implicitos: sin
// estos alias, los tipos de abajo son ambiguos contra sus homonimos de WinForms.
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace DevStatusCenter.Desktop.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    /// <summary>Bloques del medidor. Quince divide 100 en pasos de ~6,7 %, fino sin ser ilegible.</summary>
    private const int MeterBlocks = 15;

    internal const string LastTabSettingKey = "ui.lastTab";

    public const int TabOverview = 0;
    public const int TabAi = 1;
    public const int TabCloud = 2;
    public const int TabPayments = 3;

    private static readonly Brush EmptyBlockBrush = Frozen(Color.FromArgb(0x17, 0xFF, 0xFF, 0xFF));

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
    private IReadOnlyList<ServiceRowViewModel> _topSpend = [];
    private IReadOnlyList<PaymentRowViewModel> _payments = [];
    private IReadOnlyList<QuickAccessRowViewModel> _quickAccess = [];
    private MeterBlockViewModel[] _budgetMeter = BuildMeter(0m, 0m, "#62D99C");
    private PaymentRowViewModel? _nextPayment;
    private string _currentAmount = "0.00";
    private string _projectedAmount = "0.00";
    private string _currencyLabel = "USD";
    private string _budgetText = "No budget";
    private string _budgetLimit = string.Empty;
    private decimal _budgetPercent;
    private string _lastSync = "Waiting for first sync";
    private string _syncBadge = "NO SYNC";
    private string _serviceBadge = "0 SVC";
    private string _alertBadge = string.Empty;
    private string _statusText = "Loading local cache…";
    private bool _isBusy;
    private int _selectedTab = TabOverview;
    private bool _restoringTab;
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
        SelectTabCommand = new RelayCommand(SelectTab);
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

    public ICommand SelectTabCommand { get; }

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

    /// <summary>Los que más pesan, sin separar por categoría: la pregunta de la portada es "en qué se va".</summary>
    public IReadOnlyList<ServiceRowViewModel> TopSpend
    {
        get => _topSpend;
        private set => SetProperty(ref _topSpend, value);
    }

    public IReadOnlyList<PaymentRowViewModel> Payments
    {
        get => _payments;
        private set => SetProperty(ref _payments, value);
    }

    public PaymentRowViewModel? NextPayment
    {
        get => _nextPayment;
        private set => SetProperty(ref _nextPayment, value);
    }

    public IReadOnlyList<QuickAccessRowViewModel> QuickAccess
    {
        get => _quickAccess;
        private set => SetProperty(ref _quickAccess, value);
    }

    public IReadOnlyList<MeterBlockViewModel> BudgetMeter => _budgetMeter;

    public string CurrentAmount
    {
        get => _currentAmount;
        private set => SetProperty(ref _currentAmount, value);
    }

    public string ProjectedAmount
    {
        get => _projectedAmount;
        private set => SetProperty(ref _projectedAmount, value);
    }

    public string CurrencyLabel
    {
        get => _currencyLabel;
        private set => SetProperty(ref _currencyLabel, value);
    }

    public string BudgetText
    {
        get => _budgetText;
        private set => SetProperty(ref _budgetText, value);
    }

    public string BudgetLimit
    {
        get => _budgetLimit;
        private set => SetProperty(ref _budgetLimit, value);
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

    public string SyncBadge
    {
        get => _syncBadge;
        private set => SetProperty(ref _syncBadge, value);
    }

    public string ServiceBadge
    {
        get => _serviceBadge;
        private set => SetProperty(ref _serviceBadge, value);
    }

    public string AlertBadge
    {
        get => _alertBadge;
        private set
        {
            if (SetProperty(ref _alertBadge, value))
            {
                OnPropertyChanged(nameof(HasAlert));
            }
        }
    }

    public bool HasAlert => !string.IsNullOrEmpty(_alertBadge);

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// Pestaña visible. Se recuerda entre sesiones en <c>app_settings</c>; abrir siempre en la
    /// portada obligaría a repetir dos clics a quien mira siempre lo mismo.
    /// </summary>
    public int SelectedTab
    {
        get => _selectedTab;
        set
        {
            var clamped = Math.Clamp(value, TabOverview, TabPayments);
            if (!SetProperty(ref _selectedTab, clamped) || _restoringTab)
            {
                return;
            }

            // Guardar es un detalle de comodidad: si falla, la pestaña sigue cambiada en pantalla
            // y no hay nada útil que decirle al usuario.
            _ = PersistTabAsync(clamped);
        }
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
        PowerMode.Normal => "NORMAL",
        PowerMode.Eco => "ECO",
        PowerMode.Paused => "PAUSED",
        PowerMode.Gaming => "GAMING",
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

    /// <summary>
    /// Restaura la pestaña recordada. Se llama una vez al arrancar, antes de mostrar el popup, y
    /// no vuelve a escribir el ajuste que acaba de leer.
    /// </summary>
    public async Task RestoreTabAsync(CancellationToken cancellationToken = default)
    {
        var saved = await _settingsStore.GetAsync(LastTabSettingKey, cancellationToken);
        if (!int.TryParse(saved, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tab))
        {
            return;
        }

        _restoringTab = true;
        try
        {
            SelectedTab = tab;
        }
        finally
        {
            _restoringTab = false;
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var snapshot = await _dashboardService.LoadAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            _snapshot = snapshot;
            CurrencyLabel = snapshot.Currency;
            CurrentAmount = ServiceRowViewModel.FormatAmount(snapshot.CurrentSpend.Amount, _culture);
            ProjectedAmount = ServiceRowViewModel.FormatAmount(snapshot.ProjectedSpend.Amount, _culture);
            BudgetText = snapshot.MonthlyBudget is { } budget
                ? $"{snapshot.BudgetPercent:0.#}% of {ServiceRowViewModel.FormatMoney(budget.Amount, budget.Currency, _culture)}"
                : "No monthly budget";
            BudgetLimit = snapshot.MonthlyBudget is { } limit
                ? ServiceRowViewModel.FormatAmount(limit.Amount, _culture)
                : "—";
            BudgetPercent = Math.Clamp(snapshot.BudgetPercent ?? 0m, 0m, 100m);

            var projectedPercent = snapshot.MonthlyBudget is { Amount: > 0m } target
                ? Math.Clamp(snapshot.ProjectedSpend.Amount / target.Amount * 100m, 0m, 100m)
                : BudgetPercent;
            _budgetMeter = BuildMeter(BudgetPercent, projectedPercent, StatusColor);
            OnPropertyChanged(nameof(BudgetMeter));

            AiServices = Project(snapshot.Services.Where(x => x.Category == ServiceCategory.Ai));
            CloudServices = Project(snapshot.Services.Where(x => x.Category == ServiceCategory.Infrastructure));
            TopSpend = Project(snapshot.Services.OrderByDescending(x => x.Current.Amount).Take(4));

            Payments = snapshot.UpcomingPayments
                .Take(5)
                .Select(x => PaymentRowViewModel.From(x, now, _culture))
                .ToArray();
            NextPayment = Payments.Count > 0 ? Payments[0] : null;
            QuickAccess = FlattenQuickAccess(snapshot.QuickAccess);

            LastSync = snapshot.LastSuccessfulSync is { } sync
                ? $"Last sync {RelativeTime(sync)}"
                : "Waiting for first sync";
            SyncBadge = snapshot.LastSuccessfulSync is { } stamp
                ? $"SYNC {CompactAge(now - stamp)}"
                : "NO SYNC";
            ServiceBadge = $"{snapshot.Services.Count} SVC";
            var failing = snapshot.ProviderStates.Count(x => x.ConsecutiveFailures > 0);
            AlertBadge = failing > 0
                ? $"{failing} PROVIDER{(failing == 1 ? string.Empty : "S")} FAILING"
                : BudgetPercent >= 85m
                    ? $"BUDGET {BudgetPercent:0}%"
                    : string.Empty;

            StatusText = snapshot.IsStale ? "Showing cached data" : "Cache is current";
            OnPropertyChanged(nameof(StatusColor));
            FocusAlertTab(snapshot);
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

    /// <summary>
    /// Excepción acordada a la memoria de pestaña: si un servicio va pasado de cuota, gana su
    /// categoría sobre la costumbre. Enterarse pesa más que abrir donde uno lo dejó.
    /// </summary>
    private void FocusAlertTab(DashboardSnapshot snapshot)
    {
        var alarming = snapshot.Services.FirstOrDefault(service => service.Usage
            .Any(usage => usage.Metric.Kind == MetricKind.QuotaConsumed && usage.Value >= 85m));
        if (alarming is null)
        {
            return;
        }

        SelectedTab = alarming.Category == ServiceCategory.Ai ? TabAi : TabCloud;
    }

    private ServiceRowViewModel[] Project(IEnumerable<DashboardServiceRow> rows)
    {
        var list = rows.ToArray();
        if (list.Length == 0)
        {
            return [];
        }

        var largest = list.Max(x => x.Current.Amount);
        return list
            .OrderByDescending(x => x.Current.Amount)
            .Select(x => ServiceRowViewModel.From(
                x,
                _culture,
                largest > 0m ? (double)(x.Current.Amount / largest) : 0d))
            .ToArray();
    }

    private static MeterBlockViewModel[] BuildMeter(
        decimal spentPercent,
        decimal projectedPercent,
        string spentColor)
    {
        var spent = (int)Math.Round(Math.Clamp(spentPercent, 0m, 100m) / 100m * MeterBlocks,
            MidpointRounding.AwayFromZero);
        var projected = (int)Math.Round(Math.Clamp(projectedPercent, 0m, 100m) / 100m * MeterBlocks,
            MidpointRounding.AwayFromZero);

        var solid = Frozen((Color)ColorConverter.ConvertFromString(spentColor));
        var faded = Frozen(Color.FromArgb(0x6B, solid.Color.R, solid.Color.G, solid.Color.B));

        var blocks = new MeterBlockViewModel[MeterBlocks];
        for (var i = 0; i < MeterBlocks; i++)
        {
            blocks[i] = new MeterBlockViewModel(
                i < spent ? solid :
                i < projected ? faded :
                EmptyBlockBrush);
        }

        return blocks;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void SelectTab(object? parameter)
    {
        if (parameter is int index)
        {
            SelectedTab = index;
            return;
        }

        if (parameter is string text &&
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            SelectedTab = parsed;
        }
    }

    private async Task PersistTabAsync(int tab)
    {
        try
        {
            await _settingsStore.SetAsync(
                LastTabSettingKey,
                tab.ToString(CultureInfo.InvariantCulture),
                CancellationToken.None);
        }
        catch
        {
            // Preferencia de comodidad: se recupera sola la próxima vez que se cambie de pestaña.
        }
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

    /// <summary>Forma corta para la línea de estado, donde no cabe una frase.</summary>
    private static string CompactAge(TimeSpan elapsed) => elapsed switch
    {
        { TotalMinutes: < 1 } => "NOW",
        { TotalHours: < 1 } => $"{(int)elapsed.TotalMinutes}M",
        { TotalDays: < 1 } => $"{(int)elapsed.TotalHours}H",
        _ => $"{(int)elapsed.TotalDays}D"
    };

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
