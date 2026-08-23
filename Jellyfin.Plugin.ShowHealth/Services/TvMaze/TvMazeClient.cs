using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ShowHealth.Services.TvMaze.Models;

namespace Jellyfin.Plugin.ShowHealth.Services.TvMaze;

/// <summary>
/// Client for the TVmaze API (api.tvmaze.com).
/// Free and keyless; rate limited to 20 requests/10s window, max 2 concurrent.
/// Automatic retry on 429 with exponential backoff — each retry re-acquires a rate-limiter slot.
/// Persistent cache with 7 day default TTL.
/// </summary>
public class TvMazeClient : IDisposable
{
    private const string BaseUrl = "https://api.tvmaze.com";
    private const int MaxRetries = 5;

    /// <summary>
    /// The IMDb ID to TVmaze ID mapping never changes, so it is cached far longer than show data.
    /// </summary>
    private static readonly TimeSpan LookupHitTtl = TimeSpan.FromDays(365);

    /// <summary>
    /// Shows unknown to TVmaze may be added later — re-check negative lookups every two weeks.
    /// </summary>
    private static readonly TimeSpan LookupMissTtl = TimeSpan.FromDays(14);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TvMazeRateLimiter _rateLimiter;
    private readonly TvMazeCache _cache;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TvMazeClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Factory used to create <see cref="HttpClient"/> instances.</param>
    /// <param name="cacheDir">Directory for persistent cache storage.</param>
    /// <param name="rateLimiter">Optional rate limiter instance.</param>
    public TvMazeClient(IHttpClientFactory httpClientFactory, string cacheDir, TvMazeRateLimiter? rateLimiter = null)
    {
        _httpClientFactory = httpClientFactory;
        _rateLimiter = rateLimiter ?? new TvMazeRateLimiter();
        _cache = new TvMazeCache(cacheDir);
    }

    /// <summary>
    /// Resolves an IMDb title ID to a TVmaze show ID.
    /// Returns null when TVmaze does not know the title.
    /// </summary>
    /// <param name="imdbId">The IMDb title ID (e.g. "tt0903747").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<int?> LookupShowIdByImdbAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<KeyValuePair<string, string>> { new("imdb", imdbId) };
        var cacheKey = BuildRelativeUrl("/lookup/shows", queryParams);

        // Negative results are cached too, so unknown titles do not hit the API on every scan.
        var cached = await _cache.GetAsync<TvMazeLookupResult>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached != null)
        {
            return cached.ShowId;
        }

        var show = await ExecuteWithRetryAsync<TvMazeShow>("/lookup/shows", cancellationToken, queryParams).ConfigureAwait(false);
        var result = new TvMazeLookupResult { ShowId = show?.Id };

        await _cache.SetAsync(
            cacheKey,
            result,
            result.ShowId.HasValue ? LookupHitTtl : LookupMissTtl,
            cancellationToken).ConfigureAwait(false);

        return result.ShowId;
    }

    /// <summary>
    /// Gets a show with all its seasons and episodes in a single request.
    /// Specials are excluded — TVmaze only returns them when explicitly requested.
    /// </summary>
    /// <param name="showId">The TVmaze show ID.</param>
    /// <param name="ttlSelector">
    /// Optional callback that derives the cache TTL from the fetched show — used to cache
    /// ended shows far longer than running ones without a second request.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<TvMazeShow?> GetShowWithEpisodesAsync(int showId, Func<TvMazeShow, TimeSpan?>? ttlSelector = null, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<KeyValuePair<string, string>>
        {
            new("embed[]", "episodes"),
            new("embed[]", "seasons"),
        };

        return await ExecuteCachedAsync<TvMazeShow>(
            ShowPath(showId),
            cancellationToken,
            queryParams,
            ttlSelector).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the relative API path for a show, usable as a cache-invalidation prefix.
    /// </summary>
    /// <param name="showId">The TVmaze show ID.</param>
    public static string ShowPath(int showId)
    {
        return $"/shows/{showId.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        await _cache.ClearAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Invalidates all cache entries whose key starts with the given prefix.
    /// </summary>
    /// <param name="pathPrefix">The relative API path prefix (e.g. "/shows/169").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InvalidateCacheByPrefixAsync(string pathPrefix, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveByPrefixAsync(pathPrefix, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes all expired cache entries.
    /// </summary>
    public async Task<int> CleanExpiredCacheAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.CleanExpiredAsync(cancellationToken).ConfigureAwait(false);
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
                _cache.Dispose();
            }

            _disposed = true;
        }
    }

    private async Task<T?> ExecuteCachedAsync<T>(string path, CancellationToken cancellationToken, List<KeyValuePair<string, string>>? queryParams = null, Func<T, TimeSpan?>? ttlSelector = null)
    {
        // Cache key uses only the relative path + query string, not the base URL.
        // This ensures cache entries remain valid if the base URL is ever reconfigured.
        var cacheKey = BuildRelativeUrl(path, queryParams);

        var cached = await _cache.GetAsync<T>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached != null)
        {
            return cached;
        }

        var result = await ExecuteWithRetryAsync<T>(path, cancellationToken, queryParams).ConfigureAwait(false);

        if (result != null)
        {
            await _cache.SetAsync(cacheKey, result, ttlSelector?.Invoke(result), cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Executes a request with retry logic. Each attempt — including retries — acquires its own
    /// rate-limiter slot so that retried 429 responses do not bypass the window limit.
    /// A 404 is not an error: TVmaze uses it for "no such show", which the caller treats as null.
    /// </summary>
    private async Task<T?> ExecuteWithRetryAsync<T>(string path, CancellationToken cancellationToken, List<KeyValuePair<string, string>>? queryParams = null)
    {
        var url = BuildAbsoluteUrl(path, queryParams);

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            // Every attempt goes through the rate limiter independently.
            var result = await _rateLimiter.ExecuteAsync(
                async () =>
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        // Signal the caller to retry with backoff.
                        return (Value: default(T), ShouldRetry: true);
                    }

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return (Value: default(T), ShouldRetry: false);
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new TvMazeException((int)response.StatusCode, $"TVmaze API error: {response.StatusCode} for {url}");
                    }

                    var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
                    return (Value: value, ShouldRetry: false);
                },
                cancellationToken).ConfigureAwait(false);

            if (!result.ShouldRetry)
            {
                return result.Value;
            }

            // 429 received — apply exponential backoff before the next rate-limited attempt.
            if (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt));
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new TvMazeRateLimitException($"TVmaze API rate limit exceeded after {MaxRetries + 1} attempts.");
            }
        }

        return default;
    }

    private static string BuildAbsoluteUrl(string path, List<KeyValuePair<string, string>>? queryParams)
    {
        return $"{BaseUrl}{BuildRelativeUrl(path, queryParams)}";
    }

    private static string BuildRelativeUrl(string path, List<KeyValuePair<string, string>>? queryParams)
    {
        if (queryParams == null || queryParams.Count == 0)
        {
            return path;
        }

        var queryString = string.Join("&", queryParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{path}?{queryString}";
    }

    /// <summary>
    /// Cache envelope for IMDb ID lookups so that "not on TVmaze" is cacheable as a value.
    /// </summary>
    private sealed class TvMazeLookupResult
    {
        public int? ShowId { get; set; }
    }
}
