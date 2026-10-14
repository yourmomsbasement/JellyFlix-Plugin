using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JellyFlix.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace JellyFlix;

/// <summary>
/// JellyFlix: Netflix-style household profiles, personalized recommendations, and automatic updates of the JellyFlix
/// web client.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// The plugin id.
    /// </summary>
    public static readonly Guid PluginId = Guid.Parse("c1a2981e-3ba2-44c5-9360-83cf293c2a13");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="xmlSerializer">The XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        MigrateLegacyConfiguration(applicationPaths);
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Lanternfish";

    /// <inheritdoc />
    public override Guid Id => PluginId;

    /// <inheritdoc />
    public override string Description => "Household profiles, personalized recommendations, requests and automatic Lanternfish web client updates.";

    /// <summary>
    /// Carries the profile settings over from the separate JellyFlix Profiles plugin this one replaces. Its settings
    /// file has the same layout, so it is simply copied.
    /// </summary>
    private void MigrateLegacyConfiguration(IApplicationPaths applicationPaths)
    {
        var legacyPath = Path.Combine(applicationPaths.PluginConfigurationsPath, "JellyFlix.Profiles.xml");
        if (File.Exists(ConfigurationFilePath) || !File.Exists(legacyPath))
        {
            return;
        }

        try
        {
            File.Copy(legacyPath, ConfigurationFilePath);
        }
        catch (IOException)
        {
            // Defaults it is
        }
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        ];
    }
}
