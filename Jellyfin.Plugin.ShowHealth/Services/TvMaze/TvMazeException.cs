using System;

namespace Jellyfin.Plugin.ShowHealth.Services.TvMaze;

/// <summary>
/// Exception thrown when the TVmaze API returns an error response.
/// </summary>
public class TvMazeException : Exception
{
    /// <summary>
    /// Gets the HTTP status code from the API response.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TvMazeException"/> class.
    /// </summary>
    /// <param name="statusCode">HTTP status code.</param>
    /// <param name="message">Exception message.</param>
    public TvMazeException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
