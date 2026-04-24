using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ShowHealth.Models;

/// <summary>
/// Top-level response for the Show Health status endpoint.
/// </summary>
public class ShowHealthResponse
{
    /// <summary>
    /// Gets or sets the list of series health results.
    /// </summary>
    [JsonPropertyName("series")]
    public List<SeriesHealthResult> Series { get; set; } = new();

    /// <summary>
    /// Gets or sets the summary statistics across all series.
    /// </summary>
    [JsonPropertyName("summary")]
    public HealthSummary Summary { get; set; } = new();
}

/// <summary>
/// Health status for a single TV series.
/// </summary>
public class SeriesHealthResult
{
    /// <summary>
    /// Gets or sets the series name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin item ID.
    /// </summary>
    [JsonPropertyName("jellyfinId")]
    public string JellyfinId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the IMDb ID.
    /// </summary>
    [JsonPropertyName("imdbId")]
    public string ImdbId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TVDB ID.
    /// </summary>
    [JsonPropertyName("tvdbId")]
    public string? TvdbId { get; set; }

    /// <summary>
    /// Gets or sets the genres.
    /// </summary>
    [JsonPropertyName("genres")]
    public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the production studios.
    /// </summary>
    [JsonPropertyName("studios")]
    public IReadOnlyList<string> Studios { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the community rating (0-10).
    /// </summary>
    [JsonPropertyName("communityRating")]
    public float? CommunityRating { get; set; }

    /// <summary>
    /// Gets or sets the series overview/plot summary.
    /// </summary>
    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    /// <summary>
    /// Gets or sets the year the series started.
    /// </summary>
    [JsonPropertyName("startYear")]
    public int StartYear { get; set; }

    /// <summary>
    /// Gets or sets the year the series ended, if applicable.
    /// </summary>
    [JsonPropertyName("endYear")]
    public int? EndYear { get; set; }

    /// <summary>
    /// Gets or sets the series status (e.g. "running", "ended").
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = ShowStatus.Unknown;

    /// <summary>
    /// Gets or sets the number of seasons present locally.
    /// </summary>
    [JsonPropertyName("seasonsLocal")]
    public int SeasonsLocal { get; set; }

    /// <summary>
    /// Gets the total number of seasons according to the reference source.
    /// Derived as local seasons plus confirmed missing seasons (excludes not-yet-aired).
    /// </summary>
    [JsonPropertyName("seasonsTotal")]
    public int SeasonsTotal => SeasonsLocal + MissingSeasons.Count;

    /// <summary>
    /// Gets or sets the list of seasons that are missing locally.
    /// </summary>
    [JsonPropertyName("missingSeasons")]
    public List<MissingSeasonInfo> MissingSeasons { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of individual missing episodes.
    /// </summary>
    [JsonPropertyName("missingEpisodes")]
    public List<MissingEpisodeInfo> MissingEpisodes { get; set; } = new();

    /// <summary>
    /// Gets or sets info about the next upcoming episode, if any.
    /// </summary>
    [JsonPropertyName("nextEpisode")]
    public NextEpisodeInfo? NextEpisode { get; set; }
}

/// <summary>
/// Info about a missing season.
/// </summary>
public class MissingSeasonInfo
{
    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    [JsonPropertyName("season")]
    public int Season { get; set; }

    /// <summary>
    /// Gets or sets the total episode count for this season according to IMDb.
    /// </summary>
    [JsonPropertyName("episodeCount")]
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Gets or sets whether this season is a "gap" (between present seasons) vs trailing at the end.
    /// </summary>
    [JsonPropertyName("isGap")]
    public bool IsGap { get; set; }

    /// <summary>
    /// Gets or sets the individual episodes in this missing season (for CSV export).
    /// </summary>
    [JsonPropertyName("episodes")]
    public List<MissingEpisodeInfo> Episodes { get; set; } = new();
}

/// <summary>
/// Info about a missing episode.
/// </summary>
public class MissingEpisodeInfo
{
    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    [JsonPropertyName("season")]
    public int Season { get; set; }

    /// <summary>
    /// Gets or sets the episode number within the season.
    /// </summary>
    [JsonPropertyName("episode")]
    public int Episode { get; set; }

    /// <summary>
    /// Gets or sets the episode title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the IMDb ID for this episode, if available.
    /// </summary>
    [JsonPropertyName("imdbId")]
    public string? ImdbId { get; set; }

    /// <summary>
    /// Gets or sets whether this episode is a "gap" (missing inside a present season) vs trailing.
    /// Episodes in present seasons are always gaps. Episodes in trailing seasons are not.
    /// </summary>
    [JsonPropertyName("isGap")]
    public bool IsGap { get; set; }
}

/// <summary>
/// Info about the next upcoming episode.
/// </summary>
public class NextEpisodeInfo
{
    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    [JsonPropertyName("season")]
    public int Season { get; set; }

    /// <summary>
    /// Gets or sets the episode number within the season.
    /// </summary>
    [JsonPropertyName("episode")]
    public int Episode { get; set; }

    /// <summary>
    /// Gets or sets the episode title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release date of the episode, if known.
    /// </summary>
    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; set; }
}

/// <summary>
/// Response for the series list endpoint (Jellyfin-only data, no IMDb).
/// </summary>
public class SeriesListResponse
{
    /// <summary>
    /// Gets or sets the list of series.
    /// </summary>
    [JsonPropertyName("series")]
    public List<SeriesListItem> Series { get; set; } = new();
}

/// <summary>
/// Basic series info from Jellyfin (before IMDb analysis).
/// </summary>
public class SeriesListItem
{
    /// <summary>
    /// Gets or sets the series name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin item ID.
    /// </summary>
    [JsonPropertyName("jellyfinId")]
    public string JellyfinId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the IMDb ID.
    /// </summary>
    [JsonPropertyName("imdbId")]
    public string ImdbId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the year the series started.
    /// </summary>
    [JsonPropertyName("startYear")]
    public int StartYear { get; set; }
}

/// <summary>
/// An entry in the ignored series list.
/// </summary>
public class IgnoredSeriesEntry
{
    /// <summary>
    /// Gets or sets the IMDb ID.
    /// </summary>
    [JsonPropertyName("imdbId")]
    public string ImdbId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the series name (for display in the management dialog).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// String constants for series status values.
/// </summary>
public static class ShowStatus
{
    /// <summary>Series is currently airing.</summary>
    public const string Running = "running";

    /// <summary>Series has concluded.</summary>
    public const string Ended = "ended";

    /// <summary>Status could not be determined.</summary>
    public const string Unknown = "unknown";
}

/// <summary>
/// Summary statistics across all series.
/// </summary>
public class HealthSummary
{
    /// <summary>
    /// Gets or sets the total number of series evaluated.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the number of series with missing content.
    /// </summary>
    [JsonPropertyName("incomplete")]
    public int Incomplete { get; set; }

    /// <summary>
    /// Gets or sets the number of currently running series.
    /// </summary>
    [JsonPropertyName("running")]
    public int Running { get; set; }

    /// <summary>
    /// Gets or sets the number of ended series.
    /// </summary>
    [JsonPropertyName("ended")]
    public int Ended { get; set; }
}
