using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ShowHealth.Models;
using Jellyfin.Plugin.ShowHealth.Services.Jellyfin;
using Jellyfin.Plugin.ShowHealth.Services.TvMaze;
using Jellyfin.Plugin.ShowHealth.Services.TvMaze.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShowHealth.Services;

/// <summary>
/// Compares Jellyfin library data against TVmaze to find missing episodes and seasons.
/// </summary>
public class ShowHealthAnalyzer
{
    /// <summary>
    /// TVmaze status value for a concluded show. Every other value ("Running",
    /// "To Be Determined", "In Development") is treated as still running.
    /// </summary>
    private const string TvMazeEndedStatus = "Ended";

    /// <summary>
    /// Data for ended shows is immutable — cache it for a year instead of the default 7 days.
    /// </summary>
    private static readonly TimeSpan EndedShowCacheTtl = TimeSpan.FromDays(365);

    private readonly JellyfinLibraryService _libraryService;
    private readonly TvMazeClient _tvMazeClient;
    private readonly ILogger<ShowHealthAnalyzer> _logger;

    private Dictionary<string, JellyfinSeriesInfo>? _seriesIndexCache;
    private DateTime _seriesIndexCacheTime;
    private static readonly TimeSpan SeriesIndexCacheTtl = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowHealthAnalyzer"/> class.
    /// </summary>
    public ShowHealthAnalyzer(
        JellyfinLibraryService libraryService,
        TvMazeClient tvMazeClient,
        ILogger<ShowHealthAnalyzer> logger)
    {
        _libraryService = libraryService;
        _tvMazeClient = tvMazeClient;
        _logger = logger;
    }

    /// <summary>
    /// Clears all TVmaze API cache entries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        await _tvMazeClient.ClearCacheAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("TVmaze cache cleared");
    }

    /// <summary>
    /// Returns a list of all series with IMDb IDs from Jellyfin (no TVmaze calls).
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
    /// Analyzes a single series by IMDb ID against TVmaze.
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
    public async Task<ShowHealthResponse> AnalyzeAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var seriesIndex = BuildSeriesIndex(cancellationToken);
        var total = seriesIndex.Count;
        _logger.LogInformation("Analyzing {Count} series with IMDb IDs", total);

        var results = new List<SeriesHealthResult>();
        var processed = 0;

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

            processed++;
            progress?.Report((double)processed / total * 100.0);
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

        var showId = await _tvMazeClient.LookupShowIdByImdbAsync(imdbId, cancellationToken).ConfigureAwait(false);
        if (showId == null)
        {
            _logger.LogDebug("Skipping series {Name} — {ImdbId} is unknown to TVmaze", series.Name, imdbId);
            return null;
        }

        // A single request returns the show plus all its seasons and episodes.
        // Ended shows never change, so they are cached for a year instead of the default 7 days.
        var show = await _tvMazeClient.GetShowWithEpisodesAsync(
            showId.Value,
            ttlSelector: s => IsEndedStatus(s.Status) ? EndedShowCacheTtl : (TimeSpan?)null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (show == null)
        {
            _logger.LogDebug("Skipping series {Name} — TVmaze returned no data for show {ShowId}", series.Name, showId.Value);
            return null;
        }

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

        // Group remote episodes by season. Episodes without a season or episode number are
        // specials/unnumbered entries and cannot be matched against the local library.
        var remoteEpisodesBySeason = (show.Embedded?.Episodes ?? Array.Empty<TvMazeEpisode>())
            .Where(ep => ep.Season is > 0 && ep.Number is > 0)
            .GroupBy(ep => ep.Season!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(ep => ep.Number!.Value).ToList());

        // Remote seasons with their announced episode counts. A season with neither a known
        // episode order nor any episodes has only been announced — ignore it.
        var remoteSeasonEntries = (show.Embedded?.Seasons ?? Array.Empty<TvMazeSeason>())
            .Where(s => s.Number > 0)
            .Select(s => new
            {
                Num = s.Number,
                EpisodeCount = s.EpisodeOrder
                               ?? (remoteEpisodesBySeason.TryGetValue(s.Number, out var eps) ? eps.Count : 0),
            })
            .Where(s => s.EpisodeCount > 0 || remoteEpisodesBySeason.ContainsKey(s.Num))
            .ToList();

        var remoteSeasonNumbers = remoteSeasonEntries.Select(s => s.Num).ToList();

        // Find missing seasons with their episode counts
        var missingSeasonInfos = remoteSeasonEntries
            .Where(s => !localSeasonNumbers.Contains(s.Num))
            .Select(s => new MissingSeasonInfo { Season = s.Num, EpisodeCount = s.EpisodeCount })
            .ToList();

        var missingSeasonNumbers = new HashSet<int>(missingSeasonInfos.Select(s => s.Season));

        // Find missing episodes per season
        var missingEpisodes = new List<MissingEpisodeInfo>();
        NextEpisodeInfo? nextEpisode = null;
        var now = DateTime.UtcNow;

        foreach (var seasonNum in remoteSeasonNumbers)
        {
            if (!remoteEpisodesBySeason.TryGetValue(seasonNum, out var remoteEpisodes))
            {
                // Announced season with an episode order but no scheduled episodes yet —
                // nothing has aired, so it must not be reported as missing.
                missingSeasonNumbers.Remove(seasonNum);
                missingSeasonInfos.RemoveAll(s => s.Season == seasonNum);
                continue;
            }

            // Check for next upcoming episode (across all seasons)
            foreach (var ep in remoteEpisodes)
            {
                var airDate = ParseAirDate(ep.Airdate);
                if (airDate == null || airDate.Value <= now)
                {
                    continue;
                }

                if (nextEpisode == null ||
                    string.Compare(FormatDate(airDate.Value), nextEpisode.ReleaseDate ?? string.Empty, StringComparison.Ordinal) < 0)
                {
                    nextEpisode = new NextEpisodeInfo
                    {
                        Season = seasonNum,
                        Episode = ep.Number!.Value,
                        Title = ep.Name ?? "TBA",
                        ReleaseDate = FormatDate(airDate.Value),
                    };
                }
            }

            // If the whole season is missing locally, keep it as a missing season
            // but don't list individual episodes (they're implied by the season)
            if (missingSeasonNumbers.Contains(seasonNum))
            {
                // An episode counts as "aired" only if its air date is in the past.
                // No air date = unscheduled = don't count as aired.
                var airedEpisodes = remoteEpisodes.Where(ep => HasAired(ep.Airdate, now)).ToList();

                if (airedEpisodes.Count == 0)
                {
                    // Nothing in this season has aired yet — don't count it as missing
                    missingSeasonNumbers.Remove(seasonNum);
                    missingSeasonInfos.RemoveAll(s => s.Season == seasonNum);
                }
                else
                {
                    // Populate individual episode details for this missing season (used by CSV export)
                    var seasonInfo = missingSeasonInfos.Find(s => s.Season == seasonNum);
                    if (seasonInfo != null)
                    {
                        foreach (var ep in airedEpisodes)
                        {
                            seasonInfo.Episodes.Add(new MissingEpisodeInfo
                            {
                                Season = seasonNum,
                                Episode = ep.Number!.Value,
                                Title = ep.Name ?? "TBA",
                                TvMazeId = ep.Id,
                            });
                        }
                    }
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

            foreach (var ep in remoteEpisodes)
            {
                // Skip episodes that have not aired yet or have no air date at all
                if (!HasAired(ep.Airdate, now))
                {
                    continue;
                }

                if (!localEpisodeNumbers.Contains(ep.Number!.Value))
                {
                    missingEpisodes.Add(new MissingEpisodeInfo
                    {
                        Season = seasonNum,
                        Episode = ep.Number!.Value,
                        Title = ep.Name ?? "TBA",
                        TvMazeId = ep.Id,
                    });
                }
            }
        }

        // Invalidate the cache for shows with an imminent release so the next scan fetches
        // fresh data instead of serving a stale episode list.
        if (nextEpisode?.ReleaseDate != null)
        {
            var nextDate = ParseAirDate(nextEpisode.ReleaseDate);
            if (nextDate.HasValue && (nextDate.Value - now).TotalDays <= 7)
            {
                await _tvMazeClient.InvalidateCacheByPrefixAsync(TvMazeClient.ShowPath(showId.Value), cancellationToken).ConfigureAwait(false);
            }
        }

        var isEnded = IsEndedStatus(show.Status);
        var startYear = ParseYear(show.Premiered) ?? series.ProductionYear ?? 0;
        var endYear = isEnded ? ParseYear(show.Ended) : null;

        var status = isEnded ? ShowStatus.Ended : ShowStatus.Running;

        // Classify gaps vs trailing.
        // A missing season is a "gap" if there is a present season with a HIGHER number.
        // A missing episode in a present season is ALWAYS a gap (the season is "started").
        var highestPresentSeason = localSeasonNumbers.Count > 0 ? localSeasonNumbers.Max() : 0;

        foreach (var ms in missingSeasonInfos)
        {
            ms.IsGap = ms.Season < highestPresentSeason;

            // Propagate gap flag to individual episode details
            foreach (var ep in ms.Episodes)
            {
                ep.IsGap = ms.IsGap;
            }
        }

        foreach (var ep in missingEpisodes)
        {
            // Episodes in present seasons are always gaps — the season is started
            ep.IsGap = true;
        }

        return new SeriesHealthResult
        {
            Name = series.Name,
            JellyfinId = series.Id.ToString("N"),
            ImdbId = imdbId,
            TvMazeId = showId.Value,
            TvdbId = series.TvdbId,
            Genres = series.Genres,
            Studios = series.Studios,
            CommunityRating = series.CommunityRating,
            Overview = series.Overview,
            StartYear = startYear,
            EndYear = endYear,
            Status = status,
            SeasonsLocal = localSeasonNumbers.Count,
            MissingSeasons = missingSeasonInfos,
            MissingEpisodes = missingEpisodes,
            NextEpisode = nextEpisode,
        };
    }

    private static bool IsEndedStatus(string? status)
    {
        return string.Equals(status, TvMazeEndedStatus, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true if the episode aired in the past.
    /// Episodes without an air date are unscheduled and never count as aired — reporting them
    /// as missing would produce false positives for announced-but-unreleased content.
    /// </summary>
    private static bool HasAired(string? airDate, DateTime now)
    {
        var parsed = ParseAirDate(airDate);
        return parsed.HasValue && parsed.Value <= now;
    }

    /// <summary>
    /// Parses a TVmaze date string (YYYY-MM-DD) into a UTC <see cref="DateTime"/>.
    /// Returns null for null, empty, or malformed values.
    /// </summary>
    private static DateTime? ParseAirDate(string? date)
    {
        if (string.IsNullOrEmpty(date))
        {
            return null;
        }

        if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return DateTime.SpecifyKind(result, DateTimeKind.Utc);
        }

        return null;
    }

    private static int? ParseYear(string? date)
    {
        var parsed = ParseAirDate(date);
        return parsed?.Year;
    }

    private static string FormatDate(DateTime date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
