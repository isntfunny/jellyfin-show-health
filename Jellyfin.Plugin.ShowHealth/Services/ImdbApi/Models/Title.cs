using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ShowHealth.Services.ImdbApi.Models;

/// <summary>
/// Represents a title (movie, TV show, etc.) from IMDb.
/// </summary>
public class Title
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("isAdult")]
    public bool IsAdult { get; set; }

    [JsonPropertyName("primaryTitle")]
    public string? PrimaryTitle { get; set; }

    [JsonPropertyName("originalTitle")]
    public string? OriginalTitle { get; set; }

    [JsonPropertyName("primaryImage")]
    public Image? PrimaryImage { get; set; }

    [JsonPropertyName("startYear")]
    public int StartYear { get; set; }

    [JsonPropertyName("endYear")]
    public int EndYear { get; set; }

    [JsonPropertyName("runtimeSeconds")]
    public int RuntimeSeconds { get; set; }

    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();

    [JsonPropertyName("rating")]
    public Rating? Rating { get; set; }

    [JsonPropertyName("metacritic")]
    public Metacritic? Metacritic { get; set; }

    [JsonPropertyName("plot")]
    public string? Plot { get; set; }

    [JsonPropertyName("directors")]
    public List<Name> Directors { get; set; } = new();

    [JsonPropertyName("writers")]
    public List<Name> Writers { get; set; } = new();

    [JsonPropertyName("stars")]
    public List<Name> Stars { get; set; } = new();

    [JsonPropertyName("originCountries")]
    public List<Country> OriginCountries { get; set; } = new();

    [JsonPropertyName("spokenLanguages")]
    public List<Language> SpokenLanguages { get; set; } = new();

    [JsonPropertyName("interests")]
    public List<Interest> Interests { get; set; } = new();
}

/// <summary>
/// Represents a person (actor, director, etc.) from IMDb.
/// </summary>
public class Name
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("alternativeNames")]
    public List<string> AlternativeNames { get; set; } = new();

    [JsonPropertyName("primaryImage")]
    public Image? PrimaryImage { get; set; }

    [JsonPropertyName("primaryProfessions")]
    public List<string> PrimaryProfessions { get; set; } = new();

    [JsonPropertyName("biography")]
    public string? Biography { get; set; }

    [JsonPropertyName("heightCm")]
    public int HeightCm { get; set; }

    [JsonPropertyName("birthName")]
    public string? BirthName { get; set; }

    [JsonPropertyName("birthDate")]
    public PrecisionDate? BirthDate { get; set; }

    [JsonPropertyName("birthLocation")]
    public string? BirthLocation { get; set; }

    [JsonPropertyName("deathDate")]
    public PrecisionDate? DeathDate { get; set; }

    [JsonPropertyName("deathLocation")]
    public string? DeathLocation { get; set; }

    [JsonPropertyName("deathReason")]
    public string? DeathReason { get; set; }

    [JsonPropertyName("meterRanking")]
    public NameMeterRanking? MeterRanking { get; set; }
}

/// <summary>
/// Represents Metacritic information for a title.
/// </summary>
public class Metacritic
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("reviewCount")]
    public int ReviewCount { get; set; }
}

/// <summary>
/// Represents a language with ISO code and name.
/// </summary>
public class Language
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
/// Represents an interest/genre category.
/// </summary>
public class Interest
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("primaryImage")]
    public Image? PrimaryImage { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isSubgenre")]
    public bool IsSubgenre { get; set; }

    [JsonPropertyName("similarInterests")]
    public List<Interest> SimilarInterests { get; set; } = new();
}

/// <summary>
/// Represents a popularity meter ranking for a person.
/// </summary>
public class NameMeterRanking
{
    [JsonPropertyName("currentRank")]
    public int CurrentRank { get; set; }

    [JsonPropertyName("changeDirection")]
    public string? ChangeDirection { get; set; }

    [JsonPropertyName("difference")]
    public int Difference { get; set; }
}
