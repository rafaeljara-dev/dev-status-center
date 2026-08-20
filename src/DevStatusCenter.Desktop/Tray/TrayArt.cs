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
/// Leyenda: <c>#</c> trazo encendido · <c>.</c> vacio. No hay mas: el icono es monocromo y el
/// color entero lo decide el estado del presupuesto.
///
/// La cara son solo los ojos, centrados y sin nada mas alrededor. Hubo una fila de medidor bajo
/// los ojos y se leia como una boca torcida por mucho que se separara: el presupuesto ya lo dice
/// el color del icono, y decirlo dos veces costaba el dibujo. Las filas de los bordes se dejan
/// vacias porque Windows recorta el canto en algunas escalas.
/// </summary>
internal static class TrayArt
{
    public const int Size = 16;

    // La paleta vive aqui, en ARGB crudo, porque la pintan dos sistemas de tipos distintos: el
    // icono con System.Drawing y la cara del popup con System.Windows.Media. Tenerla una sola vez
    // es lo que garantiza que las dos caras sean literalmente del mismo color.

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
    public static uint CellColor(char cell, uint accent) => cell == '#' ? accent : 0u;

    /// <summary>Lienzo vacio. Se usa como cuadro apagado del pulso de aviso.</summary>
    public static readonly string[] Blank =
    [
        "................", "................", "................", "................",
        "................", "................", "................", "................",
        "................", "................", "................", "................",
        "................", "................", "................", "................",
    ];

    /// <summary>
    /// El bloque de la cara. Con los colores invertidos, lo que se pinta es el cuerpo y los ojos
    /// son huecos: a 16 px una mancha solida con agujeros se distingue en la barra mucho antes
    /// que cuatro cuadraditos sueltos.
    /// </summary>
    public static readonly string[] Body = Rows(
        (2, "..############.."),
        (3, ".##############."),
        (4, ".##############."),
        (5, ".##############."),
        (6, ".##############."),
        (7, ".##############."),
        (8, ".##############."),
        (9, ".##############."),
        (10, ".##############."),
        (11, ".##############."),
        (12, ".##############."),
        (13, "..############.."));

    public static readonly string[] EyesOpen = Rows(
        (6, "..####....####.."),
        (7, "..####....####.."),
        (8, "..####....####.."),
        (9, "..####....####.."));

    /// <summary>Al parpadear los ojos se cierran en chevrones hacia dentro: un &gt;_&lt;.</summary>
    public static readonly string[] EyesBlink = Rows(
        (6, "..##........##.."),
        (7, "...##......##..."),
        (8, "...##......##..."),
        (9, "..##........##.."));

    public static readonly string[] EyesSleep = Rows(
        (7, "..####....####.."),
        (8, "..####....####.."));

    public static readonly string[] EyesDead = Rows(
        (6, "..#..#....#..#.."),
        (7, "...##......##..."),
        (8, "...##......##..."),
        (9, "..#..#....#..#.."));

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

    // Los gags se bajan tres filas para centrarse igual que los ojos, en vez de repetir las
    // coordenadas a mano en cada dibujo.
    // El gag tambien se recorta del cuerpo: dibujarlo encima en linea suelta chocaria con una
    // cara que ahora es una mancha llena.
    public static readonly string[][] Gags =
    [
        Knockout(Body, Shift(Laptop, dy: 3)),
        Knockout(Body, Shift(Coffee, dy: 3)),
        Knockout(Body, Shift(Phone, dy: 3))
    ];

    /// <summary>
    /// Recorta <paramref name="holes"/> dentro de <paramref name="body"/>: donde el molde pinta,
    /// el cuerpo se vacia. Es lo que convierte los ojos en agujeros en vez de en manchas.
    /// </summary>
    public static string[] Knockout(string[] body, params string[][] holes)
    {
        var canvas = Merge(body);
        var result = new string[Size];
        for (var y = 0; y < Size; y++)
        {
            var line = canvas[y].ToCharArray();
            foreach (var hole in holes)
            {
                for (var x = 0; x < Size; x++)
                {
                    if (hole[y][x] != '.')
                    {
                        line[x] = '.';
                    }
                }
            }

            result[y] = new string(line);
        }

        return result;
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
