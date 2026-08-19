using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DevStatusCenter.Domain.Enums;

namespace DevStatusCenter.Desktop.Tray;

[SupportedOSPlatform("windows")]
internal static partial class DynamicTrayIconRenderer
{
    public static Icon Create(PowerMode mode, decimal budgetPercent)
    {
        using var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var color = mode switch
        {
            PowerMode.Paused => Color.FromArgb(119, 130, 146),
            PowerMode.Gaming => Color.FromArgb(169, 138, 244),
            _ when budgetPercent >= 95m => Color.FromArgb(240, 100, 100),
            _ when budgetPercent >= 85m => Color.FromArgb(244, 154, 90),
            _ when budgetPercent >= 70m => Color.FromArgb(246, 200, 95),
            _ => Color.FromArgb(98, 217, 156)
        };

        using var outer = new SolidBrush(Color.FromArgb(35, 42, 55));
        using var inner = new SolidBrush(color);
        graphics.FillEllipse(outer, 1, 1, 30, 30);
        graphics.FillEllipse(inner, 7, 7, 18, 18);

        if (mode == PowerMode.Paused)
        {
            using var pauseBrush = new SolidBrush(Color.FromArgb(35, 42, 55));
            graphics.FillRectangle(pauseBrush, 12, 10, 3, 12);
            graphics.FillRectangle(pauseBrush, 17, 10, 3, 12);
        }

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

