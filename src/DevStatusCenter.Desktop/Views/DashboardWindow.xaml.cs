using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DevStatusCenter.Desktop.ViewModels;
using DevStatusCenter.Desktop.Windows;
using Forms = System.Windows.Forms;

// UseWindowsForms mete System.Drawing y System.Windows.Forms en los usings implicitos: sin
// estos alias, los tipos de abajo son ambiguos contra sus homonimos de WinForms.
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace DevStatusCenter.Desktop.Views;

public partial class DashboardWindow : Window
{
    private readonly Action _manageQuickAccess;
    private readonly DashboardViewModel _viewModel;
    private bool _suppressAutoHide;
    private bool _allowClose;
    private bool _glassWanted = true;
    private bool _glassApplied;

    public DashboardWindow(DashboardViewModel viewModel, Action manageQuickAccess)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _manageQuickAccess = manageQuickAccess;
    }

    /// <summary>Indica si el cristal llegó a aplicarse; en Windows 10 el DWM lo rechaza.</summary>
    public bool IsGlassActive => _glassApplied;

    public void ToggleNearTray()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        ShowNearTray();
    }

    public void ShowNearTray()
    {
        Show();
        UpdateLayout();
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        MaxHeight = Math.Max(MinHeight, (screen.Height / dpi.DpiScaleY) - 24d);
        Left = (screen.Right / dpi.DpiScaleX) - ActualWidth - 12d;
        Top = (screen.Bottom / dpi.DpiScaleY) - Math.Min(ActualHeight, MaxHeight) - 12d;
        Activate();
    }

    /// <summary>
    /// Interruptor del cristal. Existe porque el Acrylic depende de lo que haya detrás: sobre una
    /// ventana clara el texto pierde contraste, y eso solo lo puede juzgar quien está mirando.
    /// </summary>
    public void SetGlass(bool enabled)
    {
        _glassWanted = enabled;
        ApplyBackdrop();
    }

    public void SuppressAutoHide(bool suppress) => _suppressAutoHide = suppress;

    public void AllowClose() => _allowClose = true;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyBackdrop();
    }

    private void ApplyBackdrop()
    {
        var opaque = (SolidColorBrush)FindResource("SurfaceBrush");
        if (!_glassWanted)
        {
            if (_glassApplied)
            {
                WindowBackdrop.Clear(this, opaque.Color);
                _glassApplied = false;
            }

            Background = opaque;
            return;
        }

        _glassApplied = WindowBackdrop.TryApply(this);

        // Sin backdrop concedido, un fondo translúcido dejaría ver negro en vez de desenfoque.
        Background = _glassApplied
            ? (SolidColorBrush)FindResource("SurfaceGlassBrush")
            : opaque;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (!_suppressAutoHide)
        {
            Hide();
        }
    }

    /// <summary>Teclas 1-4 para las pestañas y Esc para cerrar: el popup se usa sin soltar el teclado.</summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        var tab = e.Key switch
        {
            Key.D1 or Key.NumPad1 => DashboardViewModel.TabOverview,
            Key.D2 or Key.NumPad2 => DashboardViewModel.TabAi,
            Key.D3 or Key.NumPad3 => DashboardViewModel.TabCloud,
            Key.D4 or Key.NumPad4 => DashboardViewModel.TabPayments,
            _ => -1
        };

        if (tab >= 0)
        {
            _viewModel.SelectedTab = tab;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void ManageQuickAccess_Click(object sender, RoutedEventArgs e)
    {
        _manageQuickAccess();
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();
}
