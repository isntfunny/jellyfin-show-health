using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShowHealth.Services.Jellyfin;

/// <summary>
/// Represents a TV series from Jellyfin with its IMDB ID.
/// </summary>
public class JellyfinSeriesInfo
{
    /// <summary>
    /// Jellyfin interne GUID der Serie.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Anzeigename der Serie.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// IMDB ID im Format "tt1234567".
    /// </summary>
    public string? ImdbId { get; set; }

    /// <summary>
    /// TVDB ID (numerisch als String).
    /// </summary>
    public string? TvdbId { get; set; }

    /// <summary>
    /// Produktionsjahr.
    /// </summary>
    public int? ProductionYear { get; set; }

    /// <summary>
    /// Status der Serie (Continuing, Ended, Unreleased).
    /// </summary>
    public SeriesStatus? Status { get; set; }

    /// <summary>
    /// Genres der Serie.
    /// </summary>
    public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Produktionsstudios.
    /// </summary>
    public IReadOnlyList<string> Studios { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Community-Bewertung (0-10).
    /// </summary>
    public float? CommunityRating { get; set; }

    /// <summary>
    /// Zusammenfassung/Plot der Serie.
    /// </summary>
    public string? Overview { get; set; }
}

/// <summary>
/// Repraesentiert eine Season innerhalb einer Serie.
/// </summary>
public class JellyfinSeasonInfo
{
    /// <summary>
    /// Jellyfin interne GUID der Season.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name der Season (z.B. "Season 1", "Specials").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Season-Nummer (0 = Specials).
    /// </summary>
    public int? IndexNumber { get; set; }

    /// <summary>
    /// Anzahl der Episoden in dieser Season.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Jellyfin GUID der Parent-Serie.
    /// </summary>
    public Guid SeriesId { get; set; }
}

/// <summary>
/// Repraesentiert eine Episode innerhalb einer Season.
/// </summary>
public class JellyfinEpisodeInfo
{
    /// <summary>
    /// Jellyfin interne GUID der Episode.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Titel der Episode.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Episode-Nummer innerhalb der Season.
    /// </summary>
    public int? IndexNumber { get; set; }

    /// <summary>
    /// Season-Nummer.
    /// </summary>
    public int? ParentIndexNumber { get; set; }

    /// <summary>
    /// IMDB ID der Episode (falls vorhanden).
    /// </summary>
    public string? ImdbId { get; set; }

    /// <summary>
    /// Erstausstrahlungsdatum.
    /// </summary>
    public DateTime? PremiereDate { get; set; }

    /// <summary>
    /// Laufzeit in Ticks (1 Tick = 100 Nanosekunden).
    /// Fuer Minuten: RunTimeTicks / TimeSpan.TicksPerMinute.
    /// </summary>
    public long? RunTimeTicks { get; set; }

    /// <summary>
    /// Zusammenfassung der Episode.
    /// </summary>
    public string? Overview { get; set; }

    /// <summary>
    /// Community-Bewertung (0-10).
    /// </summary>
    public float? CommunityRating { get; set; }

    /// <summary>
    /// Jellyfin GUID der Parent-Season.
    /// </summary>
    public Guid SeasonId { get; set; }

    /// <summary>
    /// Jellyfin GUID der Parent-Serie.
    /// </summary>
    public Guid SeriesId { get; set; }
}

/// <summary>
/// Service to query TV series from Jellyfin's library.
/// </summary>
public class JellyfinLibraryService
{
    private const int BatchSize = 500;

    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<JellyfinLibraryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinLibraryService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager instance.</param>
    /// <param name="logger">Logger instance.</param>
    public JellyfinLibraryService(
        ILibraryManager libraryManager,
        ILogger<JellyfinLibraryService> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets all TV series from the Jellyfin library.
    /// This method is synchronous because <see cref="ILibraryManager.QueryItems"/> is synchronous.
    /// Call from a background thread (e.g. <c>Task.Run</c>) if you need to avoid blocking.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all series with their metadata.</returns>
    public IReadOnlyList<JellyfinSeriesInfo> GetAllSeries(CancellationToken cancellationToken = default)
    {
        var allSeries = new List<JellyfinSeriesInfo>();
        var startIndex = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Series },
                Recursive = true,
                StartIndex = startIndex,
                Limit = BatchSize,
            };

            var result = _libraryManager.QueryItems(query);

            if (result.Items.Count == 0)
            {
                break;
            }

            foreach (var item in result.Items)
            {
                var seriesInfo = MapToSeriesInfo(item);
                if (seriesInfo != null)
                {
                    allSeries.Add(seriesInfo);
                }
            }

            _logger.LogDebug(
                "Fetched {Count} series (total: {Total}, startIndex: {StartIndex})",
                result.Items.Count,
                result.TotalRecordCount,
                startIndex);

            startIndex += result.Items.Count;

            if (startIndex >= result.TotalRecordCount)
            {
                break;
            }
        }

        _logger.LogInformation(
            "Found {Count} TV series in Jellyfin library",
            allSeries.Count);

        return allSeries;
    }

    /// <summary>
    /// Gets series that have an IMDB ID.
    /// This method is synchronous because <see cref="ILibraryManager.QueryItems"/> is synchronous.
    /// Call from a background thread (e.g. <c>Task.Run</c>) if you need to avoid blocking.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of series with valid IMDB IDs.</returns>
    public IReadOnlyList<JellyfinSeriesInfo> GetSeriesWithImdbId(CancellationToken cancellationToken = default)
    {
        return GetAllSeries(cancellationToken)
            .Where(s => !string.IsNullOrEmpty(s.ImdbId))
            .ToList();
    }

    /// <summary>
    /// Gets all seasons for a specific series.
    /// </summary>
    /// <param name="seriesId">Jellyfin GUID of the series.</param>
    /// <returns>List of seasons, ordered by season number.</returns>
    public IReadOnlyList<JellyfinSeasonInfo> GetSeasonsForSeries(Guid seriesId)
    {
        var query = new InternalItemsQuery
        {
            ParentId = seriesId,
            IncludeItemTypes = new[] { BaseItemKind.Season },
            Recursive = false,
        };

        var result = _libraryManager.QueryItems(query);
        var seasons = new List<JellyfinSeasonInfo>();

        foreach (var item in result.Items)
        {
            if (item is not Season season)
            {
                continue;
            }

            var episodeCount = GetEpisodeCountForSeason(season.Id);

            seasons.Add(new JellyfinSeasonInfo
            {
                Id = season.Id,
                Name = season.Name ?? string.Empty,
                IndexNumber = season.IndexNumber,
                EpisodeCount = episodeCount,
                SeriesId = seriesId,
            });
        }

        return seasons.OrderBy(s => s.IndexNumber).ToList();
    }

    /// <summary>
    /// Gets all episodes for a specific season.
    /// </summary>
    /// <param name="seasonId">Jellyfin GUID of the season.</param>
    /// <returns>List of episodes, ordered by episode number.</returns>
    public IReadOnlyList<JellyfinEpisodeInfo> GetEpisodesForSeason(Guid seasonId)
    {
        var query = new InternalItemsQuery
        {
            ParentId = seasonId,
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            Recursive = false,
        };

        var result = _libraryManager.QueryItems(query);
        var episodes = new List<JellyfinEpisodeInfo>();

        foreach (var item in result.Items)
        {
            if (item is not Episode episode)
            {
                continue;
            }

            episodes.Add(new JellyfinEpisodeInfo
            {
                Id = episode.Id,
                Name = episode.Name ?? string.Empty,
                IndexNumber = episode.IndexNumber,
                ParentIndexNumber = episode.ParentIndexNumber,
                ImdbId = GetImdbIdFromItem(episode),
                PremiereDate = episode.PremiereDate,
                RunTimeTicks = episode.RunTimeTicks,
                Overview = episode.Overview,
                CommunityRating = episode.CommunityRating,
                SeasonId = seasonId,
                SeriesId = episode.SeriesId,
            });
        }

        return episodes;
    }

    /// <summary>
    /// Gets the total episode count for a season.
    /// </summary>
    /// <param name="seasonId">Jellyfin GUID of the season.</param>
    /// <returns>Number of episodes in the season.</returns>
    public int GetEpisodeCountForSeason(Guid seasonId)
    {
        var query = new InternalItemsQuery
        {
            ParentId = seasonId,
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            Recursive = false,
            Limit = 0,
        };

        return _libraryManager.GetCount(query);
    }

    /// <summary>
    /// Gets the IMDB ID for any Jellyfin item.
    /// </summary>
    /// <param name="itemId">Jellyfin GUID of the item.</param>
    /// <returns>IMDB ID or null.</returns>
    public string? GetImdbIdForItem(Guid itemId)
    {
        var item = _libraryManager.GetItemById(itemId);
        return item == null ? null : GetImdbIdFromItem(item);
    }

    /// <summary>
    /// Gets the total count of TV series in the library.
    /// </summary>
    /// <returns>Total series count.</returns>
    public int GetSeriesCount()
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Series },
            Recursive = true,
            Limit = 0,
        };

        return _libraryManager.GetCount(query);
    }

    private static JellyfinSeriesInfo? MapToSeriesInfo(BaseItem item)
    {
        if (item is not Series series)
        {
            return null;
        }

        return new JellyfinSeriesInfo
        {
            Id = series.Id,
            Name = series.Name ?? string.Empty,
            ImdbId = GetImdbIdFromItem(series),
            TvdbId = GetProviderId(series, MetadataProvider.Tvdb),
            ProductionYear = series.ProductionYear,
            Status = series.Status,
            Genres = series.Genres ?? Array.Empty<string>(),
            Studios = series.Studios ?? Array.Empty<string>(),
            CommunityRating = series.CommunityRating,
            Overview = series.Overview,
        };
    }

    private static string? GetImdbIdFromItem(BaseItem item)
    {
        // Try ProviderIds first (most common)
        var imdbId = GetProviderId(item, MetadataProvider.Imdb);
        if (!string.IsNullOrEmpty(imdbId))
        {
            // Ensure proper format (tt prefix)
            if (!imdbId.StartsWith("tt", StringComparison.OrdinalIgnoreCase))
            {
                imdbId = "tt" + imdbId.TrimStart('0');
            }

            return imdbId;
        }

        // Fallback: try GetUserDataKeys for Series
        if (item is Series series)
        {
            var keys = series.GetUserDataKeys();
            foreach (var key in keys)
            {
                if (key.StartsWith("tt", StringComparison.OrdinalIgnoreCase))
                {
                    return key;
                }
            }
        }

        return null;
    }

    private static string? GetProviderId(BaseItem item, MetadataProvider provider)
    {
        if (item.ProviderIds == null)
        {
            return null;
        }

        var providerName = provider.ToString();
        return item.ProviderIds.TryGetValue(providerName, out var value) ? value : null;
    }
}
