using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ShowHealth.Services.TvMaze.Models;

/// <summary>
/// A TVmaze show, optionally with embedded seasons and episodes.
/// </summary>
public class TvMazeShow
{
    /// <summary>
    /// Gets or sets the TVmaze show ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the show name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the airing status ("Running", "Ended", "To Be Determined", "In Development").
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the premiere date (YYYY-MM-DD), if known.
    /// </summary>
    public string? Premiered { get; set; }

    /// <summary>
    /// Gets or sets the date of the final episode (YYYY-MM-DD), if the show has ended.
    /// </summary>
    public string? Ended { get; set; }

    /// <summary>
    /// Gets or sets the external IDs (imdb, thetvdb, tvrage).
    /// </summary>
    public TvMazeExternals? Externals { get; set; }

    /// <summary>
    /// Gets or sets the embedded seasons and episodes.
    /// </summary>
    [JsonPropertyName("_embedded")]
    public TvMazeShowEmbedded? Embedded { get; set; }
}

/// <summary>
/// External provider IDs for a TVmaze show.
/// </summary>
public class TvMazeExternals
{
    /// <summary>
    /// Gets or sets the IMDb title ID (e.g. "tt0903747").
    /// </summary>
    public string? Imdb { get; set; }

    /// <summary>
    /// Gets or sets the TheTVDB series ID.
    /// </summary>
    public int? Thetvdb { get; set; }
}

/// <summary>
/// Embedded sub-resources returned via the TVmaze embed[] query parameter.
/// </summary>
public class TvMazeShowEmbedded
{
    /// <summary>
    /// Gets or sets the seasons of the show.
    /// </summary>
    public IReadOnlyList<TvMazeSeason> Seasons { get; set; } = Array.Empty<TvMazeSeason>();

    /// <summary>
    /// Gets or sets the episodes of the show (specials excluded).
    /// </summary>
    public IReadOnlyList<TvMazeEpisode> Episodes { get; set; } = Array.Empty<TvMazeEpisode>();
}

/// <summary>
/// A single season of a TVmaze show.
/// </summary>
public class TvMazeSeason
{
    /// <summary>
    /// Gets or sets the TVmaze season ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the announced number of episodes, if known.
    /// Null for seasons that have been announced but not yet scheduled.
    /// </summary>
    public int? EpisodeOrder { get; set; }

    /// <summary>
    /// Gets or sets the premiere date of the season (YYYY-MM-DD), if known.
    /// </summary>
    public string? PremiereDate { get; set; }

    /// <summary>
    /// Gets or sets the end date of the season (YYYY-MM-DD), if known.
    /// </summary>
    public string? EndDate { get; set; }
}

/// <summary>
/// A single episode of a TVmaze show.
/// </summary>
public class TvMazeEpisode
{
    /// <summary>
    /// Gets or sets the TVmaze episode ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    public int? Season { get; set; }

    /// <summary>
    /// Gets or sets the episode number within the season.
    /// Null for specials and unnumbered entries.
    /// </summary>
    public int? Number { get; set; }

    /// <summary>
    /// Gets or sets the episode title.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the air date (YYYY-MM-DD). Empty or null when unscheduled.
    /// </summary>
    public string? Airdate { get; set; }
}
