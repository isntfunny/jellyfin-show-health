using System;

namespace Jellyfin.Plugin.ShowHealth.Services.TvMaze;

/// <summary>
/// Exception thrown when the TVmaze API returns a 429 Too Many Requests.
/// </summary>
public class TvMazeRateLimitException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TvMazeRateLimitException"/> class.
    /// </summary>
    /// <param name="message">Exception message.</param>
    public TvMazeRateLimitException(string message)
        : base(message)
    {
    }
}
