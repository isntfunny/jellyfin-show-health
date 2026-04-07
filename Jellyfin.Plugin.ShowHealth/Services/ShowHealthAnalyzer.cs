using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ShowHealth.Models;
using Jellyfin.Plugin.ShowHealth.Services.ImdbApi;
using Jellyfin.Plugin.ShowHealth.Services.ImdbApi.Models;
using Jellyfin.Plugin.ShowHealth.Services.Jellyfin;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShowHealth.Services;

/// <summary>
/// Compares Jellyfin library data against IMDb to find missing episodes and seasons.
/// </summary>
public class ShowHealthAnalyzer
{
    private readonly JellyfinLibraryService _libraryService;
    private readonly ImdbApiClient _imdbClient;
    private readonly ILogger<ShowHealthAnalyzer> _logger;

    private Dictionary<string, JellyfinSeriesInfo>? _seriesIndexCache;
    private DateTime _seriesIndexCacheTime;
    private static readonly TimeSpan SeriesIndexCacheTtl = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowHealthAnalyzer"/> class.
    /// </summary>
    public ShowHealthAnalyzer(
        JellyfinLibraryService libraryService,
        ImdbApiClient imdbClient,
        ILogger<ShowHealthAnalyzer> logger)
    {
        _libraryService = libraryService;
        _imdbClient = imdbClient;
        _logger = logger;
    }

    /// <summary>
    /// Clears all IMDb API cache entries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        await _imdbClient.ClearCacheAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("IMDb cache cleared");
    }

    /// <summary>
    /// Returns a list of all series with IMDb IDs from Jellyfin (no IMDb calls).
    /// Uses the same index as AnalyzeAsync to ensure consistency.
    /// </summary>
    public SeriesListResponse GetSeriesList()
    {
        var seriesIndex = BuildSeriesIndex();
        var items = seriesIndex.Values.Select(s => new SeriesListItem
        {
            Name = s.Name,
            JellyfinId = s.Id.ToString("N"),
            ImdbId = s.ImdbId!,
            StartYear = s.ProductionYear ?? 0,
        }).ToList();

        return new SeriesListResponse { Series = items };
    }

    /// <summary>
    /// Builds a dictionary of series indexed by IMDb ID.
    /// No heavy filtering here — the virtual/empty check happens in AnalyzeSeriesAsync.
    /// Cached for 2 minutes to avoid rebuilding on every progressive-loading call.
    /// </summary>
    private Dictionary<string, JellyfinSeriesInfo> BuildSeriesIndex(CancellationToken cancellationToken = default)
    {
        if (_seriesIndexCache != null && (DateTime.UtcNow - _seriesIndexCacheTime) < SeriesIndexCacheTtl)
        {
            return _seriesIndexCache;
        }

        var seriesList = _libraryService.GetSeriesWithImdbId(cancellationToken);
        var index = new Dictionary<string, JellyfinSeriesInfo>(StringComparer.Ordinal);

        foreach (var s in seriesList)
        {
            index[s.ImdbId!] = s;
        }

        _seriesIndexCache = index;
        _seriesIndexCacheTime = DateTime.UtcNow;
        return index;
    }

    /// <summary>
    /// Analyzes a single series by IMDb ID against the IMDb API.
    /// </summary>
    public async Task<SeriesHealthResult?> AnalyzeSeriesAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        var seriesIndex = BuildSeriesIndex(cancellationToken);
        if (!seriesIndex.TryGetValue(imdbId, out var series))
        {
            return null;
        }

        return await AnalyzeSeriesAsync(series, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Analyzes all series in the Jellyfin library and returns health status.
    /// </summary>
    public async Task<ShowHealthResponse> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var seriesIndex = BuildSeriesIndex(cancellationToken);
        _logger.LogInformation("Analyzing {Count} series with IMDb IDs", seriesIndex.Count);

        var results = new List<SeriesHealthResult>();

        foreach (var series in seriesIndex.Values)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var result = await AnalyzeSeriesAsync(series, cancellationToken).ConfigureAwait(false);
                if (result != null)
                {
                    results.Add(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze series {Name} ({ImdbId})", series.Name, series.ImdbId);
            }
        }

        var response = new ShowHealthResponse
        {
            Series = results,
            Summary = new HealthSummary
            {
                Total = results.Count,
                Incomplete = results.Count(r => r.MissingEpisodes.Count > 0 || r.MissingSeasons.Count > 0),
                Running = results.Count(r => r.Status == ShowStatus.Running),
                Ended = results.Count(r => r.Status == ShowStatus.Ended),
            },
        };

        return response;
    }

    private async Task<SeriesHealthResult?> AnalyzeSeriesAsync(JellyfinSeriesInfo series, CancellationToken cancellationToken)
    {
        var imdbId = series.ImdbId!;

        // Fetch title first to determine ended status, which drives the cache TTL for all subsequent calls.
        var title = await _imdbClient.GetTitleAsync(imdbId, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Ended series data is immutable — cache for 365 days; running series use the default 7-day TTL.
        var isLikelyEnded = title != null && title.EndYear > 0;
        var dataTtl = isLikelyEnded ? TimeSpan.FromDays(365) : (TimeSpan?)null;

        var imdbSeasons = await _imdbClient.ListTitleSeasonsAsync(imdbId, cacheTtl: dataTtl, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Load all local episodes in one query (avoids N+1 per season)
        var allLocalEpisodesBySeason = _libraryService.GetAllEpisodesForSeries(series.Id);

        var localSeasons = _libraryService.GetSeasonsForSeries(series.Id, allLocalEpisodesBySeason);

        // Skip series with no real content (only virtual/metadata items)
        if (localSeasons.Count == 0 || localSeasons.All(s => s.EpisodeCount == 0))
        {
            _logger.LogDebug("Skipping series {Name} — no real episodes on disk", series.Name);
            return null;
        }

        // Determine which season numbers exist locally (excluding Specials = season 0)
        var localSeasonNumbers = localSeasons
            .Where(s => s.IndexNumber.HasValue && s.IndexNumber.Value > 0)
            .Select(s => s.IndexNumber!.Value)
            .ToHashSet();

        // Parse IMDb seasons with episode counts
        var imdbSeasonEntries = imdbSeasons.Seasons
            .Select(s => new { Num = int.TryParse(s.SeasonNumber, out var n) ? n : -1, s.EpisodeCount })
            .Where(s => s.Num > 0)
            .ToList();

        var imdbSeasonNumbers = imdbSeasonEntries.Select(s => s.Num).ToList();

        // Find missing seasons with their episode counts
        var missingSeasonInfos = imdbSeasonEntries
            .Where(s => !localSeasonNumbers.Contains(s.Num))
            .Select(s => new MissingSeasonInfo { Season = s.Num, EpisodeCount = s.EpisodeCount })
            .ToList();

        var missingSeasonNumbers = new HashSet<int>(missingSeasonInfos.Select(s => s.Season));

        // Find missing episodes per season
        var missingEpisodes = new List<MissingEpisodeInfo>();
        NextEpisodeInfo? nextEpisode = null;
        var now = DateTime.UtcNow;

        foreach (var seasonNum in imdbSeasonNumbers)
        {
            // EpisodeNumber == 0 means IMDb returned null — unnumbered special/clip show; skip.
            var imdbEpisodes = (await GetAllEpisodesForSeasonAsync(imdbId, seasonNum, dataTtl, cancellationToken).ConfigureAwait(false))
                .Where(ep => ep.EpisodeNumber != 0).ToList();

            // Check for next upcoming episode (across all seasons)
            foreach (var ep in imdbEpisodes)
            {
                if (ep.ReleaseDate != null && IsInFuture(ep.ReleaseDate, now))
                {
                    var candidate = new NextEpisodeInfo
                    {
                        Season = seasonNum,
                        Episode = ep.EpisodeNumber,
                        Title = ep.Title ?? "TBA",
                        ReleaseDate = FormatDate(ep.ReleaseDate),
                    };

                    if (nextEpisode == null ||
                        string.Compare(
                            NormalizeDateForComparison(candidate.ReleaseDate),
                            NormalizeDateForComparison(nextEpisode.ReleaseDate ?? string.Empty),
                            StringComparison.Ordinal) < 0)
                    {
                        nextEpisode = candidate;
                    }
                }
            }

            // If the whole season is missing locally, keep it as a missing season
            // but don't list individual episodes (they're implied by the season)
            if (missingSeasonNumbers.Contains(seasonNum))
            {
                // Check if there are any confirmed aired episodes.
                // An episode counts as "aired" only if it has a releaseDate in the past.
                // No releaseDate = unknown = don't count as aired.
                var hasAiredEpisodes = imdbEpisodes
                    .Any(ep => ep.ReleaseDate != null && !IsInFutureOrCurrentYear(ep.ReleaseDate, now));

                if (!hasAiredEpisodes)
                {
                    // All episodes are future/current-year-only — don't count this season as missing
                    missingSeasonNumbers.Remove(seasonNum);
                    missingSeasonInfos.RemoveAll(s => s.Season == seasonNum);
                }

                continue;
            }

            // Season exists locally — find missing episodes within it
            if (!localSeasonNumbers.Contains(seasonNum))
            {
                continue;
            }

            // Build set of all episode numbers covered locally, including multi-episode files
            // e.g. S06E01E02 has IndexNumber=1 and IndexNumberEnd=2, covering episodes 1 AND 2
            var localEpisodeNumbers = new HashSet<int>();
            if (allLocalEpisodesBySeason.TryGetValue(seasonNum, out var localEps))
            {
                foreach (var e in localEps)
                {
                    if (!e.IndexNumber.HasValue)
                    {
                        continue;
                    }

                    var start = e.IndexNumber.Value;
                    var end = e.IndexNumberEnd ?? start;
                    for (var n = start; n <= end; n++)
                    {
                        localEpisodeNumbers.Add(n);
                    }
                }
            }

            foreach (var ep in imdbEpisodes)
            {
                // Skip future episodes and episodes with only a year in the current year
                if (IsInFutureOrCurrentYear(ep.ReleaseDate, now))
                {
                    continue;
                }

                if (!localEpisodeNumbers.Contains(ep.EpisodeNumber))
                {
                    missingEpisodes.Add(new MissingEpisodeInfo
                    {
                        Season = seasonNum,
                        Episode = ep.EpisodeNumber,
                        Title = ep.Title ?? "TBA",
                        ImdbId = ep.Id,
                    });
                }
            }
        }

        // Fix 5: seasonsTotal = local seasons + confirmed missing seasons (excludes not-yet-aired)
        // Invalidate cache for seasons with imminent releases so next scan fetches fresh data.
        // Uses prefix match to catch all paginated cache entries for this season.
        if (nextEpisode?.ReleaseDate != null)
        {
            var nextDate = ParseDateString(nextEpisode.ReleaseDate);
            if (nextDate.HasValue && (nextDate.Value - now).TotalDays <= 7)
            {
                var seasonPrefix = $"/titles/{imdbId}/episodes";
                await _imdbClient.InvalidateCacheByPrefixAsync(seasonPrefix, cancellationToken).ConfigureAwait(false);
            }
        }

        // Determine status: endYear > 0 means the series has ended,
        // UNLESS there are episodes with a releaseDate after endYear (e.g. reboot/continuation).
        var isEnded = title != null && title.EndYear > 0;
        if (isEnded && nextEpisode?.ReleaseDate != null)
        {
            // Parse year from releaseDate string (format: "YYYY", "YYYY-MM", or "YYYY-MM-DD")
            if (int.TryParse(nextEpisode.ReleaseDate.AsSpan(0, 4), out var nextYear) && nextYear > title!.EndYear)
            {
                isEnded = false;
            }
        }

        var status = isEnded ? ShowStatus.Ended : ShowStatus.Running;

        return new SeriesHealthResult
        {
            Name = series.Name,
            JellyfinId = series.Id.ToString("N"),
            ImdbId = imdbId,
            StartYear = title?.StartYear ?? series.ProductionYear ?? 0,
            EndYear = isEnded ? title!.EndYear : null,
            Status = status,
            SeasonsLocal = localSeasonNumbers.Count,
            MissingSeasons = missingSeasonInfos,
            MissingEpisodes = missingEpisodes,
            NextEpisode = nextEpisode,
        };
    }

    private async Task<List<Episode>> GetAllEpisodesForSeasonAsync(
        string imdbId,
        int seasonNum,
        TimeSpan? cacheTtl,
        CancellationToken cancellationToken)
    {
        var allEpisodes = new List<Episode>();
        string? pageToken = null;

        do
        {
            var response = await _imdbClient.ListTitleEpisodesAsync(
                imdbId,
                season: seasonNum.ToString(CultureInfo.InvariantCulture),
                pageSize: 50,
                pageToken: pageToken,
                cacheTtl: cacheTtl,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            allEpisodes.AddRange(response.Episodes);
            pageToken = response.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));

        return allEpisodes;
    }

    /// <summary>
    /// Pads partial date strings to YYYY-MM-DD for correct lexicographic comparison.
    /// </summary>
    private static string NormalizeDateForComparison(string date)
    {
        return date.Length switch
        {
            4 => date + "-01-01",   // YYYY -> YYYY-01-01
            7 => date + "-01",      // YYYY-MM -> YYYY-MM-01
            _ => date,              // YYYY-MM-DD already, or empty
        };
    }

    /// <summary>
    /// Parses a formatted date string (YYYY, YYYY-MM, or YYYY-MM-DD) into a DateTime.
    /// Returns null if the string cannot be parsed.
    /// </summary>
    private static DateTime? ParseDateString(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
        {
            return null;
        }

        var normalized = NormalizeDateForComparison(dateStr);
        if (DateTime.TryParseExact(normalized, "yyyy-MM-dd", CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var result))
        {
            return DateTime.SpecifyKind(result, DateTimeKind.Utc);
        }

        return null;
    }

    /// <summary>
    /// Returns true if the episode has not yet aired:
    /// - releaseDate is in the future, OR
    /// - releaseDate has only a year (no month/day) and that year is the current year or later.
    /// Episodes with only a year should not be counted as missing for the entire year.
    /// </summary>
    private static bool IsInFutureOrCurrentYear(PrecisionDate? date, DateTime now)
    {
        if (date == null || date.Year == 0)
        {
            return false;
        }

        // Only a year, no month/day — treat as "not yet aired" for the entire year
        if (date.Month == 0 && date.Day == 0)
        {
            return date.Year >= now.Year;
        }

        return IsInFuture(date, now);
    }

    private static bool IsInFuture(PrecisionDate date, DateTime now)
    {
        if (date.Year == 0)
        {
            return false;
        }

        var month = date.Month > 0 ? date.Month : 12;

        // Fix 6: when the day is unknown, use the last day of the month so that an episode with
        // only a month/year is considered "not yet aired" for the entire month (conservative default).
        var day = date.Day > 0 ? date.Day : DateTime.DaysInMonth(date.Year, month);

        var releaseDate = new DateTime(
            date.Year,
            month,
            day,
            0,
            0,
            0,
            DateTimeKind.Utc);

        return releaseDate > now;
    }

    private static string FormatDate(PrecisionDate date)
    {
        if (date.Day > 0 && date.Month > 0)
        {
            return $"{date.Year:D4}-{date.Month:D2}-{date.Day:D2}";
        }

        if (date.Month > 0)
        {
            return $"{date.Year:D4}-{date.Month:D2}";
        }

        return date.Year.ToString(CultureInfo.InvariantCulture);
    }
}
