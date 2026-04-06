using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ShowHealth.Services.ImdbApi.Models;

/// <summary>
/// Represents a rating with aggregate score and vote count.
/// </summary>
public class Rating
{
    [JsonPropertyName("aggregateRating")]
    public float AggregateRating { get; set; }

    [JsonPropertyName("voteCount")]
    public int VoteCount { get; set; }
}
