using System;

namespace Jellyfin.Plugin.ShowHealth.Services.ImdbApi;

/// <summary>
/// Exception thrown when the IMDb API returns an error response.
/// </summary>
public class ImdbApiException : Exception
{
    /// <summary>
    /// Gets the HTTP status code from the API response.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImdbApiException"/> class.
    /// </summary>
    /// <param name="statusCode">HTTP status code.</param>
    /// <param name="message">Exception message.</param>
    public ImdbApiException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
