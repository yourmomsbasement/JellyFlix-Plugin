using System;
using Jellyfin.Data.Events.Users;
using JellyFlix.Appearance;
using JellyFlix.Artwork;
using JellyFlix.Companion;
using JellyFlix.Configuration;
using JellyFlix.Migration;
using JellyFlix.Profiles;
using JellyFlix.Profiles.Data;
using JellyFlix.Profiles.Events;
using JellyFlix.Profiles.Security;
using JellyFlix.Recommendations;
using JellyFlix.Requests;
using JellyFlix.Search;
using JellyFlix.WebUpdater;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JellyFlix;

/// <summary>
/// Registers JellyFlix's services.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.TryAddSingleton(TimeProvider.System);
        serviceCollection.AddSingleton<Func<PluginConfiguration>>(_ => () => Plugin.Instance?.Configuration ?? new PluginConfiguration());

        // Profiles
        serviceCollection.AddSingleton<IHouseholdStore, JsonHouseholdStore>();
        serviceCollection.AddSingleton(sp => new PinAttemptLimiter(sp.GetRequiredService<TimeProvider>()));
        serviceCollection.AddSingleton<ProfileService>();
        serviceCollection.AddSingleton<ProfileImageService>();
        serviceCollection.AddScoped<IEventConsumer<UserDeletedEventArgs>, UserDeletedConsumer>();

        // Recommendations
        serviceCollection.AddSingleton<RecommendationService>();

        // Requests through Seerr
        serviceCollection.AddSingleton<SeerrClient>();
        serviceCollection.AddSingleton<IRequesterResolver, HouseholdRequesterResolver>();
        serviceCollection.AddSingleton<RequestService>();

        // Search by description
        serviceCollection.AddSingleton<EmbeddingClient>();
        serviceCollection.AddSingleton<EmbeddingStore>();
        serviceCollection.AddSingleton<DescriptionSearchService>();

        // Artwork per profile
        serviceCollection.AddSingleton<ArtworkStore>();
        serviceCollection.AddSingleton<ArtworkService>();

        // Seasonal themes and dynamic colors
        serviceCollection.AddSingleton<AppearanceService>();

        // End-credit companion
        serviceCollection.AddSingleton<CompanionService>();

        // Web Updater
        serviceCollection.AddSingleton<IReleaseSource, GitHubReleaseSource>();
        serviceCollection.AddSingleton(sp => new WebUpdateService(
            sp.GetRequiredService<IReleaseSource>(),
            sp.GetRequiredService<IApplicationPaths>(),
            () => applicationHost.ApplicationVersion,
            sp.GetRequiredService<Func<PluginConfiguration>>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<WebUpdateService>>()));

        // Serves the installed web client at /web, so the server needs no --webdir
        serviceCollection.AddSingleton(sp => new WebClientHost(
            sp.GetRequiredService<IApplicationPaths>(),
            sp.GetRequiredService<Func<PluginConfiguration>>(),
            () => sp.GetRequiredService<IServerConfigurationManager>().GetNetworkConfiguration().BaseUrl));
        serviceCollection.AddSingleton<IStartupFilter>(sp => sp.GetRequiredService<WebClientHost>());

        // Replaces the separate JellyFlix Profiles and Recommendations plugins
        serviceCollection.AddSingleton<IHostedService, LegacyPluginCleanup>();
    }
}
