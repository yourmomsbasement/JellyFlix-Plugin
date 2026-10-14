using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JellyFlix.Migration;
using MediaBrowser.Common;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace JellyFlix.Tests.Migration;

public class LegacyPluginCleanupTests
{
    private static LocalPlugin CreatePlugin(Guid id, string name)
        => new("/plugins/" + name, true, new PluginManifest { Id = id, Name = name, Version = "1.0.0.0" });

    [Fact]
    public async Task RemovesTheReplacedPluginsAndAsksForARestart()
    {
        var profiles = CreatePlugin(LegacyPluginCleanup.LegacyPluginIds[0], "JellyFlix Profiles");
        var recommendations = CreatePlugin(LegacyPluginCleanup.LegacyPluginIds[1], "JellyFlix Recommendations");
        var other = CreatePlugin(Guid.NewGuid(), "TMDb");
        var plugins = new Mock<IPluginManager>();
        plugins.SetupGet(p => p.Plugins).Returns(new List<LocalPlugin> { profiles, other, recommendations });
        plugins.Setup(p => p.RemovePlugin(It.IsAny<LocalPlugin>())).Returns(true);
        var host = new Mock<IApplicationHost>();

        await new LegacyPluginCleanup(plugins.Object, host.Object, NullLogger<LegacyPluginCleanup>.Instance).StartAsync(TestContext.Current.CancellationToken);

        plugins.Verify(p => p.RemovePlugin(profiles), Times.Once);
        plugins.Verify(p => p.RemovePlugin(recommendations), Times.Once);
        plugins.Verify(p => p.RemovePlugin(other), Times.Never);
        host.Verify(h => h.NotifyPendingRestart(), Times.Once);
    }

    [Fact]
    public async Task DoesNothingWithoutReplacedPlugins()
    {
        var plugins = new Mock<IPluginManager>();
        plugins.SetupGet(p => p.Plugins).Returns(new List<LocalPlugin> { CreatePlugin(Guid.NewGuid(), "TMDb") });
        var host = new Mock<IApplicationHost>();

        await new LegacyPluginCleanup(plugins.Object, host.Object, NullLogger<LegacyPluginCleanup>.Instance).StartAsync(TestContext.Current.CancellationToken);

        plugins.Verify(p => p.RemovePlugin(It.IsAny<LocalPlugin>()), Times.Never);
        host.Verify(h => h.NotifyPendingRestart(), Times.Never);
    }
}
