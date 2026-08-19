using System.Net;
using DevStatusCenter.Application.Providers;

namespace DevStatusCenter.Infrastructure.Networking;

public sealed class ResilientHttpExecutor(
    HttpClient httpClient,
    TimeProvider timeProvider,
    TimeSpan? requestTimeout = null,
    int maximumRetries = 2)
{
    private readonly TimeSpan _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(20);

    public async Task<HttpResponseMessage> SendAsync(
        string providerId,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentNullException.ThrowIfNull(requestFactory);

        for (var attempt = 0; ; attempt++)
        {
            using var request = requestFactory();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_requestTimeout);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt < maximumRetries)
                {
                    await DelayBeforeRetryAsync(attempt, null, cancellationToken);
                    continue;
                }

                throw new ProviderRefreshException(
                    ProviderFailureKind.Timeout,
                    providerId,
                    "request_timeout",
                    $"{providerId} did not respond before the request timeout.",
                    ex);
            }
            catch (HttpRequestException) when (attempt < maximumRetries)
            {
                await DelayBeforeRetryAsync(attempt, null, cancellationToken);
                continue;
            }
            catch (HttpRequestException ex)
            {
                throw new ProviderRefreshException(
                    ProviderFailureKind.Transient,
                    providerId,
                    "network_error",
                    $"Unable to reach {providerId}.",
                    ex);
            }

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                response.Dispose();
                throw new ProviderRefreshException(
                    ProviderFailureKind.Authentication,
                    providerId,
                    "authentication_required",
                    $"{providerId} rejected the configured credential.");
            }

            var retryable = response.StatusCode == HttpStatusCode.TooManyRequests ||
                            (int)response.StatusCode >= 500;
            if (retryable && attempt < maximumRetries)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta;
                if (retryAfter is null && response.Headers.RetryAfter?.Date is { } retryDate)
                {
                    retryAfter = retryDate - timeProvider.GetUtcNow();
                }

                response.Dispose();
                await DelayBeforeRetryAsync(attempt, retryAfter, cancellationToken);
                continue;
            }

            var failureKind = response.StatusCode == HttpStatusCode.TooManyRequests
                ? ProviderFailureKind.RateLimited
                : retryable
                    ? ProviderFailureKind.Transient
                    : ProviderFailureKind.Permanent;
            var code = $"http_{(int)response.StatusCode}";
            response.Dispose();
            throw new ProviderRefreshException(
                failureKind,
                providerId,
                code,
                $"{providerId} returned HTTP {(int)response.StatusCode}.");
        }
    }

    private async Task DelayBeforeRetryAsync(
        int attempt,
        TimeSpan? retryAfter,
        CancellationToken cancellationToken)
    {
        var exponential = TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(25, 150));
        var delay = retryAfter is { } provided && provided > exponential
            ? provided
            : exponential + jitter;
        delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds, 30_000));
        await Task.Delay(delay, timeProvider, cancellationToken);
    }
}
