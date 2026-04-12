using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ShowHealth.Models;
using Jellyfin.Plugin.ShowHealth.Services;
using MediaBrowser.Controller;
using MediaBrowser.Model.Tasks;
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
    private readonly IServerApplicationPaths _appPaths;
    private readonly ITaskManager _taskManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowHealthController"/> class.
    /// </summary>
    public ShowHealthController(
        ShowHealthAnalyzer analyzer,
        IServerApplicationPaths appPaths,
        ITaskManager taskManager)
    {
        _analyzer = analyzer;
        _appPaths = appPaths;
        _taskManager = taskManager;
    }

    /// <summary>
    /// Returns the cached analysis snapshot from the last scheduled scan.
    /// Returns 404 if no snapshot exists yet (task has never run).
    /// </summary>
    /// <returns>Cached health status for all series, or 404.</returns>
    [HttpGet("Snapshot")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetSnapshot()
    {
        var path = ShowHealthPaths.GetAnalysisSnapshotPath(_appPaths);
        try
        {
            var json = await System.IO.File.ReadAllTextAsync(path).ConfigureAwait(false);
            return Content(json, "application/json");
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { message = "No analysis snapshot available. Run the Show Health Scan task first." });
        }
    }

    /// <summary>
    /// Triggers the Show Health Scan task to run immediately.
    /// </summary>
    /// <returns>No content on success.</returns>
    [HttpPost("RunScan")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult RunScan()
    {
        var task = _taskManager.ScheduledTasks
            .FirstOrDefault(t => t.ScheduledTask is Tasks.ShowHealthScanTask);

        if (task == null)
        {
            return NotFound(new { message = "Show Health Scan task not found." });
        }

        if (task.State != TaskState.Idle)
        {
            return Conflict(new { message = "Show Health Scan is already running." });
        }

        _ = _taskManager.Execute(task, new TaskOptions());
        return NoContent();
    }

    /// <summary>
    /// Gets the health status of all TV series in the library (live, slow).
    /// Compares local episodes/seasons against IMDb data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health status for all series.</returns>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ShowHealthResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var result = await _analyzer.AnalyzeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Returns all series from the Jellyfin library instantly (no IMDb calls).
    /// </summary>
    /// <returns>List of series with basic Jellyfin data.</returns>
    [HttpGet("Series")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<SeriesListResponse> GetSeries()
    {
        var series = _analyzer.GetSeriesList();
        return Ok(series);
    }

    /// <summary>
    /// Clears all IMDb API cache entries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("ClearCache")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> ClearCache(CancellationToken cancellationToken)
    {
        await _analyzer.ClearCacheAsync(cancellationToken).ConfigureAwait(false);

        // Delete the analysis snapshot so the dashboard shows the first-run state
        try
        {
            System.IO.File.Delete(ShowHealthPaths.GetAnalysisSnapshotPath(_appPaths));
        }
        catch (FileNotFoundException)
        {
        }

        // Trigger a fresh scan
        var task = _taskManager.ScheduledTasks
            .FirstOrDefault(t => t.ScheduledTask is Tasks.ShowHealthScanTask);
        if (task != null && task.State == TaskState.Idle)
        {
            _ = _taskManager.Execute(task, new TaskOptions());
        }

        return NoContent();
    }

    /// <summary>
    /// Analyzes a single series against IMDb by its IMDb ID.
    /// </summary>
    /// <param name="imdbId">The IMDb ID (e.g. tt1234567).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health result for the series, or 404 if not found.</returns>
    [HttpGet("Analyze/{imdbId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeriesHealthResult>> AnalyzeSeries(string imdbId, CancellationToken cancellationToken)
    {
        if (!Regex.IsMatch(imdbId, @"^tt\d{7,10}$", RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            return BadRequest("Invalid IMDb ID format");
        }

        var result = await _analyzer.AnalyzeSeriesAsync(imdbId, cancellationToken).ConfigureAwait(false);
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Returns the list of ignored series.
    /// </summary>
    [HttpGet("Ignored")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<IgnoredSeriesEntry>>> GetIgnored()
    {
        var list = await LoadIgnoredListAsync().ConfigureAwait(false);
        return Ok(list);
    }

    /// <summary>
    /// Adds a series to the ignore list.
    /// </summary>
    [HttpPost("Ignored")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> AddIgnored([FromBody] IgnoredSeriesEntry entry)
    {
        if (string.IsNullOrEmpty(entry.ImdbId))
        {
            return BadRequest("imdbId is required");
        }

        var list = await LoadIgnoredListAsync().ConfigureAwait(false);
        if (list.Any(e => e.ImdbId == entry.ImdbId))
        {
            return NoContent();
        }

        list.Add(entry);
        await SaveIgnoredListAsync(list).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Removes a series from the ignore list.
    /// </summary>
    [HttpDelete("Ignored/{imdbId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RemoveIgnored(string imdbId)
    {
        var list = await LoadIgnoredListAsync().ConfigureAwait(false);
        list.RemoveAll(e => e.ImdbId == imdbId);
        await SaveIgnoredListAsync(list).ConfigureAwait(false);
        return NoContent();
    }

    private async Task<List<IgnoredSeriesEntry>> LoadIgnoredListAsync()
    {
        var path = ShowHealthPaths.GetIgnoredSeriesPath(_appPaths);
        try
        {
            var json = await System.IO.File.ReadAllTextAsync(path).ConfigureAwait(false);
            return System.Text.Json.JsonSerializer.Deserialize<List<IgnoredSeriesEntry>>(json) ?? new List<IgnoredSeriesEntry>();
        }
        catch (FileNotFoundException)
        {
            return new List<IgnoredSeriesEntry>();
        }
    }

    private async Task SaveIgnoredListAsync(List<IgnoredSeriesEntry> list)
    {
        ShowHealthPaths.EnsureDirectory(_appPaths);
        var path = ShowHealthPaths.GetIgnoredSeriesPath(_appPaths);
        var json = System.Text.Json.JsonSerializer.Serialize(list);
        await System.IO.File.WriteAllTextAsync(path, json).ConfigureAwait(false);
    }
}
