namespace DevStatusCenter.Application.Abstractions;

public interface ISettingsStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, string value, CancellationToken cancellationToken);
}

