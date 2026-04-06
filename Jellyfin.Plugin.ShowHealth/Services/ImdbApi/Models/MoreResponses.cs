using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ShowHealth.Services.ImdbApi.Models;

/// <summary>
/// Response for listing episodes of a title.
/// </summary>
public class ListTitleEpisodesResponse
{
    [JsonPropertyName("episodes")]
    public List<Episode> Episodes { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Represents an episode of a TV series.
/// </summary>
public class Episode
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("primaryImage")]
    public Image? PrimaryImage { get; set; }

    [JsonPropertyName("season")]
    public string? Season { get; set; }

    [JsonPropertyName("episodeNumber")]
    public int EpisodeNumber { get; set; }

    [JsonPropertyName("runtimeSeconds")]
    public int RuntimeSeconds { get; set; }

    [JsonPropertyName("plot")]
    public string? Plot { get; set; }

    [JsonPropertyName("rating")]
    public Rating? Rating { get; set; }

    [JsonPropertyName("releaseDate")]
    public PrecisionDate? ReleaseDate { get; set; }
}

/// <summary>
/// Response for listing images of a title.
/// </summary>
public class ListTitleImagesResponse
{
    [JsonPropertyName("images")]
    public List<Image> Images { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Response for listing videos of a title.
/// </summary>
public class ListTitleVideosResponse
{
    [JsonPropertyName("videos")]
    public List<Video> Videos { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Represents a video associated with a title.
/// </summary>
public class Video
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("primaryImage")]
    public Image? PrimaryImage { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("runtimeSeconds")]
    public int RuntimeSeconds { get; set; }
}

/// <summary>
/// Response for listing credits of a title.
/// </summary>
public class ListTitleCreditsResponse
{
    [JsonPropertyName("credits")]
    public List<Credit> Credits { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Represents a credit (person-role association) for a title.
/// </summary>
public class Credit
{
    [JsonPropertyName("title")]
    public Title? TitleObj { get; set; }

    [JsonPropertyName("name")]
    public Name? Name { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("characters")]
    public List<string> Characters { get; set; } = new();

    [JsonPropertyName("episodeCount")]
    public int EpisodeCount { get; set; }
}

/// <summary>
/// Response for listing award nominations of a title.
/// </summary>
public class ListTitleAwardNominationsResponse
{
    [JsonPropertyName("stats")]
    public AwardNominationStats? Stats { get; set; }

    [JsonPropertyName("awardNominations")]
    public List<AwardNomination> AwardNominations { get; set; } = new();

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Statistics about award nominations.
/// </summary>
public class AwardNominationStats
{
    [JsonPropertyName("nominationCount")]
    public int NominationCount { get; set; }

    [JsonPropertyName("winCount")]
    public int WinCount { get; set; }
}

/// <summary>
/// Represents an award nomination.
/// </summary>
public class AwardNomination
{
    [JsonPropertyName("titles")]
    public List<Title> Titles { get; set; } = new();

    [JsonPropertyName("nominees")]
    public List<Name> Nominees { get; set; } = new();

    [JsonPropertyName("event")]
    public Event? Event { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("isWinner")]
    public bool IsWinner { get; set; }

    [JsonPropertyName("winnerRank")]
    public int WinnerRank { get; set; }
}

/// <summary>
/// Represents an award event.
/// </summary>
public class Event
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
/// Response for listing release dates of a title.
/// </summary>
public class ListTitleReleaseDatesResponse
{
    [JsonPropertyName("releaseDates")]
    public List<ReleaseDate> ReleaseDates { get; set; } = new();

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Represents a release date in a specific country.
/// </summary>
public class ReleaseDate
{
    [JsonPropertyName("country")]
    public Country? Country { get; set; }

    [JsonPropertyName("releaseDate")]
    public PrecisionDate? ReleaseDateValue { get; set; }

    [JsonPropertyName("attributes")]
    public List<string> Attributes { get; set; } = new();
}

/// <summary>
/// Response for listing AKAs of a title.
/// </summary>
public class ListTitleAKAsResponse
{
    [JsonPropertyName("akas")]
    public List<AKA> Akas { get; set; } = new();
}

/// <summary>
/// Represents an alternative title (AKA).
/// </summary>
public class AKA
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("country")]
    public Country? Country { get; set; }

    [JsonPropertyName("language")]
    public Language? Language { get; set; }

    [JsonPropertyName("attributes")]
    public List<string> Attributes { get; set; } = new();
}

/// <summary>
/// Response for listing seasons of a title.
/// </summary>
public class ListTitleSeasonsResponse
{
    [JsonPropertyName("seasons")]
    public List<Season> Seasons { get; set; } = new();
}

/// <summary>
/// Represents a season of a TV series.
/// </summary>
public class Season
{
    [JsonPropertyName("season")]
    public string? SeasonNumber { get; set; }

    [JsonPropertyName("episodeCount")]
    public int EpisodeCount { get; set; }
}

/// <summary>
/// Response for listing parents guide of a title.
/// </summary>
public class ListTitleParentsGuideResponse
{
    [JsonPropertyName("parentsGuide")]
    public List<ParentsGuide> ParentsGuide { get; set; } = new();
}

/// <summary>
/// Represents a parents guide entry.
/// </summary>
public class ParentsGuide
{
    [JsonPropertyName("category")]
    public ParentsGuideCategory? Category { get; set; }

    [JsonPropertyName("severityBreakdowns")]
    public List<ParentsGuideSeverity> SeverityBreakdowns { get; set; } = new();

    [JsonPropertyName("reviews")]
    public List<ParentsGuideReview> Reviews { get; set; } = new();
}

/// <summary>
/// Parents guide category enum.
/// Values match the API's SCREAMING_SNAKE_CASE string representation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ParentsGuideCategory
{
    [JsonStringEnumMemberName("SEXUAL_CONTENT")]
    SexualContent,

    [JsonStringEnumMemberName("VIOLENCE")]
    Violence,

    [JsonStringEnumMemberName("PROFANITY")]
    Profanity,

    [JsonStringEnumMemberName("ALCOHOL_DRUGS")]
    AlcoholDrugs,

    [JsonStringEnumMemberName("FRIGHTENING_INTENSE_SCENES")]
    FrighteningIntenseScenes,
}

/// <summary>
/// Represents a severity breakdown in parents guide.
/// </summary>
public class ParentsGuideSeverity
{
    [JsonPropertyName("severityLevel")]
    public string? SeverityLevel { get; set; }

    [JsonPropertyName("voteCount")]
    public int VoteCount { get; set; }
}

/// <summary>
/// Represents a parents guide review.
/// </summary>
public class ParentsGuideReview
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("isSpoiler")]
    public bool IsSpoiler { get; set; }
}

/// <summary>
/// Response for listing certificates of a title.
/// </summary>
public class ListTitleCertificatesResponse
{
    [JsonPropertyName("certificates")]
    public List<Certificate> Certificates { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}

/// <summary>
/// Represents a content rating certificate.
/// </summary>
public class Certificate
{
    [JsonPropertyName("rating")]
    public string? Rating { get; set; }

    [JsonPropertyName("country")]
    public Country? Country { get; set; }

    [JsonPropertyName("attributes")]
    public List<string> Attributes { get; set; } = new();
}

/// <summary>
/// Response for listing company credits of a title.
/// </summary>
public class ListTitleCompanyCreditsResponse
{
    [JsonPropertyName("companyCredits")]
    public List<CompanyCredit> CompanyCredits { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Represents a company credit.
/// </summary>
public class CompanyCredit
{
    [JsonPropertyName("company")]
    public Company? Company { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("countries")]
    public List<Country> Countries { get; set; } = new();

    [JsonPropertyName("yearsInvolved")]
    public YearsInvolved? YearsInvolved { get; set; }

    [JsonPropertyName("attributes")]
    public List<string> Attributes { get; set; } = new();
}

/// <summary>
/// Represents a company.
/// </summary>
public class Company
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
/// Represents years a company was involved in a project.
/// </summary>
public class YearsInvolved
{
    [JsonPropertyName("startYear")]
    public int StartYear { get; set; }

    [JsonPropertyName("endYear")]
    public int EndYear { get; set; }
}

/// <summary>
/// Response for listing filmography of a name.
/// </summary>
public class ListNameFilmographyResponse
{
    [JsonPropertyName("credits")]
    public List<Credit> Credits { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Response for listing images of a name.
/// </summary>
public class ListNameImagesResponse
{
    [JsonPropertyName("images")]
    public List<Image> Images { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Response for listing relationships of a name.
/// </summary>
public class ListNameRelationshipsResponse
{
    [JsonPropertyName("relationships")]
    public List<NameRelationship> Relationships { get; set; } = new();
}

/// <summary>
/// Represents a relationship between two names.
/// </summary>
public class NameRelationship
{
    [JsonPropertyName("name")]
    public Name? Name { get; set; }

    [JsonPropertyName("relationType")]
    public string? RelationType { get; set; }

    [JsonPropertyName("attributes")]
    public List<string> Attributes { get; set; } = new();
}

/// <summary>
/// Response for listing trivia of a name.
/// </summary>
public class ListNameTriviaResponse
{
    [JsonPropertyName("triviaEntries")]
    public List<NameTrivia> TriviaEntries { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Represents a trivia fact about a person.
/// </summary>
public class NameTrivia
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("interestCount")]
    public int InterestCount { get; set; }

    [JsonPropertyName("voteCount")]
    public int VoteCount { get; set; }
}

/// <summary>
/// Response for listing star meter rankings.
/// </summary>
public class ListStarMetersResponse
{
    [JsonPropertyName("names")]
    public List<Name> Names { get; set; } = new();

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

/// <summary>
/// Response for listing interest categories.
/// </summary>
public class ListInterestCategoriesResponse
{
    [JsonPropertyName("categories")]
    public List<InterestCategory> Categories { get; set; } = new();
}

/// <summary>
/// Represents a category of interests.
/// </summary>
public class InterestCategory
{
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("interests")]
    public List<Interest> Interests { get; set; } = new();
}

/// <summary>
/// Represents box office information.
/// </summary>
public class BoxOffice
{
    [JsonPropertyName("domesticGross")]
    public Money? DomesticGross { get; set; }

    [JsonPropertyName("worldwideGross")]
    public Money? WorldwideGross { get; set; }

    [JsonPropertyName("openingWeekendGross")]
    public OpeningWeekendGross? OpeningWeekendGross { get; set; }

    [JsonPropertyName("productionBudget")]
    public Money? ProductionBudget { get; set; }
}

/// <summary>
/// Represents a monetary amount.
/// </summary>
public class Money
{
    [JsonPropertyName("amount")]
    public string? Amount { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }
}

/// <summary>
/// Represents opening weekend gross information.
/// </summary>
public class OpeningWeekendGross
{
    [JsonPropertyName("gross")]
    public Money? Gross { get; set; }

    [JsonPropertyName("weekendEndDate")]
    public PrecisionDate? WeekendEndDate { get; set; }
}
