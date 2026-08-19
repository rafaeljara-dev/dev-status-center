using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Forms = System.Windows.Forms;

namespace DevStatusCenter.Desktop.Tray;

/// <summary>
/// Convierte una rejilla de <see cref="TrayArt"/> en un icono de Windows.
///
/// Sin suavizado y sin escalado suave: cada pixel del destino toma el color de la celda que le
/// toca por vecino mas cercano. En el caso habitual (bandeja de 16 px) la correspondencia es
/// uno a uno y el dibujo sale intacto; en pantallas con mas ppp, donde Windows pide 20, 24 o 32,
/// los bordes siguen siendo rectos en vez de convertirse en grises.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class TrayIconFactory
{
    /// <summary>
    /// Lo que Windows pide de verdad para el area de notificaciones. Se consulta en cada llamada
    /// porque cambia si el usuario mueve la escala de pantalla sin reiniciar la sesion.
    /// </summary>
    public static int PreferredSize =>
        Math.Max(TrayArt.Size, Forms.SystemInformation.SmallIconSize.Width);

    public static Icon Create(string[] grid, uint accent)
    {
        var size = PreferredSize;
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        for (var y = 0; y < size; y++)
        {
            var row = grid[y * TrayArt.Size / size];
            for (var x = 0; x < size; x++)
            {
                var argb = TrayArt.CellColor(row[x * TrayArt.Size / size], accent);
                if (argb != 0)
                {
                    bitmap.SetPixel(x, y, Color.FromArgb(unchecked((int)argb)));
                }
            }
        }

        // GetHicon entrega un handle que hay que destruir a mano; Icon.FromHandle no lo posee.
        // Clonar y destruir es la unica forma de devolver un Icon que se pueda liberar solo.
        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr handle);
}
