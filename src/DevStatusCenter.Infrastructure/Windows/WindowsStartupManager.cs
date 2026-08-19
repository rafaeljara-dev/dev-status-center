using System.Runtime.Versioning;
using DevStatusCenter.Application.Abstractions;
using Microsoft.Win32;

namespace DevStatusCenter.Infrastructure.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsStartupManager : IStartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DevStatusCenter";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Unable to open the current user's startup registry key.");
        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to resolve the application executable.");
        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Publish the desktop executable before enabling Start with Windows.");
        }

        key.SetValue(ValueName, $"\"{processPath}\"", RegistryValueKind.String);
    }
}

