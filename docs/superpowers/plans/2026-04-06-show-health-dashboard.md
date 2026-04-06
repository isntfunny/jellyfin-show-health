# Show Health Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Jellyfin plugin dashboard that compares local TV series against IMDb data and reports missing episodes, seasons, and series status.

**Architecture:** REST Controller serves JSON to a plain-JS frontend. `ShowHealthAnalyzer` compares Jellyfin library data (always fresh) with IMDb API data (cached). A Scheduled Task runs the analysis periodically and logs activity when changes are detected. Frontend uses ES classes for clean separation.

**Tech Stack:** C# / .NET 9.0 / Jellyfin Plugin SDK, Plain JavaScript (ES modules, classes), ASP.NET Core ControllerBase

---

## File Structure

```
Jellyfin.Plugin.ShowHealth/
├── Models/
│   └── SeriesHealthResult.cs      — Response DTOs (SeriesHealthResult, MissingEpisodeInfo, etc.)
├── Services/
│   ├── ShowHealthAnalyzer.cs      — Core comparison logic (Jellyfin vs IMDb)
│   ├── ImdbApi/                   — (existing) IMDb API client, cache, rate limiter
│   └── Jellyfin/                  — (existing) JellyfinLibraryService
├── Api/
│   └── ShowHealthController.cs    — REST endpoint GET /ShowHealth/Status
├── Tasks/
│   └── ShowHealthScanTask.cs      — IScheduledTask, periodic scan + activity log
├── Web/
│   ├── showhealth.html            — Dashboard page (modify existing)
│   └── showhealth.js              — JS classes (modify existing)
└── Plugin.cs                      — (existing, no changes needed)
```

---

### Task 1: Response Models

**Files:**
- Create: `Jellyfin.Plugin.ShowHealth/Models/SeriesHealthResult.cs`

- [ ] **Step 1: Create the response model file**

```csharp
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.ShowHealth.Models;

/// <summary>
/// Top-level response for the Show Health status endpoint.
/// </summary>
public class ShowHealthResponse
{
    [JsonPropertyName("series")]
    public List<SeriesHealthResult> Series { get; set; } = new();

    [JsonPropertyName("summary")]
    public HealthSummary Summary { get; set; } = new();
}

/// <summary>
/// Health status for a single TV series.
/// </summary>
public class SeriesHealthResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("jellyfinId")]
    public string JellyfinId { get; set; } = string.Empty;

    [JsonPropertyName("imdbId")]
    public string ImdbId { get; set; } = string.Empty;

    [JsonPropertyName("startYear")]
    public int StartYear { get; set; }

    [JsonPropertyName("endYear")]
    public int? EndYear { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "unknown";

    [JsonPropertyName("seasonsLocal")]
    public int SeasonsLocal { get; set; }

    [JsonPropertyName("seasonsTotal")]
    public int SeasonsTotal { get; set; }

    [JsonPropertyName("missingSeasons")]
    public List<int> MissingSeasons { get; set; } = new();

    [JsonPropertyName("missingEpisodes")]
    public List<MissingEpisodeInfo> MissingEpisodes { get; set; } = new();

    [JsonPropertyName("nextEpisode")]
    public NextEpisodeInfo? NextEpisode { get; set; }
}

/// <summary>
/// Info about a missing episode.
/// </summary>
public class MissingEpisodeInfo
{
    [JsonPropertyName("season")]
    public int Season { get; set; }

    [JsonPropertyName("episode")]
    public int Episode { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("imdbId")]
    public string? ImdbId { get; set; }
}

/// <summary>
/// Info about the next upcoming episode.
/// </summary>
public class NextEpisodeInfo
{
    [JsonPropertyName("season")]
    public int Season { get; set; }

    [JsonPropertyName("episode")]
    public int Episode { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("releaseDate")]
    public string? ReleaseDate { get; set; }
}

/// <summary>
/// Summary statistics across all series.
/// </summary>
public class HealthSummary
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("incomplete")]
    public int Incomplete { get; set; }

    [JsonPropertyName("running")]
    public int Running { get; set; }

    [JsonPropertyName("ended")]
    public int Ended { get; set; }
}
```

- [ ] **Step 2: Build to verify compilation**

Run: `dotnet build Jellyfin.Plugin.ShowHealth.sln`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit**

```bash
git add Jellyfin.Plugin.ShowHealth/Models/SeriesHealthResult.cs
git commit -m "feat: add response models for Show Health API"
```

---

### Task 2: ShowHealthAnalyzer — DI Registration

The `ImdbApiClient` needs `IHttpClientFactory` and a cache directory. The `ShowHealthAnalyzer` and `JellyfinLibraryService` need to be registered in DI. Create a service registrator.

**Files:**
- Create: `Jellyfin.Plugin.ShowHealth/ShowHealthServiceRegistrator.cs`

- [ ] **Step 1: Create the service registrator**

```csharp
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Jellyfin.Plugin.ShowHealth.Services;
using Jellyfin.Plugin.ShowHealth.Services.ImdbApi;
using Jellyfin.Plugin.ShowHealth.Services.Jellyfin;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ShowHealth;

/// <summary>
/// Registers plugin services into the Jellyfin DI container.
/// </summary>
public class ShowHealthServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ImdbApiRateLimiter>();
        serviceCollection.AddSingleton<ImdbApiClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var appPaths = sp.GetRequiredService<IServerApplicationPaths>();
            var cacheDir = Path.Combine(appPaths.PluginConfigurationsPath, "ShowHealth", "cache");
            var rateLimiter = sp.GetRequiredService<ImdbApiRateLimiter>();
            return new ImdbApiClient(httpClientFactory, cacheDir, rateLimiter);
        });
        serviceCollection.AddScoped<JellyfinLibraryService>();
        serviceCollection.AddScoped<ShowHealthAnalyzer>();
    }
}
```

- [ ] **Step 2: Add missing using for System.IO.Path**

Ensure the file has `using System.IO;` at the top if `Path.Combine` is used. Check the `IServerApplicationPaths` import comes from `MediaBrowser.Controller`. Verify by building.

- [ ] **Step 3: Build to verify**

Run: `dotnet build Jellyfin.Plugin.ShowHealth.sln`
Expected: Build succeeded, 0 errors

- [ ] **Step 4: Commit**

```bash
git add Jellyfin.Plugin.ShowHealth/ShowHealthServiceRegistrator.cs
git commit -m "feat: add DI service registrator for Show Health"
```

---

### Task 3: ShowHealthAnalyzer — Core Comparison Logic

**Files:**
- Create: `Jellyfin.Plugin.ShowHealth/Services/ShowHealthAnalyzer.cs`

- [ ] **Step 1: Create the analyzer**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ShowHealth.Models;
using Jellyfin.Plugin.ShowHealth.Services.ImdbApi;
using Jellyfin.Plugin.ShowHealth.Services.ImdbApi.Models;
using Jellyfin.Plugin.ShowHealth.Services.Jellyfin;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShowHealth.Services;

/// <summary>
/// Compares Jellyfin library data against IMDb to find missing episodes and seasons.
/// </summary>
public class ShowHealthAnalyzer
{
    private readonly JellyfinLibraryService _libraryService;
    private readonly ImdbApiClient _imdbClient;
    private readonly ILogger<ShowHealthAnalyzer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowHealthAnalyzer"/> class.
    /// </summary>
    public ShowHealthAnalyzer(
        JellyfinLibraryService libraryService,
        ImdbApiClient imdbClient,
        ILogger<ShowHealthAnalyzer> logger)
    {
        _libraryService = libraryService;
        _imdbClient = imdbClient;
        _logger = logger;
    }

    /// <summary>
    /// Analyzes all series in the Jellyfin library and returns health status.
    /// </summary>
    public async Task<ShowHealthResponse> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var seriesList = _libraryService.GetSeriesWithImdbId(cancellationToken);
        _logger.LogInformation("Analyzing {Count} series with IMDb IDs", seriesList.Count);

        var results = new List<SeriesHealthResult>();

        foreach (var series in seriesList)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var result = await AnalyzeSeriesAsync(series, cancellationToken).ConfigureAwait(false);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze series {Name} ({ImdbId})", series.Name, series.ImdbId);
            }
        }

        var response = new ShowHealthResponse
        {
            Series = results,
            Summary = new HealthSummary
            {
                Total = results.Count,
                Incomplete = results.Count(r => r.MissingEpisodes.Count > 0 || r.MissingSeasons.Count > 0),
                Running = results.Count(r => r.Status == "running"),
                Ended = results.Count(r => r.Status == "ended"),
            },
        };

        return response;
    }

    private async Task<SeriesHealthResult> AnalyzeSeriesAsync(JellyfinSeriesInfo series, CancellationToken cancellationToken)
    {
        var imdbId = series.ImdbId!;

        // Fetch IMDb data
        var titleTask = _imdbClient.GetTitleAsync(imdbId, cancellationToken);
        var seasonsTask = _imdbClient.ListTitleSeasonsAsync(imdbId, cancellationToken);
        await Task.WhenAll(titleTask, seasonsTask).ConfigureAwait(false);

        var title = await titleTask.ConfigureAwait(false);
        var imdbSeasons = await seasonsTask.ConfigureAwait(false);

        // Fetch local seasons
        var localSeasons = _libraryService.GetSeasonsForSeries(series.Id);

        // Determine which season numbers exist locally (excluding Specials = season 0)
        var localSeasonNumbers = localSeasons
            .Where(s => s.IndexNumber.HasValue && s.IndexNumber.Value > 0)
            .Select(s => s.IndexNumber!.Value)
            .ToHashSet();

        // Parse IMDb season numbers
        var imdbSeasonNumbers = imdbSeasons.Seasons
            .Select(s => int.TryParse(s.SeasonNumber, out var n) ? n : -1)
            .Where(n => n > 0)
            .ToList();

        // Find missing seasons
        var missingSeasonNumbers = imdbSeasonNumbers
            .Where(n => !localSeasonNumbers.Contains(n))
            .ToList();

        // Find missing episodes per season
        var missingEpisodes = new List<MissingEpisodeInfo>();
        NextEpisodeInfo? nextEpisode = null;
        var now = DateTime.UtcNow;

        foreach (var seasonNum in imdbSeasonNumbers)
        {
            var imdbEpisodes = await _imdbClient.ListTitleEpisodesAsync(
                imdbId,
                season: seasonNum.ToString(CultureInfo.InvariantCulture),
                pageSize: 50,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Check for next upcoming episode (across all seasons)
            foreach (var ep in imdbEpisodes.Episodes)
            {
                if (ep.ReleaseDate != null && IsInFuture(ep.ReleaseDate, now))
                {
                    var candidate = new NextEpisodeInfo
                    {
                        Season = seasonNum,
                        Episode = ep.EpisodeNumber,
                        Title = ep.Title ?? "TBA",
                        ReleaseDate = FormatDate(ep.ReleaseDate),
                    };

                    if (nextEpisode == null ||
                        string.Compare(candidate.ReleaseDate, nextEpisode.ReleaseDate, StringComparison.Ordinal) < 0)
                    {
                        nextEpisode = candidate;
                    }
                }
            }

            // If the whole season is missing locally, list all aired episodes as missing
            if (missingSeasonNumbers.Contains(seasonNum))
            {
                foreach (var ep in imdbEpisodes.Episodes)
                {
                    if (ep.ReleaseDate == null || !IsInFuture(ep.ReleaseDate, now))
                    {
                        missingEpisodes.Add(new MissingEpisodeInfo
                        {
                            Season = seasonNum,
                            Episode = ep.EpisodeNumber,
                            Title = ep.Title ?? "TBA",
                            ImdbId = ep.Id,
                        });
                    }
                }

                continue;
            }

            // Season exists locally — find missing episodes within it
            if (!localSeasonNumbers.Contains(seasonNum))
            {
                continue;
            }

            var localSeason = localSeasons.First(s => s.IndexNumber == seasonNum);
            var localEpisodes = _libraryService.GetEpisodesForSeason(localSeason.Id);
            var localEpisodeNumbers = localEpisodes
                .Where(e => e.IndexNumber.HasValue)
                .Select(e => e.IndexNumber!.Value)
                .ToHashSet();

            foreach (var ep in imdbEpisodes.Episodes)
            {
                // Skip future episodes
                if (ep.ReleaseDate != null && IsInFuture(ep.ReleaseDate, now))
                {
                    continue;
                }

                if (!localEpisodeNumbers.Contains(ep.EpisodeNumber))
                {
                    missingEpisodes.Add(new MissingEpisodeInfo
                    {
                        Season = seasonNum,
                        Episode = ep.EpisodeNumber,
                        Title = ep.Title ?? "TBA",
                        ImdbId = ep.Id,
                    });
                }
            }
        }

        // Determine status
        var isEnded = title != null && title.EndYear > 0;
        var status = isEnded ? "ended" : "running";

        return new SeriesHealthResult
        {
            Name = series.Name,
            JellyfinId = series.Id.ToString("N"),
            ImdbId = imdbId,
            StartYear = title?.StartYear ?? series.ProductionYear ?? 0,
            EndYear = isEnded ? title!.EndYear : null,
            Status = status,
            SeasonsLocal = localSeasonNumbers.Count,
            SeasonsTotal = imdbSeasonNumbers.Count,
            MissingSeasons = missingSeasonNumbers,
            MissingEpisodes = missingEpisodes,
            NextEpisode = nextEpisode,
        };
    }

    private static bool IsInFuture(PrecisionDate date, DateTime now)
    {
        if (date.Year == 0)
        {
            return false;
        }

        var releaseDate = new DateTime(
            date.Year,
            date.Month > 0 ? date.Month : 12,
            date.Day > 0 ? date.Day : 28,
            0, 0, 0,
            DateTimeKind.Utc);

        return releaseDate > now;
    }

    private static string FormatDate(PrecisionDate date)
    {
        if (date.Day > 0 && date.Month > 0)
        {
            return $"{date.Year:D4}-{date.Month:D2}-{date.Day:D2}";
        }

        if (date.Month > 0)
        {
            return $"{date.Year:D4}-{date.Month:D2}";
        }

        return date.Year.ToString(CultureInfo.InvariantCulture);
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build Jellyfin.Plugin.ShowHealth.sln`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit**

```bash
git add Jellyfin.Plugin.ShowHealth/Services/ShowHealthAnalyzer.cs
git commit -m "feat: add ShowHealthAnalyzer for Jellyfin vs IMDb comparison"
```

---

### Task 4: REST Controller

**Files:**
- Create: `Jellyfin.Plugin.ShowHealth/Api/ShowHealthController.cs`

- [ ] **Step 1: Create the controller**

```csharp
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ShowHealth.Models;
using Jellyfin.Plugin.ShowHealth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ShowHealth.Api;

/// <summary>
/// API controller for Show Health status.
/// </summary>
[ApiController]
[Route("ShowHealth")]
[Authorize]
public class ShowHealthController : ControllerBase
{
    private readonly ShowHealthAnalyzer _analyzer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowHealthController"/> class.
    /// </summary>
    public ShowHealthController(ShowHealthAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    /// <summary>
    /// Gets the health status of all TV series in the library.
    /// Compares local episodes/seasons against IMDb data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health status for all series.</returns>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ShowHealthResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var result = await _analyzer.AnalyzeAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build Jellyfin.Plugin.ShowHealth.sln`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: Commit**

```bash
git add Jellyfin.Plugin.ShowHealth/Api/ShowHealthController.cs
git commit -m "feat: add ShowHealth REST controller with GET /ShowHealth/Status"
```

---

### Task 5: Scheduled Task

**Files:**
- Create: `Jellyfin.Plugin.ShowHealth/Tasks/ShowHealthScanTask.cs`

- [ ] **Step 1: Create the scheduled task**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ShowHealth.Services;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShowHealth.Tasks;

/// <summary>
/// Scheduled task that periodically scans the library for missing episodes.
/// </summary>
public class ShowHealthScanTask : IScheduledTask
{
    private readonly ShowHealthAnalyzer _analyzer;
    private readonly IActivityManager _activityManager;
    private readonly ILogger<ShowHealthScanTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowHealthScanTask"/> class.
    /// </summary>
    public ShowHealthScanTask(
        ShowHealthAnalyzer analyzer,
        IActivityManager activityManager,
        ILogger<ShowHealthScanTask> logger)
    {
        _analyzer = analyzer;
        _activityManager = activityManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Show Health Scan";

    /// <inheritdoc />
    public string Key => "ShowHealthScan";

    /// <inheritdoc />
    public string Description => "Scans TV series library for missing episodes and seasons by comparing against IMDb data.";

    /// <inheritdoc />
    public string Category => "Library";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Show Health scan");
        progress.Report(0);

        var result = await _analyzer.AnalyzeAsync(cancellationToken).ConfigureAwait(false);

        progress.Report(100);

        var totalMissing = result.Summary.Incomplete;
        var totalEpisodes = 0;
        foreach (var series in result.Series)
        {
            totalEpisodes += series.MissingEpisodes.Count;
        }

        if (totalMissing > 0)
        {
            _logger.LogInformation(
                "Show Health scan complete: {Incomplete}/{Total} series incomplete, {MissingEpisodes} episodes missing",
                totalMissing,
                result.Summary.Total,
                totalEpisodes);

            await _activityManager.CreateAsync(new ActivityLogEntry
            {
                Name = "Show Health Scan",
                Type = "ShowHealthScan",
                Overview = $"{totalMissing} series incomplete, {totalEpisodes} episodes missing",
            }).ConfigureAwait(false);
        }
        else
        {
            _logger.LogInformation("Show Health scan complete: all {Total} series are complete", result.Summary.Total);
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerInterval,
                IntervalTicks = TimeSpan.FromHours(24).Ticks,
            },
        ];
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build Jellyfin.Plugin.ShowHealth.sln`
Expected: Build succeeded, 0 errors. If `IActivityManager` or `ActivityLogEntry` are not found, check NuGet packages. `IActivityManager` is in `Jellyfin.Data` which is a transitive dependency of `Jellyfin.Controller`. If the interface has changed, use `IActivityManager` from `MediaBrowser.Model.Activity` and adjust the `CreateAsync` call signature accordingly.

- [ ] **Step 3: Commit**

```bash
git add Jellyfin.Plugin.ShowHealth/Tasks/ShowHealthScanTask.cs
git commit -m "feat: add scheduled task for periodic Show Health scans"
```

---

### Task 6: Dashboard HTML

**Files:**
- Modify: `Jellyfin.Plugin.ShowHealth/Web/showhealth.html`

- [ ] **Step 1: Replace the Hello World HTML with the dashboard layout**

```html
<div id="showHealthPage" data-role="page" class="page type-interior pluginConfigurationPage"
     data-title="Show Health"
     data-controller="__plugin/showhealthjs">
    <div data-role="content">
        <div class="content-primary">

            <!-- Header -->
            <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:1em;">
                <h1>Show Health</h1>
                <span id="showHealthSummary" style="color:#888;font-size:0.9em;"></span>
            </div>

            <!-- Sort bar -->
            <div id="showHealthSortBar" style="display:flex;gap:8px;margin-bottom:1em;">
                <button is="emby-button" class="raised" data-sort="status" style="font-size:0.85em;">By Status</button>
                <button is="emby-button" class="raised" data-sort="urgency" style="font-size:0.85em;">By Urgency</button>
                <button is="emby-button" class="raised" data-sort="name" style="font-size:0.85em;">A-Z</button>
            </div>

            <!-- Error message -->
            <div id="showHealthError" style="display:none;padding:1em;background:#3a1a1a;border-radius:4px;margin-bottom:1em;color:#e5383b;"></div>

            <!-- Table -->
            <div id="showHealthTableContainer"></div>

        </div>
    </div>
</div>
```

- [ ] **Step 2: Build to verify embedded resource**

Run: `dotnet build Jellyfin.Plugin.ShowHealth.sln`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add Jellyfin.Plugin.ShowHealth/Web/showhealth.html
git commit -m "feat: add dashboard HTML layout for Show Health"
```

---

### Task 7: Frontend JavaScript — ShowHealthApi class

**Files:**
- Modify: `Jellyfin.Plugin.ShowHealth/Web/showhealth.js`

- [ ] **Step 1: Replace the Hello World JS with the full frontend**

Start with the `ShowHealthApi` class and the `ShowHealthSorter` class (no DOM dependencies, pure logic):

```javascript
// ============================================================
// ShowHealthApi — communicates with the backend
// ============================================================
class ShowHealthApi {
    constructor(apiClient) {
        this._apiClient = apiClient;
    }

    async fetchStatus() {
        var url = this._apiClient.getUrl('/ShowHealth/Status');
        var response = await this._apiClient.getJSON(url);
        return response;
    }
}

// ============================================================
// ShowHealthSorter — sorts series data by different criteria
// ============================================================
class ShowHealthSorter {
    sort(series, mode) {
        var copy = series.slice();
        switch (mode) {
            case 'status':
                return this._sortByStatus(copy);
            case 'urgency':
                return this._sortByUrgency(copy);
            case 'name':
                return this._sortByName(copy);
            default:
                return copy;
        }
    }

    _sortByStatus(series) {
        return series.sort(function (a, b) {
            var aIncomplete = a.missingEpisodes.length > 0 || a.missingSeasons.length > 0 ? 0 : 1;
            var bIncomplete = b.missingEpisodes.length > 0 || b.missingSeasons.length > 0 ? 0 : 1;
            if (aIncomplete !== bIncomplete) {
                return aIncomplete - bIncomplete;
            }
            return a.name.localeCompare(b.name);
        });
    }

    _sortByUrgency(series) {
        return series.sort(function (a, b) {
            var aDate = a.nextEpisode ? a.nextEpisode.releaseDate : '';
            var bDate = b.nextEpisode ? b.nextEpisode.releaseDate : '';
            // Series with upcoming episodes first (sorted by date)
            if (aDate && !bDate) return -1;
            if (!aDate && bDate) return 1;
            if (aDate && bDate) return aDate.localeCompare(bDate);
            // Then incomplete series
            var aIncomplete = a.missingEpisodes.length > 0 || a.missingSeasons.length > 0 ? 0 : 1;
            var bIncomplete = b.missingEpisodes.length > 0 || b.missingSeasons.length > 0 ? 0 : 1;
            if (aIncomplete !== bIncomplete) return aIncomplete - bIncomplete;
            return a.name.localeCompare(b.name);
        });
    }

    _sortByName(series) {
        return series.sort(function (a, b) {
            return a.name.localeCompare(b.name);
        });
    }
}
```

- [ ] **Step 2: Commit partial JS (api + sorter)**

```bash
git add Jellyfin.Plugin.ShowHealth/Web/showhealth.js
git commit -m "feat: add ShowHealthApi and ShowHealthSorter JS classes"
```

---

### Task 8: Frontend JavaScript — ShowHealthTable class

**Files:**
- Modify: `Jellyfin.Plugin.ShowHealth/Web/showhealth.js`

- [ ] **Step 1: Add ShowHealthTable class after ShowHealthSorter**

```javascript
// ============================================================
// ShowHealthTable — renders the series table with expand/collapse
// ============================================================
class ShowHealthTable {
    constructor(container, apiClient) {
        this._container = container;
        this._apiClient = apiClient;
        this._expandedRows = {};
    }

    render(series) {
        var table = document.createElement('table');
        table.style.cssText = 'width:100%;border-collapse:collapse;font-size:0.9em;';

        table.appendChild(this._createHeader());
        var tbody = document.createElement('tbody');

        for (var i = 0; i < series.length; i++) {
            var s = series[i];
            var isIncomplete = s.missingEpisodes.length > 0 || s.missingSeasons.length > 0;
            var mainRow = this._createSeriesRow(s, isIncomplete);
            tbody.appendChild(mainRow);

            if (isIncomplete) {
                var detailRow = this._createDetailRow(s);
                tbody.appendChild(detailRow);
            }
        }

        table.appendChild(tbody);
        this._container.innerHTML = '';
        this._container.appendChild(table);
    }

    _createHeader() {
        var thead = document.createElement('thead');
        var tr = document.createElement('tr');
        tr.style.cssText = 'border-bottom:2px solid #444;text-align:left;';
        var headers = ['', '', 'Series', 'Status', 'Seasons', 'Missing', 'Next Episode'];
        var widths = ['24px', '50px', '', '', '', '', ''];
        for (var i = 0; i < headers.length; i++) {
            var th = document.createElement('th');
            th.style.padding = '8px';
            if (widths[i]) th.style.width = widths[i];
            th.textContent = headers[i];
            tr.appendChild(th);
        }
        thead.appendChild(tr);
        return thead;
    }

    _createSeriesRow(s, isIncomplete) {
        var self = this;
        var tr = document.createElement('tr');
        tr.style.cssText = 'border-bottom:1px solid #333;';
        if (!isIncomplete) {
            tr.style.opacity = '0.5';
        }

        // Expand arrow
        var tdArrow = document.createElement('td');
        tdArrow.style.padding = '8px';
        if (isIncomplete) {
            tr.style.cursor = 'pointer';
            var isExpanded = !!this._expandedRows[s.jellyfinId];
            tdArrow.innerHTML = isExpanded ? '&#9660;' : '&#9654;';
            tr.addEventListener('click', function () {
                self._toggleRow(s.jellyfinId, tr, tdArrow);
            });
        }
        tr.appendChild(tdArrow);

        // Poster
        var tdPoster = document.createElement('td');
        tdPoster.style.padding = '8px';
        var img = document.createElement('img');
        img.src = this._apiClient.getUrl('/Items/' + s.jellyfinId + '/Images/Primary', { height: 54 });
        img.style.cssText = 'width:36px;height:54px;border-radius:3px;object-fit:cover;';
        img.onerror = function () { this.style.display = 'none'; };
        tdPoster.appendChild(img);
        tr.appendChild(tdPoster);

        // Name + years
        var tdName = document.createElement('td');
        tdName.style.padding = '8px';
        var nameDiv = document.createElement('div');
        nameDiv.style.fontWeight = 'bold';
        nameDiv.textContent = s.name;
        tdName.appendChild(nameDiv);
        var yearDiv = document.createElement('div');
        yearDiv.style.cssText = 'font-size:0.8em;color:#888;';
        yearDiv.textContent = s.endYear ? s.startYear + ' – ' + s.endYear : s.startYear + ' –';
        tdName.appendChild(yearDiv);
        tr.appendChild(tdName);

        // Status badge
        var tdStatus = document.createElement('td');
        tdStatus.style.padding = '8px';
        var badge = document.createElement('span');
        badge.style.cssText = 'padding:2px 8px;border-radius:4px;font-size:0.8em;';
        if (s.status === 'ended') {
            badge.style.background = '#2d6a4f';
            badge.style.color = '#b7e4c7';
            badge.textContent = 'Ended';
        } else {
            badge.style.background = '#1d3557';
            badge.style.color = '#a8dadc';
            badge.textContent = 'Running';
        }
        tdStatus.appendChild(badge);
        tr.appendChild(tdStatus);

        // Seasons
        var tdSeasons = document.createElement('td');
        tdSeasons.style.padding = '8px';
        tdSeasons.textContent = s.seasonsLocal + '/' + s.seasonsTotal;
        if (s.missingSeasons.length > 0) {
            tdSeasons.style.color = '#e5383b';
        }
        tr.appendChild(tdSeasons);

        // Missing
        var tdMissing = document.createElement('td');
        tdMissing.style.padding = '8px';
        if (s.missingSeasons.length > 0 && s.missingEpisodes.length === 0) {
            tdMissing.style.color = '#e5383b';
            tdMissing.style.fontWeight = 'bold';
            tdMissing.textContent = 'Season ' + s.missingSeasons.join(', ') + ' missing';
        } else if (s.missingEpisodes.length > 0) {
            tdMissing.style.color = '#e5383b';
            tdMissing.style.fontWeight = 'bold';
            tdMissing.textContent = s.missingEpisodes.length + ' episodes';
        } else {
            tdMissing.style.color = '#2d6a4f';
            tdMissing.textContent = 'Complete';
        }
        tr.appendChild(tdMissing);

        // Next episode
        var tdNext = document.createElement('td');
        tdNext.style.padding = '8px';
        if (s.nextEpisode && s.nextEpisode.releaseDate) {
            var nextBadge = document.createElement('span');
            nextBadge.style.cssText = 'padding:2px 8px;border-radius:4px;background:#3d3200;color:#ffd60a;font-size:0.8em;';
            nextBadge.textContent = s.nextEpisode.releaseDate;
            tdNext.appendChild(nextBadge);
        } else {
            tdNext.style.color = '#555';
            tdNext.textContent = '\u2014';
        }
        tr.appendChild(tdNext);

        return tr;
    }

    _createDetailRow(s) {
        var tr = document.createElement('tr');
        tr.style.cssText = 'background:#111;';
        tr.dataset.detailFor = s.jellyfinId;

        var isExpanded = !!this._expandedRows[s.jellyfinId];
        if (!isExpanded) {
            tr.style.display = 'none';
        }

        var td = document.createElement('td');
        td.colSpan = 7;
        td.style.cssText = 'padding:12px 12px 12px 48px;';

        // Group missing episodes by season
        var bySeason = {};
        for (var i = 0; i < s.missingEpisodes.length; i++) {
            var ep = s.missingEpisodes[i];
            if (!bySeason[ep.season]) {
                bySeason[ep.season] = [];
            }
            bySeason[ep.season].push(ep);
        }

        var seasonKeys = Object.keys(bySeason).sort(function (a, b) { return a - b; });
        for (var j = 0; j < seasonKeys.length; j++) {
            var seasonNum = seasonKeys[j];
            var episodes = bySeason[seasonNum];

            var header = document.createElement('div');
            header.style.cssText = 'font-weight:bold;color:#aaa;margin-bottom:6px;' + (j > 0 ? 'margin-top:10px;' : '');
            header.textContent = 'Season ' + seasonNum + ' \u2014 ' + episodes.length + ' missing';
            td.appendChild(header);

            var chips = document.createElement('div');
            chips.style.cssText = 'display:flex;gap:8px;flex-wrap:wrap;';
            for (var k = 0; k < episodes.length; k++) {
                var chip = document.createElement('span');
                chip.style.cssText = 'padding:4px 10px;background:#2a2a3a;border-radius:4px;font-size:0.85em;border-left:3px solid #e5383b;';
                chip.textContent = 'E' + String(episodes[k].episode).padStart(2, '0') + ' \u2014 ' + episodes[k].title;
                chips.appendChild(chip);
            }
            td.appendChild(chips);
        }

        tr.appendChild(td);
        return tr;
    }

    _toggleRow(jellyfinId, mainRow, arrowTd) {
        this._expandedRows[jellyfinId] = !this._expandedRows[jellyfinId];
        var detailRow = this._container.querySelector('[data-detail-for="' + jellyfinId + '"]');
        if (detailRow) {
            detailRow.style.display = this._expandedRows[jellyfinId] ? '' : 'none';
        }
        arrowTd.innerHTML = this._expandedRows[jellyfinId] ? '&#9660;' : '&#9654;';
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Jellyfin.Plugin.ShowHealth/Web/showhealth.js
git commit -m "feat: add ShowHealthTable JS class with expand/collapse"
```

---

### Task 9: Frontend JavaScript — ShowHealthPage entry point

**Files:**
- Modify: `Jellyfin.Plugin.ShowHealth/Web/showhealth.js`

- [ ] **Step 1: Add ShowHealthPage class and export default at the end of the file**

```javascript
// ============================================================
// ShowHealthPage — main entry point, lifecycle and event binding
// ============================================================
class ShowHealthPage {
    constructor(view) {
        this._view = view;
        this._api = new ShowHealthApi(ApiClient);
        this._sorter = new ShowHealthSorter();
        this._table = new ShowHealthTable(
            view.querySelector('#showHealthTableContainer'),
            ApiClient
        );
        this._data = null;
        this._currentSort = 'status';
    }

    async init() {
        this._bindSortButtons();
        await this._loadData();
    }

    _bindSortButtons() {
        var self = this;
        var buttons = this._view.querySelectorAll('#showHealthSortBar button');
        for (var i = 0; i < buttons.length; i++) {
            buttons[i].addEventListener('click', function () {
                var mode = this.getAttribute('data-sort');
                self._currentSort = mode;
                self._updateSortButtonStyles();
                self._renderTable();
            });
        }
        this._updateSortButtonStyles();
    }

    _updateSortButtonStyles() {
        var buttons = this._view.querySelectorAll('#showHealthSortBar button');
        for (var i = 0; i < buttons.length; i++) {
            var btn = buttons[i];
            if (btn.getAttribute('data-sort') === this._currentSort) {
                btn.style.cssText = 'font-size:0.85em;border:1px solid #666;background:#2a2a3a;';
            } else {
                btn.style.cssText = 'font-size:0.85em;border:1px solid #333;background:#1a1a2e;';
            }
        }
    }

    async _loadData() {
        Dashboard.showLoadingMsg();
        var errorEl = this._view.querySelector('#showHealthError');
        errorEl.style.display = 'none';

        try {
            var response = await this._api.fetchStatus();
            this._data = response;
            this._updateSummary(response.summary);
            this._renderTable();
        } catch (err) {
            errorEl.textContent = 'Failed to load Show Health data: ' + (err.message || err);
            errorEl.style.display = '';
        } finally {
            Dashboard.hideLoadingMsg();
        }
    }

    _updateSummary(summary) {
        var el = this._view.querySelector('#showHealthSummary');
        el.textContent = summary.total + ' series \u00B7 ' + summary.incomplete + ' incomplete';
    }

    _renderTable() {
        if (!this._data) return;
        var sorted = this._sorter.sort(this._data.series, this._currentSort);
        this._table.render(sorted);
    }
}

// ============================================================
// Entry point — Jellyfin calls this via data-controller
// ============================================================
export default function (view) {
    view.addEventListener('viewshow', function () {
        var page = new ShowHealthPage(view);
        page.init();
    });
}
```

- [ ] **Step 2: Build to verify the embedded resource is valid**

Run: `dotnet build Jellyfin.Plugin.ShowHealth.sln`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add Jellyfin.Plugin.ShowHealth/Web/showhealth.js
git commit -m "feat: add ShowHealthPage entry point and complete frontend"
```

---

### Task 10: Build, Deploy, and Smoke Test

- [ ] **Step 1: Full build**

Run: `dotnet build Jellyfin.Plugin.ShowHealth.sln`
Expected: Build succeeded, 0 errors, 0 warnings

- [ ] **Step 2: Publish and deploy to Jellyfin**

Run:
```bash
dotnet publish Jellyfin.Plugin.ShowHealth.sln --configuration=Release
cp -r Jellyfin.Plugin.ShowHealth/bin/Release/net9.0/publish/* /mnt/docker/docker/volumes/jellyfin_config/_data/plugins/Jellyfin.Plugin.ShowHealth/
docker restart jellyfin
```

- [ ] **Step 3: Verify in Jellyfin**

1. Open Jellyfin web UI
2. Check that "Show Health" appears in the main menu (left sidebar)
3. Click "Show Health" — the dashboard should load
4. Verify the loading spinner appears, then data loads
5. Check that series with posters appear in the table
6. Click on an incomplete series to expand the detail row
7. Test sort buttons (By Status, By Urgency, A-Z)
8. Check Dashboard → Scheduled Tasks → "Show Health Scan" exists

- [ ] **Step 4: Commit any fixes from smoke testing**

```bash
git add -A
git commit -m "fix: address issues found during smoke testing"
```

- [ ] **Step 5: Final commit and push**

```bash
git push -u origin master
```
