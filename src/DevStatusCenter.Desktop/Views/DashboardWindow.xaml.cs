using System.Windows;
using System.Windows.Media;
using DevStatusCenter.Desktop.ViewModels;
using Forms = System.Windows.Forms;

namespace DevStatusCenter.Desktop.Views;

public partial class DashboardWindow : Window
{
    private readonly Action _manageQuickAccess;
    private bool _suppressAutoHide;
    private bool _allowClose;

    public DashboardWindow(DashboardViewModel viewModel, Action manageQuickAccess)
    {
        InitializeComponent();
        DataContext = viewModel;
        _manageQuickAccess = manageQuickAccess;
    }

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

    public void SuppressAutoHide(bool suppress) => _suppressAutoHide = suppress;

    public void AllowClose() => _allowClose = true;

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

    private void ManageQuickAccess_Click(object sender, RoutedEventArgs e)
    {
        _manageQuickAccess();
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();
}
