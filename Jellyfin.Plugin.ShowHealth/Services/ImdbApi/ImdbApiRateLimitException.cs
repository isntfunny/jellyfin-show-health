using System;

namespace Jellyfin.Plugin.ShowHealth.Services.ImdbApi;

/// <summary>
/// Exception thrown when the IMDb API returns a 429 Too Many Requests.
/// </summary>
public class ImdbApiRateLimitException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImdbApiRateLimitException"/> class.
    /// </summary>
    /// <param name="message">Exception message.</param>
    public ImdbApiRateLimitException(string message)
        : base(message)
    {
    }
}
