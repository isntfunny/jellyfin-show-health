using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.ShowHealth.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ShowHealth;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Show Health";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("2626c821-e54b-488a-b316-ea9c5f95e24f");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = GetType().Namespace!;

        return
        [
            // Dashboard page (main menu entry)
            new PluginPageInfo
            {
                Name = "showhealth",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Web.showhealth.html", ns),
                EnableInMainMenu = true,
                MenuIcon = "health_and_safety",
            },

            // JavaScript controller for dashboard page
            new PluginPageInfo
            {
                Name = "showhealthjs",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Web.showhealth.js", ns),
            },
        ];
    }
}
