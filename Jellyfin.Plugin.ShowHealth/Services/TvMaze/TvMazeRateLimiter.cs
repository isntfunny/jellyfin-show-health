using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.ShowHealth.Services.TvMaze;

/// <summary>
/// Rate limiter for the TVmaze API.
/// TVmaze allows at least 20 calls per 10s window per IP; we stay at that documented
/// floor and cap concurrency, because exceeding it results in a temporary IP ban.
/// </summary>
public class TvMazeRateLimiter : IDisposable
{
    private const int MaxRequestsPerWindow = 20;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly object _lock = new();
    private readonly Queue<DateTimeOffset> _timestamps = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TvMazeRateLimiter"/> class.
    /// </summary>
    /// <param name="maxConcurrent">Maximum concurrent requests (default: 2).</param>
    public TvMazeRateLimiter(int maxConcurrent = 2)
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
                var windowStart = now - Window;

                while (_timestamps.Count > 0 && _timestamps.Peek() < windowStart)
                {
                    _timestamps.Dequeue();
                }

                if (_timestamps.Count < MaxRequestsPerWindow)
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
