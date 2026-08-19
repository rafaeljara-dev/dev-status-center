using System.Threading.Channels;
using DevStatusCenter.Application.Abstractions;
using DevStatusCenter.Application.Power;
using DevStatusCenter.Application.Providers;
using DevStatusCenter.Domain.Enums;
using DevStatusCenter.Domain.Models;

namespace DevStatusCenter.Application.Scheduling;

public enum RefreshRequestStatus
{
    Completed,
    Throttled,
    Suspended,
    ProviderNotFound
}

public sealed record RefreshRequestResult(
    RefreshRequestStatus Status,
    int RefreshedProviders,
    string Message);

public sealed class RefreshScheduler : IAsyncDisposable
{
    private readonly IReadOnlyList<IProvider> _providers;
    private readonly ILocalStore _store;
    private readonly PowerManager _powerManager;
    private readonly TimeProvider _timeProvider;
    private readonly string _displayCurrency;
    private readonly SemaphoreSlim _concurrency;
    private readonly Channel<SchedulerCommand> _commands;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly Dictionary<string, RuntimeState> _runtime = new(StringComparer.Ordinal);
    private CancellationTokenSource? _activeRefreshCts;
    private Task? _loopTask;
    private bool _disposed;

    public RefreshScheduler(
        IReadOnlyList<IProvider> providers,
        ILocalStore store,
        PowerManager powerManager,
        TimeProvider timeProvider,
        string displayCurrency,
        int maximumConcurrency = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumConcurrency, 1);
        _providers = providers;
        _store = store;
        _powerManager = powerManager;
        _timeProvider = timeProvider;
        _displayCurrency = displayCurrency;
        _concurrency = new SemaphoreSlim(maximumConcurrency, maximumConcurrency);
        _commands = Channel.CreateUnbounded<SchedulerCommand>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _powerManager.ModeChanged += OnPowerModeChanged;
    }

    public event EventHandler? SnapshotChanged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_loopTask is not null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        foreach (var provider in _providers)
        {
            var persisted = await _store.ReadProviderStateAsync(provider.Descriptor.Id, cancellationToken);
            _runtime[provider.Descriptor.Id] = new RuntimeState(
                persisted?.LastAttemptAt,
                persisted?.LastSuccessAt,
                now,
                persisted?.ConsecutiveFailures ?? 0);
        }

        _loopTask = Task.Run(() => RunAsync(_lifetimeCts.Token), CancellationToken.None);
    }

    public async Task<RefreshRequestResult> RequestRefreshAsync(
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var completion = new TaskCompletionSource<RefreshRequestResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await _commands.Writer.WriteAsync(new ManualRefreshCommand(providerId, completion), cancellationToken);
        return await completion.Task.WaitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _powerManager.ModeChanged -= OnPowerModeChanged;
        await _lifetimeCts.CancelAsync();
        _commands.Writer.TryComplete();
        CancelActiveRefresh();

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during application shutdown.
            }
        }

        // The active cycle disposes its own linked source in RefreshProvidersAsync.
        _lifetimeCts.Dispose();
        _concurrency.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            while (_commands.Reader.TryRead(out var command))
            {
                await HandleCommandAsync(command, cancellationToken);
            }

            if (!_powerManager.AllowsBackgroundActivity)
            {
                await _commands.Reader.WaitToReadAsync(cancellationToken);
                continue;
            }

            var now = _timeProvider.GetUtcNow();
            var due = _providers.Where(x => _runtime[x.Descriptor.Id].NextDueAt <= now).ToArray();
            if (due.Length > 0)
            {
                await RefreshProvidersAsync(due, isManual: false, cancellationToken);
                continue;
            }

            var nextDue = _runtime.Count == 0
                ? now.AddHours(24)
                : _runtime.Values.Min(x => x.NextDueAt);
            var delay = nextDue > now ? nextDue - now : TimeSpan.Zero;
            await WaitForCommandOrDelayAsync(delay, cancellationToken);
        }
    }

    private async Task HandleCommandAsync(SchedulerCommand command, CancellationToken cancellationToken)
    {
        switch (command)
        {
            case PowerModeChangedCommand:
                if (_powerManager.AllowsBackgroundActivity)
                {
                    var now = _timeProvider.GetUtcNow();
                    foreach (var key in _runtime.Keys.ToArray())
                    {
                        _runtime[key] = _runtime[key] with { NextDueAt = now };
                    }
                }

                break;

            case ManualRefreshCommand manual:
                await HandleManualRefreshAsync(manual, cancellationToken);
                break;
        }
    }

    private async Task HandleManualRefreshAsync(
        ManualRefreshCommand command,
        CancellationToken cancellationToken)
    {
        if (!_powerManager.AllowsBackgroundActivity)
        {
            command.Completion.TrySetResult(new RefreshRequestResult(
                RefreshRequestStatus.Suspended,
                0,
                "Monitoring is paused. Resume Normal or Eco mode before refreshing."));
            return;
        }

        var selected = command.ProviderId is null
            ? _providers.ToArray()
            : _providers.Where(x => string.Equals(
                x.Descriptor.Id,
                command.ProviderId,
                StringComparison.Ordinal)).ToArray();

        if (selected.Length == 0)
        {
            command.Completion.TrySetResult(new RefreshRequestResult(
                RefreshRequestStatus.ProviderNotFound,
                0,
                "The requested provider is not registered."));
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var eligible = selected.Where(provider =>
        {
            var runtime = _runtime[provider.Descriptor.Id];
            return provider.Descriptor.RefreshPolicy.SupportsManualRefresh &&
                   (runtime.LastAttemptAt is null ||
                    now - runtime.LastAttemptAt >= provider.Descriptor.RefreshPolicy.MinimumInterval);
        }).ToArray();

        if (eligible.Length == 0)
        {
            command.Completion.TrySetResult(new RefreshRequestResult(
                RefreshRequestStatus.Throttled,
                0,
                "Refresh ignored to protect provider rate limits."));
            return;
        }

        await RefreshProvidersAsync(eligible, isManual: true, cancellationToken);
        command.Completion.TrySetResult(new RefreshRequestResult(
            RefreshRequestStatus.Completed,
            eligible.Length,
            $"Refreshed {eligible.Length} provider(s)."));
    }

    private async Task RefreshProvidersAsync(
        IReadOnlyCollection<IProvider> providers,
        bool isManual,
        CancellationToken lifetimeToken)
    {
        using var cycleCts = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
        Interlocked.Exchange(ref _activeRefreshCts, cycleCts)?.Dispose();

        try
        {
            await Task.WhenAll(providers.Select(provider =>
                RefreshOneAsync(provider, isManual, cycleCts.Token)));
        }
        finally
        {
            Interlocked.CompareExchange(ref _activeRefreshCts, null, cycleCts);
        }
    }

    /// <summary>
    /// Cancels the refresh cycle that is running right now, if any. Callable from any
    /// thread: the power-mode event fires on the UI thread while the scheduler loop owns
    /// the cycle's lifetime.
    /// </summary>
    private void CancelActiveRefresh()
    {
        var active = Volatile.Read(ref _activeRefreshCts);
        if (active is null)
        {
            return;
        }

        try
        {
            active.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The cycle completed between the read and the cancel. Nothing left to stop,
            // and the scheduler loop already recorded its result.
        }
    }

    private async Task RefreshOneAsync(
        IProvider provider,
        bool isManual,
        CancellationToken cancellationToken)
    {
        await _concurrency.WaitAsync(cancellationToken);
        try
        {
            var now = _timeProvider.GetUtcNow();
            var runtime = _runtime[provider.Descriptor.Id] with { LastAttemptAt = now };
            _runtime[provider.Descriptor.Id] = runtime;
            await WriteStateAsync(provider, ProviderStatus.Refreshing, runtime, null, null, cancellationToken);

            var context = new ProviderRefreshContext(now, isManual, _displayCurrency);
            var result = await provider.RefreshAsync(context, cancellationToken);
            await _store.ApplyProviderRefreshAsync(result, cancellationToken);

            var completedAt = _timeProvider.GetUtcNow();
            var next = completedAt + IntervalFor(provider.Descriptor.RefreshPolicy);
            runtime = runtime with
            {
                LastSuccessAt = completedAt,
                NextDueAt = next,
                ConsecutiveFailures = 0
            };
            _runtime[provider.Descriptor.Id] = runtime;
            await WriteStateAsync(provider, ProviderStatus.Healthy, runtime, null, null, cancellationToken);
            SnapshotChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The current power mode cancelled the cycle. No error or retry is recorded.
        }
        catch (ProviderRefreshException ex)
        {
            await RecordFailureAsync(provider, ex.Kind, ex.ErrorCode, ex.Message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RecordFailureAsync(
                provider,
                ProviderFailureKind.Transient,
                "unexpected_error",
                ex.Message,
                cancellationToken);
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private async Task RecordFailureAsync(
        IProvider provider,
        ProviderFailureKind kind,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var runtime = _runtime[provider.Descriptor.Id];
        var failures = runtime.ConsecutiveFailures + 1;
        var retry = CalculateBackoff(provider.Descriptor.RefreshPolicy.MinimumInterval, failures);
        runtime = runtime with
        {
            ConsecutiveFailures = failures,
            NextDueAt = _timeProvider.GetUtcNow() + retry
        };
        _runtime[provider.Descriptor.Id] = runtime;

        var status = kind switch
        {
            ProviderFailureKind.Authentication => ProviderStatus.AuthenticationRequired,
            ProviderFailureKind.RateLimited => ProviderStatus.RateLimited,
            _ => ProviderStatus.Error
        };
        await WriteStateAsync(provider, status, runtime, errorCode, errorMessage, cancellationToken);
        SnapshotChanged?.Invoke(this, EventArgs.Empty);
    }

    private Task WriteStateAsync(
        IProvider provider,
        ProviderStatus status,
        RuntimeState runtime,
        string? errorCode,
        string? errorMessage,
        CancellationToken cancellationToken) =>
        _store.WriteProviderStateAsync(new ProviderState(
            provider.Descriptor.Id,
            status,
            runtime.LastAttemptAt,
            runtime.LastSuccessAt,
            runtime.NextDueAt,
            runtime.ConsecutiveFailures,
            errorCode,
            errorMessage), cancellationToken);

    private TimeSpan IntervalFor(RefreshPolicy policy) => _powerManager.Mode switch
    {
        PowerMode.Eco => policy.EcoInterval,
        _ => policy.NormalInterval
    };

    private static TimeSpan CalculateBackoff(TimeSpan minimum, int failures)
    {
        var exponent = Math.Min(failures - 1, 6);
        var baseSeconds = Math.Max(minimum.TotalSeconds, 30d) * Math.Pow(2d, exponent);
        var jitter = Random.Shared.NextDouble() * Math.Min(baseSeconds * 0.2d, 60d);
        return TimeSpan.FromSeconds(Math.Min(baseSeconds + jitter, TimeSpan.FromHours(6).TotalSeconds));
    }

    private async Task WaitForCommandOrDelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var commandReady = _commands.Reader.WaitToReadAsync(waitCts.Token).AsTask();
        var timer = Task.Delay(delay, _timeProvider, waitCts.Token);
        await Task.WhenAny(commandReady, timer);
        await waitCts.CancelAsync();
    }

    private void OnPowerModeChanged(object? sender, PowerMode mode)
    {
        if (mode is PowerMode.Paused or PowerMode.Gaming)
        {
            CancelActiveRefresh();
        }

        _commands.Writer.TryWrite(new PowerModeChangedCommand());
    }

    private abstract record SchedulerCommand;

    private sealed record PowerModeChangedCommand : SchedulerCommand;

    private sealed record ManualRefreshCommand(
        string? ProviderId,
        TaskCompletionSource<RefreshRequestResult> Completion) : SchedulerCommand;

    private sealed record RuntimeState(
        DateTimeOffset? LastAttemptAt,
        DateTimeOffset? LastSuccessAt,
        DateTimeOffset NextDueAt,
        int ConsecutiveFailures);
}
