using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ShowHealth.Models;
using Jellyfin.Plugin.ShowHealth.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShowHealth.Tasks;

/// <summary>
/// Runs after every Jellyfin library scan to update the Show Health analysis snapshot.
/// Does NOT send notifications — that is handled by the daily <see cref="ShowHealthScanTask"/>.
/// </summary>
public class ShowHealthPostScanTask : ILibraryPostScanTask
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly ShowHealthAnalyzer _analyzer;
    private readonly IServerApplicationPaths _appPaths;
    private readonly ILogger<ShowHealthPostScanTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowHealthPostScanTask"/> class.
    /// </summary>
    public ShowHealthPostScanTask(
        ShowHealthAnalyzer analyzer,
        IServerApplicationPaths appPaths,
        ILogger<ShowHealthPostScanTask> logger)
    {
        _analyzer = analyzer;
        _appPaths = appPaths;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Show Health post-scan: updating analysis snapshot");

        var result = await _analyzer.AnalyzeAsync(progress, cancellationToken).ConfigureAwait(false);

        ShowHealthPaths.EnsureDirectory(_appPaths);
        var snapshotPath = ShowHealthPaths.GetAnalysisSnapshotPath(_appPaths);

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var tmpPath = snapshotPath + ".tmp";
        await File.WriteAllTextAsync(tmpPath, json, cancellationToken).ConfigureAwait(false);
        File.Move(tmpPath, snapshotPath, overwrite: true);

        _logger.LogInformation(
            "Show Health post-scan complete: {Total} shows, {Incomplete} incomplete",
            result.Summary.Total,
            result.Summary.Incomplete);
    }
}
