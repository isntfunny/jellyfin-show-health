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
    /// Analyzes all series in the Jellyfin library and returns health status.
    /// </summary>
    public async Task<ShowHealthResponse> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var seriesList = _libraryService.GetSeriesWithImdbId(cancellationToken);
        _logger.LogInformation("Analyzing {Count} series with IMDb IDs", seriesList.Count);

        var results = new List<SeriesHealthResult>();

        foreach (var series in seriesList)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var result = await AnalyzeSeriesAsync(series, cancellationToken).ConfigureAwait(false);
                results.Add(result);
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
                Running = results.Count(r => r.Status == "running"),
                Ended = results.Count(r => r.Status == "ended"),
            },
        };

        return response;
    }

    private async Task<SeriesHealthResult> AnalyzeSeriesAsync(JellyfinSeriesInfo series, CancellationToken cancellationToken)
    {
        var imdbId = series.ImdbId!;

        // Fetch IMDb data (title + seasons in parallel)
        var titleTask = _imdbClient.GetTitleAsync(imdbId, cancellationToken);
        var seasonsTask = _imdbClient.ListTitleSeasonsAsync(imdbId, cancellationToken);
        await Task.WhenAll(titleTask, seasonsTask).ConfigureAwait(false);

        var title = await titleTask.ConfigureAwait(false);
        var imdbSeasons = await seasonsTask.ConfigureAwait(false);

        // Fetch local seasons
        var localSeasons = _libraryService.GetSeasonsForSeries(series.Id);

        // Determine which season numbers exist locally (excluding Specials = season 0)
        var localSeasonNumbers = localSeasons
            .Where(s => s.IndexNumber.HasValue && s.IndexNumber.Value > 0)
            .Select(s => s.IndexNumber!.Value)
            .ToHashSet();

        // Parse IMDb season numbers
        var imdbSeasonNumbers = imdbSeasons.Seasons
            .Select(s => int.TryParse(s.SeasonNumber, out var n) ? n : -1)
            .Where(n => n > 0)
            .ToList();

        // Find missing seasons
        var missingSeasonNumbers = imdbSeasonNumbers
            .Where(n => !localSeasonNumbers.Contains(n))
            .ToList();

        // Find missing episodes per season
        var missingEpisodes = new List<MissingEpisodeInfo>();
        NextEpisodeInfo? nextEpisode = null;
        var now = DateTime.UtcNow;

        foreach (var seasonNum in imdbSeasonNumbers)
        {
            var imdbEpisodes = await _imdbClient.ListTitleEpisodesAsync(
                imdbId,
                season: seasonNum.ToString(CultureInfo.InvariantCulture),
                pageSize: 50,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Check for next upcoming episode (across all seasons)
            foreach (var ep in imdbEpisodes.Episodes)
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
                        string.Compare(candidate.ReleaseDate, nextEpisode.ReleaseDate, StringComparison.Ordinal) < 0)
                    {
                        nextEpisode = candidate;
                    }
                }
            }

            // If the whole season is missing locally, list all aired episodes as missing
            if (missingSeasonNumbers.Contains(seasonNum))
            {
                foreach (var ep in imdbEpisodes.Episodes)
                {
                    if (ep.ReleaseDate == null || !IsInFuture(ep.ReleaseDate, now))
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

                continue;
            }

            // Season exists locally — find missing episodes within it
            if (!localSeasonNumbers.Contains(seasonNum))
            {
                continue;
            }

            var localSeason = localSeasons.First(s => s.IndexNumber == seasonNum);
            var localEpisodes = _libraryService.GetEpisodesForSeason(localSeason.Id);
            var localEpisodeNumbers = localEpisodes
                .Where(e => e.IndexNumber.HasValue)
                .Select(e => e.IndexNumber!.Value)
                .ToHashSet();

            foreach (var ep in imdbEpisodes.Episodes)
            {
                // Skip future episodes
                if (ep.ReleaseDate != null && IsInFuture(ep.ReleaseDate, now))
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

        // Determine status: endYear > 0 means the series has ended
        var isEnded = title != null && title.EndYear > 0;
        var status = isEnded ? "ended" : "running";

        return new SeriesHealthResult
        {
            Name = series.Name,
            JellyfinId = series.Id.ToString("N"),
            ImdbId = imdbId,
            StartYear = title?.StartYear ?? series.ProductionYear ?? 0,
            EndYear = isEnded ? title!.EndYear : null,
            Status = status,
            SeasonsLocal = localSeasonNumbers.Count,
            SeasonsTotal = imdbSeasonNumbers.Count,
            MissingSeasons = missingSeasonNumbers,
            MissingEpisodes = missingEpisodes,
            NextEpisode = nextEpisode,
        };
    }

    private static bool IsInFuture(PrecisionDate date, DateTime now)
    {
        if (date.Year == 0)
        {
            return false;
        }

        var releaseDate = new DateTime(
            date.Year,
            date.Month > 0 ? date.Month : 12,
            date.Day > 0 ? date.Day : 28,
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
