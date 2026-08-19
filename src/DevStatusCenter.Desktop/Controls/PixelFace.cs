using System.Windows;
using System.Windows.Media;
using DevStatusCenter.Desktop.Tray;

// UseWindowsForms mete System.Drawing y System.Windows.Forms en los usings implicitos: sin
// estos alias, los tipos de abajo son ambiguos contra sus homonimos de WinForms.
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace DevStatusCenter.Desktop.Controls;

/// <summary>
/// La misma cara del area de notificaciones, dibujada en grande dentro del popup.
///
/// No es una version parecida ni una reinterpretacion vectorial: pinta exactamente la rejilla de
/// 16x16 que el <see cref="TrayAnimator"/> acaba de mandar al icono, con el mismo color. Si en la
/// barra hay un cafe, aqui hay un cafe, en el mismo cuadro. Una cara que se anima por su cuenta
/// acabaria contando algo distinto de la otra, que es justo lo que no queremos.
///
/// El tamano de celda se redondea a un entero de pixeles de dispositivo: es lo que mantiene los
/// bordes rectos: una celda de 2,4 px repartiria medio pixel de color a los lados y devolveria el
/// borrado que este rediseno vino a quitar.
/// </summary>
internal sealed class PixelFace : FrameworkElement
{
    private readonly Dictionary<uint, Brush> _brushes = [];
    private string[] _grid = TrayArt.EyesOpen;
    private uint _color = TrayArt.Accent(Domain.Enums.PowerMode.Normal, 0m);

    public PixelFace()
    {
        IsHitTestVisible = false;
        SnapsToDevicePixels = true;
    }

    public void SetFrame(FaceFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (ReferenceEquals(_grid, frame.Grid) && _color == frame.Color)
        {
            return;
        }

        _grid = frame.Grid;
        _color = frame.Color;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsInfinity(availableSize.Width) ? TrayArt.Size * 2 : availableSize.Width,
        double.IsInfinity(availableSize.Height) ? TrayArt.Size * 2 : availableSize.Height);

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        base.OnRender(drawingContext);

        var dpi = VisualTreeHelper.GetDpi(this);
        var shortest = Math.Min(ActualWidth, ActualHeight);
        if (shortest < TrayArt.Size)
        {
            return;
        }

        // El redondeo se hace en pixeles fisicos y se vuelve a unidades independientes del
        // dispositivo, de forma que la celda cae en la rejilla de la pantalla tambien al 125 %.
        var cellDevice = Math.Floor(shortest * dpi.DpiScaleX / TrayArt.Size);
        if (cellDevice < 1)
        {
            return;
        }

        var cell = cellDevice / dpi.DpiScaleX;
        var side = cell * TrayArt.Size;
        var originX = Math.Round((ActualWidth - side) / 2 * dpi.DpiScaleX) / dpi.DpiScaleX;
        var originY = Math.Round((ActualHeight - side) / 2 * dpi.DpiScaleY) / dpi.DpiScaleY;

        for (var y = 0; y < TrayArt.Size; y++)
        {
            var row = _grid[y];
            for (var x = 0; x < TrayArt.Size; x++)
            {
                var argb = TrayArt.CellColor(row[x], _color);
                if (argb == 0)
                {
                    continue;
                }

                drawingContext.DrawRectangle(
                    BrushFor(argb),
                    pen: null,
                    new Rect(new Point(originX + (x * cell), originY + (y * cell)), new Size(cell, cell)));
            }
        }
    }

    private Brush BrushFor(uint argb)
    {
        if (_brushes.TryGetValue(argb, out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(Color.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb));
        brush.Freeze();
        _brushes[argb] = brush;
        return brush;
    }
}
