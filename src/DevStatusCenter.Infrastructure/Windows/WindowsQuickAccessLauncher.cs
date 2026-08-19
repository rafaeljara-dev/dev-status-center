using System.Diagnostics;
using System.Runtime.Versioning;
using DevStatusCenter.Application.Abstractions;
using DevStatusCenter.Domain.Models;

namespace DevStatusCenter.Infrastructure.Windows;

[SupportedOSPlatform("windows10.0.19041")]
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

    private static ProcessStartInfo CreateExplorerStartInfo(string path) => new()
    {
        FileName = path,
        UseShellExecute = true
    };

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

