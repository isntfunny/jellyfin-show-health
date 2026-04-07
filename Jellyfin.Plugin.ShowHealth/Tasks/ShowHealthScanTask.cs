using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.ShowHealth.Models;
using Jellyfin.Plugin.ShowHealth.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShowHealth.Tasks;

/// <summary>
/// Scheduled task that periodically scans the library for missing episodes.
/// Compares with previous scan results and only notifies about NEW missing content.
/// First run ever does NOT fire notifications (baseline scan).
/// </summary>
public class ShowHealthScanTask : IScheduledTask
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly ShowHealthAnalyzer _analyzer;
    private readonly IActivityManager _activityManager;
    private readonly ISessionManager _sessionManager;
    private readonly IServerApplicationPaths _appPaths;
    private readonly ILogger<ShowHealthScanTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowHealthScanTask"/> class.
    /// </summary>
    public ShowHealthScanTask(
        ShowHealthAnalyzer analyzer,
        IActivityManager activityManager,
        ISessionManager sessionManager,
        IServerApplicationPaths appPaths,
        ILogger<ShowHealthScanTask> logger)
    {
        _analyzer = analyzer;
        _activityManager = activityManager;
        _sessionManager = sessionManager;
        _appPaths = appPaths;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Show Health Scan";

    /// <inheritdoc />
    public string Key => "ShowHealthScan";

    /// <inheritdoc />
    public string Description => "Scans TV series library for missing episodes and seasons by comparing against IMDb data.";

    /// <inheritdoc />
    public string Category => "Show Health";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Show Health scan");
        progress.Report(0);

        var result = await _analyzer.AnalyzeAsync(cancellationToken).ConfigureAwait(false);
        progress.Report(90);

        var scanFilePath = GetScanFilePath();
        var previousSnapshot = await LoadPreviousSnapshotAsync(scanFilePath).ConfigureAwait(false);
        var isFirstRun = previousSnapshot == null;

        // Build current snapshot: set of "SeriesName|SxxExx" strings for quick diff
        var currentSnapshot = BuildSnapshot(result);

        // Save current snapshot for next run
        await SaveSnapshotAsync(scanFilePath, currentSnapshot).ConfigureAwait(false);

        progress.Report(95);

        if (isFirstRun)
        {
            _logger.LogInformation(
                "Show Health first scan complete (baseline): {Total} series, {Incomplete} incomplete. No notification on first run.",
                result.Summary.Total,
                result.Summary.Incomplete);
        }
        else
        {
            // Find NEW missing items (in current but not in previous)
            var newMissing = currentSnapshot.Except(previousSnapshot!).ToList();

            // Find COMPLETED items (were missing before, not anymore)
            var completed = previousSnapshot!.Except(currentSnapshot).ToList();

            if (newMissing.Count > 0)
            {
                var summary = FormatNotificationSummary(newMissing);

                _logger.LogInformation(
                    "Show Health scan: {Count} new missing items detected",
                    newMissing.Count);

                await _activityManager.CreateAsync(new ActivityLog(
                    "Show Health: new missing content detected",
                    "ShowHealthScan",
                    Guid.Empty)
                {
                    Overview = summary,
                }).ConfigureAwait(false);

                // Push notification to all admin sessions (browser, apps)
                await _sessionManager.SendMessageToAdminSessions(
                    SessionMessageType.ActivityLogEntry,
                    new { Header = "Show Health", Text = summary },
                    cancellationToken).ConfigureAwait(false);
            }

            if (completed.Count > 0)
            {
                var summary = FormatCompletedSummary(completed);

                _logger.LogInformation(
                    "Show Health scan: {Count} items completed",
                    completed.Count);

                await _activityManager.CreateAsync(new ActivityLog(
                    "Show Health: content completed!",
                    "ShowHealthScanCompleted",
                    Guid.Empty)
                {
                    Overview = summary,
                }).ConfigureAwait(false);

                // Push notification to all admin sessions
                await _sessionManager.SendMessageToAdminSessions(
                    SessionMessageType.ActivityLogEntry,
                    new { Header = "Show Health", Text = summary },
                    cancellationToken).ConfigureAwait(false);
            }

            if (newMissing.Count == 0 && completed.Count == 0)
            {
                _logger.LogInformation(
                    "Show Health scan complete: no changes. {Total} series, {Incomplete} incomplete.",
                    result.Summary.Total,
                    result.Summary.Incomplete);
            }
        }

        progress.Report(100);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromHours(24).Ticks,
            },
        ];
    }

    private static HashSet<string> BuildSnapshot(ShowHealthResponse result)
    {
        var snapshot = new HashSet<string>(StringComparer.Ordinal);

        foreach (var series in result.Series)
        {
            // Missing seasons as "SeriesName|S01 complete"
            foreach (var ms in series.MissingSeasons)
            {
                snapshot.Add($"{series.Name}|S{ms.Season:D2} complete ({ms.EpisodeCount} ep)");
            }

            // Missing episodes as "SeriesName|S01E03"
            foreach (var ep in series.MissingEpisodes)
            {
                snapshot.Add($"{series.Name}|S{ep.Season:D2}E{ep.Episode:D2}");
            }
        }

        return snapshot;
    }

    private static string FormatNotificationSummary(List<string> newMissing)
        => FormatEntrySummary(newMissing, $"{newMissing.Count} new missing items");

    private static string FormatCompletedSummary(List<string> completed)
        => FormatEntrySummary(completed, $"\ud83c\udf89 {completed.Count} items now complete!");

    private static string FormatEntrySummary(List<string> entries, string header)
    {
        var bySeries = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var pipe = entry.IndexOf('|', StringComparison.Ordinal);
            if (pipe < 0)
            {
                continue;
            }

            var seriesName = entry[..pipe];
            var item = entry[(pipe + 1)..];

            if (!bySeries.TryGetValue(seriesName, out var list))
            {
                list = new List<string>();
                bySeries[seriesName] = list;
            }

            list.Add(item);
        }

        var parts = new List<string>();
        foreach (var kvp in bySeries.Take(5))
        {
            var items = string.Join(", ", kvp.Value.Take(3));
            if (kvp.Value.Count > 3)
            {
                items += $" +{kvp.Value.Count - 3} more";
            }

            parts.Add($"{kvp.Key}: {items}");
        }

        var summary = string.Join("; ", parts);
        if (bySeries.Count > 5)
        {
            summary += $" (+{bySeries.Count - 5} more series)";
        }

        return $"{header} — {summary}";
    }

    private string GetScanFilePath()
    {
        var dir = Path.Combine(_appPaths.PluginConfigurationsPath, "ShowHealth");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "last-scan.json");
    }

    private async Task<HashSet<string>?> LoadPreviousSnapshotAsync(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list != null ? new HashSet<string>(list, StringComparer.Ordinal) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load previous scan snapshot, treating as first run");
            return null;
        }
    }

    private static async Task SaveSnapshotAsync(string path, HashSet<string> snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot.ToList(), JsonOptions);
        await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
    }
}
