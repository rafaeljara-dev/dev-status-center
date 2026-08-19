namespace DevStatusCenter.Application.Abstractions;

public interface IStartupManager
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}

