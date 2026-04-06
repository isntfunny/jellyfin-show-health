using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ShowHealth.Services.ImdbApi.Models;

/// <summary>
/// Response for listing titles.
/// </summary>
public class ListTitlesResponse
{
    [JsonPropertyName("titles")]
    public List<Title> Titles { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Response for batch getting titles.
/// </summary>
public class BatchGetTitlesResponse
{
    [JsonPropertyName("titles")]
    public List<Title> Titles { get; set; } = new();
}

/// <summary>
/// Response for searching titles.
/// </summary>
public class SearchTitlesResponse
{
    [JsonPropertyName("titles")]
    public List<Title> Titles { get; set; } = new();
}

/// <summary>
/// Response for batch getting names.
/// </summary>
public class BatchGetNamesResponse
{
    [JsonPropertyName("names")]
    public List<Name> Names { get; set; } = new();
}

/// <summary>
/// Generic error response from the API.
/// </summary>
public class ErrorResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
