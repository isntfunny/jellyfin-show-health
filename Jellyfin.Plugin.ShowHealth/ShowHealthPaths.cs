using System;
using System.IO;
using MediaBrowser.Controller;

namespace Jellyfin.Plugin.ShowHealth;

/// <summary>
/// Shared file paths used by the controller and scheduled task.
/// </summary>
internal static class ShowHealthPaths
{
    private const string PluginDir = "ShowHealth";
    private const string AnalysisSnapshotFile = "last-analysis.json";
    private const string ScanSnapshotFile = "last-scan.json";
    private const string IgnoredSeriesFile = "ignored-series.json";
    private const string CacheDir = "tvmaze-cache";
    private const string LegacyImdbCacheDir = "cache";

    /// <summary>
    /// Returns the path to the analysis snapshot JSON file.
    /// Does NOT create the directory — call <see cref="EnsureDirectory"/> before writing.
    /// </summary>
    internal static string GetAnalysisSnapshotPath(IServerApplicationPaths appPaths)
    {
        return Path.Combine(appPaths.PluginConfigurationsPath, PluginDir, AnalysisSnapshotFile);
    }

    /// <summary>
    /// Returns the path to the scan diff snapshot (used for change detection between runs).
    /// </summary>
    internal static string GetScanSnapshotPath(IServerApplicationPaths appPaths)
    {
        return Path.Combine(appPaths.PluginConfigurationsPath, PluginDir, ScanSnapshotFile);
    }

    /// <summary>
    /// Returns the path to the ignored series JSON file.
    /// </summary>
    internal static string GetIgnoredSeriesPath(IServerApplicationPaths appPaths)
    {
        return Path.Combine(appPaths.PluginConfigurationsPath, PluginDir, IgnoredSeriesFile);
    }

    /// <summary>
    /// Returns the directory holding cached TVmaze responses.
    /// </summary>
    internal static string GetCacheDirectory(IServerApplicationPaths appPaths)
    {
        return Path.Combine(appPaths.PluginConfigurationsPath, PluginDir, CacheDir);
    }

    /// <summary>
    /// Deletes the cache directory left behind by the IMDb API backend. Entries there use a
    /// different response shape and different keys, so they would only waste disk until expiry.
    /// </summary>
    internal static void RemoveLegacyImdbCache(IServerApplicationPaths appPaths)
    {
        var legacyDir = Path.Combine(appPaths.PluginConfigurationsPath, PluginDir, LegacyImdbCacheDir);

        try
        {
            if (Directory.Exists(legacyDir))
            {
                Directory.Delete(legacyDir, true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; a leftover cache directory is harmless.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup; a leftover cache directory is harmless.
        }
    }

    /// <summary>
    /// Ensures the ShowHealth plugin data directory exists.
    /// </summary>
    internal static string EnsureDirectory(IServerApplicationPaths appPaths)
    {
        var dir = Path.Combine(appPaths.PluginConfigurationsPath, PluginDir);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
