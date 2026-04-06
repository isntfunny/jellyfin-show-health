using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
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

            await _activityManager.CreateAsync(new ActivityLog(
                "Show Health Scan",
                "ShowHealthScan",
                Guid.Empty)
            {
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
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromHours(24).Ticks,
            },
        ];
    }
}
