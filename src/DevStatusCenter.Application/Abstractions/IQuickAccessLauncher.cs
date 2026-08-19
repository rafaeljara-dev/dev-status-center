using DevStatusCenter.Domain.Models;

namespace DevStatusCenter.Application.Abstractions;

public interface IQuickAccessLauncher
{
    Task OpenAsync(
        QuickAccessEntry entry,
        QuickAccessAction? action = null,
        CancellationToken cancellationToken = default);
}

