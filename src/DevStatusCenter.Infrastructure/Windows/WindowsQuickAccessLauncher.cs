using System.Diagnostics;
using System.Runtime.Versioning;
using DevStatusCenter.Application.Abstractions;
using DevStatusCenter.Domain.Models;

namespace DevStatusCenter.Infrastructure.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsQuickAccessLauncher(string editorExecutable = "code") : IQuickAccessLauncher
{
    public Task OpenAsync(
        QuickAccessEntry entry,
        QuickAccessAction? action = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        if (entry.Kind == QuickAccessKind.Group || string.IsNullOrWhiteSpace(entry.Path))
        {
            throw new InvalidOperationException("Groups cannot be launched directly.");
        }

        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(entry.Path));
        if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Quick access path does not exist: {fullPath}");
        }

        var selectedAction = action ?? entry.DefaultAction;
        var startInfo = selectedAction switch
        {
            QuickAccessAction.Explorer => CreateExplorerStartInfo(fullPath),
            QuickAccessAction.Terminal => CreateArgumentStartInfo("wt.exe", "-d", DirectoryFor(fullPath)),
            QuickAccessAction.Editor => CreateArgumentStartInfo(editorExecutable, fullPath),
            _ => throw new ArgumentOutOfRangeException(nameof(action), selectedAction, null)
        };

        Process.Start(startInfo);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Abre el Explorador de forma explicita.
    ///
    /// Antes esto era <c>FileName = path</c> con <c>UseShellExecute = true</c>, que delega en el
    /// handler que Windows tenga registrado para la clase Directory. Visual Studio y otros IDE se
    /// apropian de esa asociacion, asi que "abrir la carpeta" terminaba abriendo un IDE. Invocar
    /// explorer.exe directamente hace que la accion signifique siempre lo mismo.
    ///
    /// Un archivo se revela dentro de su carpeta con /select en lugar de ejecutarse: abrir un
    /// .exe o un .ps1 anclado por accidente seria un resultado bastante peor que verlo.
    /// </summary>
    private static ProcessStartInfo CreateExplorerStartInfo(string path)
    {
        var info = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = false
        };

        if (Directory.Exists(path))
        {
            info.ArgumentList.Add(path);
            return info;
        }

        // Forma documentada de /select: un unico argumento con la ruta entrecomillada. Windows no
        // admite comillas dobles en una ruta, asi que no hay nada que escapar.
        info.Arguments = $"/select,\"{path}\"";
        return info;
    }

    private static ProcessStartInfo CreateArgumentStartInfo(string executable, params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = true
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        return info;
    }

    private static string DirectoryFor(string path) =>
        Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
}

