using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.ShowHealth.Services.ImdbApi;

/// <summary>
/// Rate limiter for the IMDb API.
/// Enforces 50 requests per 10s window with max concurrent requests.
/// </summary>
public class ImdbApiRateLimiter : IDisposable
{
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly object _lock = new();
    private readonly Queue<DateTimeOffset> _timestamps = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImdbApiRateLimiter"/> class.
    /// </summary>
    /// <param name="maxConcurrent">Maximum concurrent requests (default: 4).</param>
    public ImdbApiRateLimiter(int maxConcurrent = 4)
    {
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    /// <summary>
    /// Executes an action with rate limiting.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        await _concurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WaitForSlotAsync(cancellationToken).ConfigureAwait(false);
            return await action().ConfigureAwait(false);
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    /// <summary>
    /// Executes an action with rate limiting.
    /// </summary>
    public async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        await _concurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WaitForSlotAsync(cancellationToken).ConfigureAwait(false);
            await action().ConfigureAwait(false);
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    private async Task WaitForSlotAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            lock (_lock)
            {
                var now = DateTimeOffset.UtcNow;
                var windowStart = now.AddSeconds(-10);

                while (_timestamps.Count > 0 && _timestamps.Peek() < windowStart)
                {
                    _timestamps.Dequeue();
                }

                if (_timestamps.Count < 50)
                {
                    _timestamps.Enqueue(now);
                    return;
                }
            }

            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _concurrencySemaphore.Dispose();
            }

            _disposed = true;
        }
    }
}
