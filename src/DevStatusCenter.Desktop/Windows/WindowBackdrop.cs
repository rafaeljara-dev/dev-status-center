using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

// UseWindowsForms mete System.Drawing y System.Windows.Forms en los usings implicitos: sin
// estos alias, los tipos de abajo son ambiguos contra sus homonimos de WinForms.
using Color = System.Windows.Media.Color;

namespace DevStatusCenter.Desktop.Windows;

/// <summary>
/// Cristal nativo para el popup.
///
/// El "liquid glass" de la web se hace con <c>backdrop-filter</c> mas un mapa de desplazamiento
/// SVG. Nada de eso existe en WPF, y meter un WebView solo para el efecto contradiria todo el
/// proyecto. Windows, en cambio, ya trae el desenfoque de fabrica:
///
/// - Mica (<c>DWMSBT_MAINWINDOW</c>) muestrea el fondo de escritorio una vez. No sirve aqui: el
///   popup aparece sobre lo que sea que este abierto, no sobre el wallpaper.
/// - Acrylic (<c>DWMSBT_TRANSIENTWINDOW</c>) desenfoca en tiempo real, y la guia de Microsoft lo
///   reserva justamente para superficies transitorias que se cierran al perder el foco. El popup
///   ya se oculta en <c>Window_Deactivated</c>: es ese caso exacto.
///
/// Coste: cero con el popup oculto — una ventana que no se compone no desenfoca nada — y GPU del
/// compositor de Windows, no del proceso, mientras se mira.
///
/// En Windows 10 el atributo no existe: la llamada devuelve error, esto informa que no se aplico
/// y la ventana se queda con su fondo solido. La degradacion no necesita codigo aparte.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class WindowBackdrop
{
    private const int WindowCornerPreference = 33;
    private const int SystemBackdropType = 38;
    private const int CornerRound = 2;
    private const int BackdropTransientWindow = 3;
    private const int BackdropAuto = 0;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int valueSize);

    /// <summary>
    /// Pide esquinas redondeadas y Acrylic. Devuelve <c>false</c> si el sistema no concede el
    /// backdrop, y en ese caso no toca el fondo de composicion: dejarlo transparente sin cristal
    /// detras pintaria la ventana de negro.
    /// </summary>
    public static bool TryApply(Window window)
    {
        if (PresentationSource.FromVisual(window) is not HwndSource source ||
            source.Handle == IntPtr.Zero)
        {
            return false;
        }

        // Las esquinas se piden aparte: con AllowsTransparency fuera, redondearlas es cosa del
        // DWM, y ademas dibuja mejor el antialiasing del borde que un CornerRadius de WPF.
        var corner = CornerRound;
        DwmSetWindowAttribute(source.Handle, WindowCornerPreference, ref corner, sizeof(int));

        var backdrop = BackdropTransientWindow;
        if (DwmSetWindowAttribute(source.Handle, SystemBackdropType, ref backdrop, sizeof(int)) != 0)
        {
            return false;
        }

        // Sin esto WPF pinta su propio fondo opaco por encima del cristal y no se ve nada.
        source.CompositionTarget.BackgroundColor = Colors.Transparent;
        return true;
    }

    /// <summary>Vuelve al fondo solido, para el interruptor del menu del area de notificaciones.</summary>
    public static void Clear(Window window, Color opaqueBackground)
    {
        if (PresentationSource.FromVisual(window) is not HwndSource source ||
            source.Handle == IntPtr.Zero)
        {
            return;
        }

        var backdrop = BackdropAuto;
        DwmSetWindowAttribute(source.Handle, SystemBackdropType, ref backdrop, sizeof(int));
        source.CompositionTarget.BackgroundColor = opaqueBackground;
    }
}
