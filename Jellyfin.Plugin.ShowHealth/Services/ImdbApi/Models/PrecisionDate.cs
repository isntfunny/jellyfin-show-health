using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ShowHealth.Services.ImdbApi.Models;

/// <summary>
/// Represents a release date with optional precision (year, month, day).
/// </summary>
public class PrecisionDate
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("month")]
    public int Month { get; set; }

    [JsonPropertyName("day")]
    public int Day { get; set; }
}
