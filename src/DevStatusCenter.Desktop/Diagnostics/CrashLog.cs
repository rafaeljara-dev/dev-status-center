using System.Globalization;
using System.Text;

namespace DevStatusCenter.Desktop.Diagnostics;

/// <summary>
/// Registro de último recurso para excepciones no controladas.
///
/// La aplicación vive en el área de notificaciones y no tiene consola: sin esto, morirse se ve
/// exactamente igual que estar funcionando en silencio, que es el peor modo de fallo posible para
/// algo cuyo trabajo es avisarte de cosas.
///
/// Escribe texto plano en <c>%LOCALAPPDATA%\DevStatusCenter\crash.log</c>, recortándolo cuando
/// crece: un archivo de diagnóstico no debe convertirse en un problema de disco.
/// </summary>
internal static class CrashLog
{
    private const long MaximumBytes = 256 * 1024;

    private static readonly Lock Gate = new();

    private static string? _path;

    public static void Initialize(string localRoot)
    {
        lock (Gate)
        {
            _path = Path.Combine(localRoot, "crash.log");
        }
    }

    /// <summary>Nunca lanza: fallar al registrar un fallo no puede provocar otro.</summary>
    public static void Write(string origin, Exception? exception)
    {
        try
        {
            string? path;
            lock (Gate)
            {
                path = _path;
            }

            if (path is null)
            {
                return;
            }

            var entry = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture))
                .Append("  [").Append(origin).Append("]\n")
                .Append(exception?.ToString() ?? "(sin excepción)")
                .Append("\n\n")
                .ToString();

            lock (Gate)
            {
                var file = new FileInfo(path);
                if (file.Exists && file.Length > MaximumBytes)
                {
                    File.Delete(path);
                }

                File.AppendAllText(path, entry, Encoding.UTF8);
            }
        }
        catch
        {
            // Un error de disco aquí no debe escalar: el proceso ya está en un camino de fallo.
        }
    }
}
