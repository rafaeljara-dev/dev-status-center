using DevStatusCenter.Domain.Enums;

namespace DevStatusCenter.Desktop.Tray;

/// <summary>
/// El icono, pixel a pixel.
///
/// Cada dibujo es una rejilla de 16x16 caracteres, que es exactamente el tamano que pide la
/// bandeja de Windows a 96 ppp. El renderizador anterior dibujaba a 32x32 con suavizado y dejaba
/// que Windows redujera: cada pixel acababa siendo el promedio de cuatro y el icono se veia como
/// una mancha. Aqui lo que se escribe es lo que se ve.
///
/// Leyenda: <c>#</c> trazo de la cara · <c>=</c> celda encendida del medidor ·
/// <c>-</c> celda apagada · <c>p</c> celda del pago proximo · <c>.</c> vacio.
///
/// El icono esta partido en dos mitades con reglas distintas: las filas 1-11 son la personalidad
/// (expresiones y gags) y las filas 12-13 son el medidor de presupuesto. La cara son solo los
/// ojos: sin boca, a 16 px la mirada se lee mas limpia y el dibujo aguanta mejor el escalado. Un gag ocupa la cara
/// entera pero nunca toca esas dos filas: la broma no puede costar el dato. Las filas 0, 14 y 15
/// se dejan vacias porque Windows recorta los bordes en algunas escalas de pantalla.
/// </summary>
internal static class TrayArt
{
    public const int Size = 16;

    /// <summary>Celdas del medidor. Doce es el ancho util dejando un margen de 2 px por lado.</summary>
    private const int MeterCells = 12;

    // La paleta vive aqui, en ARGB crudo, porque la pintan dos sistemas de tipos distintos: el
    // icono con System.Drawing y la cara del popup con System.Windows.Media. Tenerla una sola vez
    // es lo que garantiza que las dos caras sean literalmente del mismo color.
    public const uint DimCell = 0xFF2F3A47;
    public const uint PaymentCell = 0xFFF6C85F;

    /// <summary>Color de la cara segun el modo y el presupuesto. Los umbrales no han cambiado.</summary>
    public static uint Accent(PowerMode mode, decimal budgetPercent) => mode switch
    {
        PowerMode.Paused => 0xFF778292,
        PowerMode.Gaming => 0xFFA98AF4,
        _ when budgetPercent >= 95m => 0xFFF06464,
        _ when budgetPercent >= 85m => 0xFFF49A5A,
        _ when budgetPercent >= 70m => 0xFFF6C85F,
        _ => 0xFF62D99C
    };

    /// <summary>Color de una celda, o 0 si esa celda no se pinta.</summary>
    public static uint CellColor(char cell, uint accent) => cell switch
    {
        '#' or '=' => accent,
        '-' => DimCell,
        'p' => PaymentCell,
        _ => 0u
    };

    private static readonly string[] Blank =
    [
        "................", "................", "................", "................",
        "................", "................", "................", "................",
        "................", "................", "................", "................",
        "................", "................", "................", "................",
    ];

    public static readonly string[] EyesOpen = Rows(
        (3, "..####....####.."),
        (4, "..####....####.."),
        (5, "..####....####.."),
        (6, "..####....####.."));

    /// <summary>Al parpadear los ojos se cierran en chevrones hacia dentro: un &gt;_&lt;.</summary>
    public static readonly string[] EyesBlink = Rows(
        (3, "..##........##.."),
        (4, "...##......##..."),
        (5, "...##......##..."),
        (6, "..##........##.."));

    public static readonly string[] EyesSleep = Rows(
        (4, "..####....####.."),
        (5, "..####....####.."));

    public static readonly string[] EyesDead = Rows(
        (3, "..#..#....#..#.."),
        (4, "...##......##..."),
        (5, "...##......##..."),
        (6, "..#..#....#..#.."));

    public static readonly string[] EyesLeft = Shift(EyesOpen, -1);

    public static readonly string[] EyesRight = Shift(EyesOpen, 1);

    public static readonly string[] Laptop = Rows(
        (1, "..############.."),
        (2, "..#..........#.."),
        (3, "..#..........#.."),
        (4, "..#..........#.."),
        (5, "..#..........#.."),
        (6, "..#..........#.."),
        (7, "..############.."),
        (8, ".##############."));

    public static readonly string[] Coffee = Rows(
        (0, "....#..#..#....."),
        (1, "....#..#..#....."),
        (3, "..###########..."),
        (4, "..#.........#..."),
        (5, "..#.........####"),
        (6, "..#.........#..#"),
        (7, "..#.........####"),
        (8, "..#.........#..."),
        (9, "...#########...."));

    /// <summary>El vapor sube un pixel; es lo unico que se mueve dentro del gag.</summary>
    public static readonly string[] CoffeeSteam = Rows(
        (0, ".....#..#..#...."),
        (1, ".....#..#..#...."),
        (3, "..###########..."),
        (4, "..#.........#..."),
        (5, "..#.........####"),
        (6, "..#.........#..#"),
        (7, "..#.........####"),
        (8, "..#.........#..."),
        (9, "...#########...."));

    public static readonly string[] Phone = Rows(
        (1, "....########...."),
        (2, "....#......#...."),
        (3, "....#......#...."),
        (4, "....#......#...."),
        (5, "....#......#...."),
        (6, "....#......#...."),
        (7, "....#......#...."),
        (8, "....#.####.#...."),
        (9, "....########...."));

    public static readonly string[][] Gags = [Laptop, Coffee, Phone];

    /// <summary>Las dos filas de abajo, llenas segun el presupuesto consumido.</summary>
    public static string[] Meter(decimal percent, bool paymentDue)
    {
        var lit = (int)Math.Round(
            Math.Clamp(percent, 0m, 100m) / 100m * MeterCells,
            MidpointRounding.AwayFromZero);
        return MeterRow(index => index < lit ? '=' : '-', paymentDue);
    }

    /// <summary>
    /// Barrido de sincronizacion: un bloque de dos celdas recorre el medidor. La cara no se
    /// entera, porque sincronizar no es un estado de animo.
    /// </summary>
    public static string[] MeterSweep(int head, bool paymentDue) =>
        MeterRow(index => index >= head && index < head + 2 ? '=' : '-', paymentDue);

    private static string[] MeterRow(Func<int, char> cell, bool paymentDue)
    {
        var line = new char[Size];
        Array.Fill(line, '.');
        for (var i = 0; i < MeterCells; i++)
        {
            // La ultima celda marca en ambar que hay un cargo dentro de tres dias. Es el unico
            // aviso que hoy no tiene forma de llegar sin abrir el popup.
            line[i + 2] = paymentDue && i == MeterCells - 1 ? 'p' : cell(i);
        }

        var body = new string(line);
        return Rows((12, body), (13, body));
    }

    public static string[] Merge(params string[][] layers)
    {
        var canvas = new char[Size][];
        for (var y = 0; y < Size; y++)
        {
            canvas[y] = new char[Size];
            Array.Fill(canvas[y], '.');
        }

        foreach (var layer in layers)
        {
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    if (layer[y][x] != '.')
                    {
                        canvas[y][x] = layer[y][x];
                    }
                }
            }
        }

        var result = new string[Size];
        for (var y = 0; y < Size; y++)
        {
            result[y] = new string(canvas[y]);
        }

        return result;
    }

    /// <summary>Desplaza en horizontal (la mirada) o en vertical (la entrada de un gag).</summary>
    public static string[] Shift(string[] grid, int dx = 0, int dy = 0)
    {
        var result = new string[Size];
        for (var y = 0; y < Size; y++)
        {
            var source = y - dy;
            if (source < 0 || source >= Size)
            {
                result[y] = Blank[0];
                continue;
            }

            var line = new char[Size];
            for (var x = 0; x < Size; x++)
            {
                var from = x - dx;
                line[x] = from >= 0 && from < Size ? grid[source][from] : '.';
            }

            result[y] = new string(line);
        }

        return result;
    }

    private static string[] Rows(params (int Y, string Line)[] lines)
    {
        var result = (string[])Blank.Clone();
        foreach (var (y, line) in lines)
        {
            result[y] = line;
        }

        return result;
    }
}
