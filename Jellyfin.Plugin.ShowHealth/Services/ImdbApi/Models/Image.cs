using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ShowHealth.Services.ImdbApi.Models;

/// <summary>
/// Represents an image associated with a title or person.
/// </summary>
public class Image
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
