using System.Globalization;
using System.Runtime.Versioning;
using DevStatusCenter.Application.Abstractions;
using DevStatusCenter.Application.Dashboard;
using DevStatusCenter.Domain.Models;
using DevStatusCenter.Application.Power;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Desktop.ViewModels;
using DevStatusCenter.Desktop.Views;
using Forms = System.Windows.Forms;

namespace DevStatusCenter.Desktop.Tray;

[SupportedOSPlatform("windows")]
public sealed class TrayIconService : IDisposable, INotifier
{
    /// <summary>Ventana de aviso de pago: la misma que usa el evaluador de alertas.</summary>
    private static readonly TimeSpan PaymentHorizon = TimeSpan.FromDays(3);

    /// <summary>
    /// Un fallo aislado se recupera solo en el siguiente ciclo; poner cara de muerto por eso
    /// seria alarmismo. Tres seguidos es el mismo umbral con el que ya se notifica.
    /// </summary>
    private const int FailuresBeforeSadFace = 3;

    private readonly DashboardWindow _window;
    private readonly DashboardViewModel _viewModel;
    private readonly PowerManager _powerManager;
    private readonly IQuickAccessLauncher _launcher;
    private readonly IStartupManager _startupManager;
    private readonly Action _manageQuickAccess;
    private readonly Action _manageProviders;
    private readonly Action<bool> _setGlass;
    private readonly Action _exit;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _quickAccessMenu;
    private readonly TrayAnimator _animator;
    private readonly Action<TrayMotion> _saveMotion;
    private string? _lastSignature;
    private bool _disposed;

    public TrayIconService(
        DashboardWindow window,
        DashboardViewModel viewModel,
        PowerManager powerManager,
        IQuickAccessLauncher launcher,
        IStartupManager startupManager,
        Action manageQuickAccess,
        Action manageProviders,
        bool glassEnabled,
        Action<bool> setGlass,
        TrayMotion motion,
        Action<TrayMotion> saveMotion,
        Action exit)
    {
        _window = window;
        _viewModel = viewModel;
        _powerManager = powerManager;
        _launcher = launcher;
        _startupManager = startupManager;
        _manageQuickAccess = manageQuickAccess;
        _manageProviders = manageProviders;
        _setGlass = setGlass;
        _saveMotion = saveMotion;
        _exit = exit;

        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add("Open Dev Status", null, (_, _) => _window.ShowNearTray());
        _menu.Items.Add("Refresh now", null, (_, _) => _viewModel.RefreshCommand.Execute(null));
        _menu.Items.Add(new Forms.ToolStripSeparator());

        var motionMenu = new Forms.ToolStripMenuItem("Icon motion");
        var motionItems = new List<Forms.ToolStripMenuItem>();
        foreach (var (level, text) in (ReadOnlySpan<(TrayMotion, string)>)
                 [
                     (TrayMotion.Full, "Full — gags and blinking"),
                     (TrayMotion.Useful, "Useful only — sync, news, alerts"),
                     (TrayMotion.Still, "Still — never moves")
                 ])
        {
            var captured = level;
            var item = new Forms.ToolStripMenuItem(text)
            {
                CheckOnClick = false,
                Checked = motion == level
            };
            item.Click += (_, _) => SetMotion(captured, motionItems);
            motionItems.Add(item);
            motionMenu.DropDownItems.Add(item);
        }

        var monitoring = new Forms.ToolStripMenuItem("Monitoring mode");
        monitoring.DropDownItems.Add(CreateModeItem("Normal", PowerMode.Normal));
        monitoring.DropDownItems.Add(CreateModeItem("Eco", PowerMode.Eco));
        monitoring.DropDownItems.Add(CreateModeItem("Paused", PowerMode.Paused));
        monitoring.DropDownItems.Add(CreateModeItem("Gaming", PowerMode.Gaming));
        _menu.Items.Add(monitoring);
        _menu.Items.Add(motionMenu);

        _quickAccessMenu = new Forms.ToolStripMenuItem("Quick access");
        _menu.Items.Add(_quickAccessMenu);
        _menu.Items.Add("Manage quick access…", null, (_, _) => _manageQuickAccess());
        _menu.Items.Add("Providers & credentials…", null, (_, _) => _manageProviders());
        _menu.Items.Add(new Forms.ToolStripSeparator());
        var startupItem = new Forms.ToolStripMenuItem("Start with Windows")
        {
            Checked = _startupManager.IsEnabled,
            CheckOnClick = false
        };
        startupItem.Click += (_, _) => ToggleStartup(startupItem);
        _menu.Items.Add(startupItem);

        // El cristal se puede apagar: sobre una ventana clara detrás, el texto pierde contraste,
        // y eso solo lo puede juzgar quien esté mirando la pantalla en ese momento.
        var glassItem = new Forms.ToolStripMenuItem("Glass background")
        {
            Checked = glassEnabled,
            CheckOnClick = false
        };
        glassItem.Click += (_, _) =>
        {
            glassItem.Checked = !glassItem.Checked;
            _setGlass(glassItem.Checked);
        };
        _menu.Items.Add(glassItem);
        _menu.Items.Add("Exit", null, (_, _) => _exit());

        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _menu,
            Text = "Dev Status Center",
            Visible = true
        };
        _notifyIcon.MouseUp += NotifyIcon_MouseUp;
        _animator = new TrayAnimator(_notifyIcon, motion, TimeProvider.System);
        _viewModel.SnapshotApplied += OnSnapshotApplied;
        _powerManager.ModeChanged += OnPowerModeChanged;
    }

    /// <summary>El scheduler avisa desde su propio hilo; el llamador ya lo trae al de la UI.</summary>
    public void SetSyncing(bool syncing) => _animator.SetSyncing(syncing);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.SnapshotApplied -= OnSnapshotApplied;
        _powerManager.ModeChanged -= OnPowerModeChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.MouseUp -= NotifyIcon_MouseUp;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _animator.Dispose();
    }

    private Forms.ToolStripMenuItem CreateModeItem(string text, PowerMode mode)
    {
        var item = new Forms.ToolStripMenuItem(text)
        {
            CheckOnClick = false,
            Checked = _powerManager.Mode == mode
        };
        item.Click += async (_, _) => await SetModeAsync(mode);
        return item;
    }

    private async Task SetModeAsync(PowerMode mode)
    {
        try
        {
            await _viewModel.SetPowerModeAsync(mode);
            foreach (var item in _menu.Items
                         .OfType<Forms.ToolStripMenuItem>()
                         .Where(x => x.Text == "Monitoring mode")
                         .SelectMany(x => x.DropDownItems.OfType<Forms.ToolStripMenuItem>()))
            {
                item.Checked = string.Equals(item.Text, mode.ToString(), StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void NotifyIcon_MouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
        {
            _window.ToggleNearTray();
        }
    }

    private void OnSnapshotApplied(object? sender, DashboardSnapshot snapshot)
    {
        var now = DateTimeOffset.UtcNow;
        var paymentDue = snapshot.UpcomingPayments.Any(x => x.DueAt - now <= PaymentHorizon);
        RebuildQuickAccess(snapshot.QuickAccess);
        _animator.SetState(
            _powerManager.Mode,
            snapshot.BudgetPercent ?? 0m,
            snapshot.ProviderStates.Any(x => x.ConsecutiveFailures >= FailuresBeforeSadFace),
            paymentDue);

        // La mirada solo tiene sentido si el refresh trajo algo distinto: repetirla en cada ciclo
        // la convertiria en ruido y dejaria de significar "hay novedades".
        var signature = Signature(snapshot);
        if (_lastSignature is not null && !string.Equals(_lastSignature, signature, StringComparison.Ordinal))
        {
            _animator.PlayGlance();
        }

        _lastSignature = signature;
        _notifyIcon.Text = snapshot.LastSuccessfulSync is null
            ? "Dev Status Center · waiting for sync"
            : $"Dev Status Center · {snapshot.CurrentSpend.Currency} {snapshot.CurrentSpend.Amount:N2}";
    }

    /// <summary>Lo que hace distinto a un snapshot del anterior a ojos de quien mira la bandeja.</summary>
    private static string Signature(DashboardSnapshot snapshot) => string.Create(
        CultureInfo.InvariantCulture,
        $"{snapshot.CurrentSpend.Amount}|{snapshot.Services.Count}|{snapshot.UpcomingPayments.Count}");

    private void SetMotion(TrayMotion motion, List<Forms.ToolStripMenuItem> items)
    {
        _animator.Motion = motion;
        for (var i = 0; i < items.Count; i++)
        {
            items[i].Checked = i == (int)motion;
        }

        _saveMotion(motion);
    }

    private void OnPowerModeChanged(object? sender, PowerMode mode) => _animator.SetPowerMode(mode);

    /// <summary>
    /// Notificación nativa mediante globo del área de notificaciones. Un toast propiamente dicho
    /// exige identidad MSIX; hasta entonces esto es lo que Windows ofrece a un ejecutable suelto,
    /// y evita fingir una integración que no existe.
    /// </summary>
    public void Notify(Alert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);

        // Gaming Mode calla, aunque una alerta se haya colado por un refresh manual previo.
        if (_disposed || _powerManager.Mode == PowerMode.Gaming)
        {
            return;
        }

        _animator.PlayAlert();
        _notifyIcon.ShowBalloonTip(
            alert.Severity >= AlertSeverity.Important ? 10_000 : 5_000,
            Truncate(alert.Title, 60),
            Truncate(alert.Body, 220),
            alert.Severity switch
            {
                AlertSeverity.Critical or AlertSeverity.Important => Forms.ToolTipIcon.Warning,
                AlertSeverity.Warning => Forms.ToolTipIcon.Warning,
                _ => Forms.ToolTipIcon.Info
            });
    }

    private static string Truncate(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum];

    private void RebuildQuickAccess(IReadOnlyList<QuickAccessEntry> entries)
    {
        _quickAccessMenu.DropDownItems.Clear();
        var roots = entries
            .Where(x => x.ParentId is null)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .ToArray();
        var children = entries
            .Where(x => x.ParentId is not null)
            .GroupBy(x => x.ParentId!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);

        foreach (var root in roots)
        {
            _quickAccessMenu.DropDownItems.Add(CreateQuickAccessItem(root, children, new HashSet<string>()));
        }

        if (_quickAccessMenu.DropDownItems.Count == 0)
        {
            _quickAccessMenu.DropDownItems.Add(new Forms.ToolStripMenuItem("No pinned projects") { Enabled = false });
        }
    }

    private Forms.ToolStripMenuItem CreateQuickAccessItem(
        QuickAccessEntry entry,
        IReadOnlyDictionary<string, QuickAccessEntry[]> children,
        HashSet<string> ancestors)
    {
        var item = new Forms.ToolStripMenuItem(entry.DisplayName);
        if (!ancestors.Add(entry.Id))
        {
            item.Enabled = false;
            return item;
        }

        if (entry.Kind == QuickAccessKind.Group)
        {
            foreach (var child in children.GetValueOrDefault(entry.Id, []))
            {
                item.DropDownItems.Add(CreateQuickAccessItem(child, children, new HashSet<string>(ancestors)));
            }

            item.Enabled = item.DropDownItems.Count > 0;
        }
        else
        {
            item.Click += async (_, _) =>
            {
                try
                {
                    await _launcher.OpenAsync(entry);
                }
                catch (Exception ex)
                {
                    ShowError(ex.Message);
                }
            };
        }

        return item;
    }

    private void ShowError(string message)
    {
        _notifyIcon.ShowBalloonTip(
            4_000,
            "Dev Status Center",
            Truncate(message, 220),
            Forms.ToolTipIcon.Warning);
    }

    private void ToggleStartup(Forms.ToolStripMenuItem item)
    {
        try
        {
            _startupManager.SetEnabled(!item.Checked);
            item.Checked = _startupManager.IsEnabled;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }
}
