using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ShowHealth.Services.ImdbApi.Models;

namespace Jellyfin.Plugin.ShowHealth.Services.ImdbApi;

/// <summary>
/// Client for the IMDb API (api.imdbapi.dev).
/// Rate limiting: 50 requests/10s window, max 4 concurrent.
/// Automatic retry on 429 with exponential backoff — each retry re-acquires a rate-limiter slot.
/// Persistent cache with 7 day TTL.
/// </summary>
public class ImdbApiClient : IDisposable
{
    private const string BaseUrl = "https://api.imdbapi.dev";
    private const int MaxRetries = 5;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ImdbApiRateLimiter _rateLimiter;
    private readonly ImdbApiCache _cache;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImdbApiClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Factory used to create <see cref="HttpClient"/> instances.</param>
    /// <param name="cacheDir">Directory for persistent cache storage.</param>
    /// <param name="rateLimiter">Optional rate limiter instance.</param>
    public ImdbApiClient(IHttpClientFactory httpClientFactory, string cacheDir, ImdbApiRateLimiter? rateLimiter = null)
    {
        _httpClientFactory = httpClientFactory;
        _rateLimiter = rateLimiter ?? new ImdbApiRateLimiter();
        _cache = new ImdbApiCache(cacheDir);
    }

    /// <summary>
    /// Gets a title by its IMDb ID.
    /// </summary>
    /// <param name="titleId">The IMDb title ID.</param>
    /// <param name="cacheTtl">Optional cache TTL override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<Title?> GetTitleAsync(string titleId, TimeSpan? cacheTtl = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteCachedAsync<Title>($"/titles/{titleId}", cancellationToken, cacheTtl: cacheTtl).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists titles with optional filters.
    /// </summary>
    public async Task<ListTitlesResponse> ListTitlesAsync(
        IEnumerable<string>? types = null,
        IEnumerable<string>? genres = null,
        IEnumerable<string>? countryCodes = null,
        IEnumerable<string>? languageCodes = null,
        IEnumerable<string>? nameIds = null,
        IEnumerable<string>? interestIds = null,
        int? startYear = null,
        int? endYear = null,
        int? minVoteCount = null,
        int? maxVoteCount = null,
        float? minAggregateRating = null,
        float? maxAggregateRating = null,
        string? sortBy = null,
        string? sortOrder = null,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();

        AddMultiParam(queryParams, "types", types);
        AddMultiParam(queryParams, "genres", genres);
        AddMultiParam(queryParams, "countryCodes", countryCodes);
        AddMultiParam(queryParams, "languageCodes", languageCodes);
        AddMultiParam(queryParams, "nameIds", nameIds);
        AddMultiParam(queryParams, "interestIds", interestIds);

        AddParam(queryParams, "startYear", startYear?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "endYear", endYear?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "minVoteCount", minVoteCount?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "maxVoteCount", maxVoteCount?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "minAggregateRating", minAggregateRating?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "maxAggregateRating", maxAggregateRating?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "sortBy", sortBy);
        AddParam(queryParams, "sortOrder", sortOrder);
        AddParam(queryParams, "pageToken", pageToken);

        var result = await ExecuteCachedAsync<ListTitlesResponse>("/titles", cancellationToken, queryParams).ConfigureAwait(false);
        return result ?? new ListTitlesResponse();
    }

    /// <summary>
    /// Gets multiple titles by their IDs in a single batch request (max 5 IDs).
    /// </summary>
    public async Task<BatchGetTitlesResponse> BatchGetTitlesAsync(
        IEnumerable<string> titleIds,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        AddMultiParam(queryParams, "titleIds", titleIds);

        var result = await ExecuteCachedAsync<BatchGetTitlesResponse>("/titles:batchGet", cancellationToken, queryParams).ConfigureAwait(false);
        return result ?? new BatchGetTitlesResponse();
    }

    /// <summary>
    /// Searches for titles by query string.
    /// </summary>
    public async Task<SearchTitlesResponse> SearchTitlesAsync(
        string query,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            { "query", query },
        };

        AddParam(queryParams, "limit", limit?.ToString(CultureInfo.InvariantCulture));

        var result = await ExecuteCachedAsync<SearchTitlesResponse>("/search/titles", cancellationToken, queryParams).ConfigureAwait(false);
        return result ?? new SearchTitlesResponse();
    }

    /// <summary>
    /// Gets a name (person) by their IMDb ID.
    /// </summary>
    public async Task<Name?> GetNameAsync(string nameId, CancellationToken cancellationToken = default)
    {
        return await ExecuteCachedAsync<Name>($"/names/{nameId}", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets multiple names by their IDs in a single batch request (max 5 IDs).
    /// </summary>
    public async Task<BatchGetNamesResponse> BatchGetNamesAsync(
        IEnumerable<string> nameIds,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        AddMultiParam(queryParams, "nameIds", nameIds);

        var result = await ExecuteCachedAsync<BatchGetNamesResponse>("/names:batchGet", cancellationToken, queryParams).ConfigureAwait(false);
        return result ?? new BatchGetNamesResponse();
    }

    /// <summary>
    /// Lists episodes for a TV series title.
    /// </summary>
    /// <param name="titleId">The IMDb title ID.</param>
    /// <param name="season">Season number filter.</param>
    /// <param name="pageSize">Maximum number of results to return.</param>
    /// <param name="pageToken">Pagination token from a previous response.</param>
    /// <param name="cacheTtl">Optional cache TTL override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ListTitleEpisodesResponse> ListTitleEpisodesAsync(
        string titleId,
        string? season = null,
        int? pageSize = null,
        string? pageToken = null,
        TimeSpan? cacheTtl = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        AddParam(queryParams, "season", season);
        AddParam(queryParams, "pageSize", pageSize?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "pageToken", pageToken);

        var result = await ExecuteCachedAsync<ListTitleEpisodesResponse>($"/titles/{titleId}/episodes", cancellationToken, queryParams, cacheTtl).ConfigureAwait(false);
        return result ?? new ListTitleEpisodesResponse();
    }

    /// <summary>
    /// Lists images for a title.
    /// </summary>
    public async Task<ListTitleImagesResponse> ListTitleImagesAsync(
        string titleId,
        IEnumerable<string>? types = null,
        int? pageSize = null,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        AddMultiParam(queryParams, "types", types);
        AddParam(queryParams, "pageSize", pageSize?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "pageToken", pageToken);

        var result = await ExecuteCachedAsync<ListTitleImagesResponse>($"/titles/{titleId}/images", cancellationToken, queryParams).ConfigureAwait(false);
        return result ?? new ListTitleImagesResponse();
    }

    /// <summary>
    /// Lists videos for a title.
    /// </summary>
    public async Task<ListTitleVideosResponse> ListTitleVideosAsync(
        string titleId,
        IEnumerable<string>? types = null,
        int? pageSize = null,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        AddMultiParam(queryParams, "types", types);
        AddParam(queryParams, "pageSize", pageSize?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "pageToken", pageToken);

        var result = await ExecuteCachedAsync<ListTitleVideosResponse>($"/titles/{titleId}/videos", cancellationToken, queryParams).ConfigureAwait(false);
        return result ?? new ListTitleVideosResponse();
    }

    /// <summary>
    /// Lists credits for a title.
    /// </summary>
    public async Task<ListTitleCreditsResponse> ListTitleCreditsAsync(
        string titleId,
        IEnumerable<string>? categories = null,
        int? pageSize = null,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        AddMultiParam(queryParams, "categories", categories);
        AddParam(queryParams, "pageSize", pageSize?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "pageToken", pageToken);

        var result = await ExecuteCachedAsync<ListTitleCreditsResponse>($"/titles/{titleId}/credits", cancellationToken, queryParams).ConfigureAwait(false);
        return result ?? new ListTitleCreditsResponse();
    }

    /// <summary>
    /// Gets box office information for a title.
    /// </summary>
    public async Task<BoxOffice?> GetTitleBoxOfficeAsync(string titleId, CancellationToken cancellationToken = default)
    {
        return await ExecuteCachedAsync<BoxOffice>($"/titles/{titleId}/boxOffice", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists award nominations for a title.
    /// </summary>
    public async Task<ListTitleAwardNominationsResponse> ListTitleAwardNominationsAsync(
        string titleId,
        int? pageSize = null,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        AddParam(queryParams, "pageSize", pageSize?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "pageToken", pageToken);

        var result = await ExecuteCachedAsync<ListTitleAwardNominationsResponse>($"/titles/{titleId}/awardNominations", cancellationToken, queryParams).ConfigureAwait(false);
        return result ?? new ListTitleAwardNominationsResponse();
    }

    /// <summary>
    /// Lists release dates for a title.
    /// </summary>
    public async Task<ListTitleReleaseDatesResponse> ListTitleReleaseDatesAsync(
        string titleId,
        int? pageSize = null,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        AddParam(queryParams, "pageSize", pageSize?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "pageToken", pageToken);

        var result = await ExecuteCachedAsync<ListTitleReleaseDatesResponse>($"/titles/{titleId}/releaseDates", cancellationToken, queryParams).ConfigureAwait(false);
        return result ?? new ListTitleReleaseDatesResponse();
    }

    /// <summary>
    /// Lists AKAs (alternative titles) for a title.
    /// </summary>
    public async Task<ListTitleAKAsResponse> ListTitleAKAsAsync(string titleId, CancellationToken cancellationToken = default)
    {
        return await ExecuteCachedAsync<ListTitleAKAsResponse>($"/titles/{titleId}/akas", cancellationToken).ConfigureAwait(false)
               ?? new ListTitleAKAsResponse();
    }

    /// <summary>
    /// Lists seasons for a title.
    /// </summary>
    /// <param name="titleId">The IMDb title ID.</param>
    /// <param name="cacheTtl">Optional cache TTL override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ListTitleSeasonsResponse> ListTitleSeasonsAsync(string titleId, TimeSpan? cacheTtl = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteCachedAsync<ListTitleSeasonsResponse>($"/titles/{titleId}/seasons", cancellationToken, cacheTtl: cacheTtl).ConfigureAwait(false)
               ?? new ListTitleSeasonsResponse();
    }

    /// <summary>
    /// Lists parents guide for a title.
    /// </summary>
    public async Task<ListTitleParentsGuideResponse> ListTitleParentsGuideAsync(string titleId, CancellationToken cancellationToken = default)
    {
        return await ExecuteCachedAsync<ListTitleParentsGuideResponse>($"/titles/{titleId}/parentsGuide", cancellationToken).ConfigureAwait(false)
               ?? new ListTitleParentsGuideResponse();
    }

    /// <summary>
    /// Lists certificates for a title.
    /// </summary>
    public async Task<ListTitleCertificatesResponse> ListTitleCertificatesAsync(string titleId, CancellationToken cancellationToken = default)
    {
        return await ExecuteCachedAsync<ListTitleCertificatesResponse>($"/titles/{titleId}/certificates", cancellationToken).ConfigureAwait(false)
               ?? new ListTitleCertificatesResponse();
    }

    /// <summary>
    /// Lists company credits for a title.
    /// </summary>
    public async Task<ListTitleCompanyCreditsResponse> ListTitleCompanyCreditsAsync(
        string titleId,
        IEnumerable<string>? categories = null,
        int? pageSize = null,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        AddMultiParam(queryParams, "categories", categories);
        AddParam(queryParams, "pageSize", pageSize?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "pageToken", pageToken);

        var result = await ExecuteCachedAsync<ListTitleCompanyCreditsResponse>($"/titles/{titleId}/companyCredits", cancellationToken, queryParams).ConfigureAwait(false);
        return result ?? new ListTitleCompanyCreditsResponse();
    }

    /// <summary>
    /// Lists filmography for a name.
    /// </summary>
    public async Task<ListNameFilmographyResponse> ListNameFilmographyAsync(
        string nameId,
        IEnumerable<string>? categories = null,
        int? pageSize = null,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        AddMultiParam(queryParams, "categories", categories);
        AddParam(queryParams, "pageSize", pageSize?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "pageToken", pageToken);

        var result = await ExecuteCachedAsync<ListNameFilmographyResponse>($"/names/{nameId}/filmography", cancellationToken, queryParams).ConfigureAwait(false);
        return result ?? new ListNameFilmographyResponse();
    }

    /// <summary>
    /// Lists images for a name.
    /// </summary>
    public async Task<ListNameImagesResponse> ListNameImagesAsync(
        string nameId,
        IEnumerable<string>? types = null,
        int? pageSize = null,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        AddMultiParam(queryParams, "types", types);
        AddParam(queryParams, "pageSize", pageSize?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "pageToken", pageToken);

        var result = await ExecuteCachedAsync<ListNameImagesResponse>($"/names/{nameId}/images", cancellationToken, queryParams).ConfigureAwait(false);
        return result ?? new ListNameImagesResponse();
    }

    /// <summary>
    /// Lists relationships for a name.
    /// </summary>
    public async Task<ListNameRelationshipsResponse> ListNameRelationshipsAsync(string nameId, CancellationToken cancellationToken = default)
    {
        return await ExecuteCachedAsync<ListNameRelationshipsResponse>($"/names/{nameId}/relationships", cancellationToken).ConfigureAwait(false)
               ?? new ListNameRelationshipsResponse();
    }

    /// <summary>
    /// Lists trivia for a name.
    /// </summary>
    public async Task<ListNameTriviaResponse> ListNameTriviaAsync(
        string nameId,
        int? pageSize = null,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        AddParam(queryParams, "pageSize", pageSize?.ToString(CultureInfo.InvariantCulture));
        AddParam(queryParams, "pageToken", pageToken);

        var result = await ExecuteCachedAsync<ListNameTriviaResponse>($"/names/{nameId}/trivia", cancellationToken, queryParams).ConfigureAwait(false);
        return result ?? new ListNameTriviaResponse();
    }

    /// <summary>
    /// Lists star meter rankings.
    /// </summary>
    public async Task<ListStarMetersResponse> ListStarMetersAsync(
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>();
        AddParam(queryParams, "pageToken", pageToken);

        var result = await ExecuteCachedAsync<ListStarMetersResponse>("/chart/starmeter", cancellationToken, queryParams).ConfigureAwait(false);
        return result ?? new ListStarMetersResponse();
    }

    /// <summary>
    /// Lists interest categories.
    /// </summary>
    public async Task<ListInterestCategoriesResponse> ListInterestCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteCachedAsync<ListInterestCategoriesResponse>("/interests", cancellationToken).ConfigureAwait(false)
               ?? new ListInterestCategoriesResponse();
    }

    /// <summary>
    /// Gets an interest by ID.
    /// </summary>
    public async Task<Interest?> GetInterestAsync(string interestId, CancellationToken cancellationToken = default)
    {
        return await ExecuteCachedAsync<Interest>($"/interests/{interestId}", cancellationToken).ConfigureAwait(false);
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
    /// <param name="pathPrefix">The relative API path prefix (e.g. "/titles/tt123/episodes").</param>
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

    private async Task<T?> ExecuteCachedAsync<T>(string path, CancellationToken cancellationToken, Dictionary<string, string>? queryParams = null, TimeSpan? cacheTtl = null)
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
            await _cache.SetAsync(cacheKey, result, cacheTtl, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Executes a request with retry logic. Each attempt — including retries — acquires its own
    /// rate-limiter slot so that retried 429 responses do not bypass the window limit.
    /// </summary>
    private async Task<T?> ExecuteWithRetryAsync<T>(string path, CancellationToken cancellationToken, Dictionary<string, string>? queryParams = null)
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
                        return (Response: response, Value: default(T), ShouldRetry: true);
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new ImdbApiException((int)response.StatusCode, $"IMDb API error: {response.StatusCode} for {url}");
                    }

                    var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
                    return (Response: response, Value: value, ShouldRetry: false);
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
                throw new ImdbApiRateLimitException($"IMDb API rate limit exceeded after {MaxRetries + 1} attempts.");
            }
        }

        return default;
    }

    private static string BuildAbsoluteUrl(string path, Dictionary<string, string>? queryParams)
    {
        return $"{BaseUrl}{BuildRelativeUrl(path, queryParams)}";
    }

    private static string BuildRelativeUrl(string path, Dictionary<string, string>? queryParams)
    {
        if (queryParams == null || queryParams.Count == 0)
        {
            return path;
        }

        var queryString = string.Join("&", queryParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{path}?{queryString}";
    }

    private static void AddParam(Dictionary<string, string> dict, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            dict[key] = value;
        }
    }

    private static void AddMultiParam(Dictionary<string, string> dict, string key, IEnumerable<string>? values)
    {
        if (values == null)
        {
            return;
        }

        var list = values.ToList();
        if (list.Count == 0)
        {
            return;
        }

        dict[key] = string.Join(",", list);
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
                _rateLimiter.Dispose();
                _cache.Dispose();
            }

            _disposed = true;
        }
    }
}
