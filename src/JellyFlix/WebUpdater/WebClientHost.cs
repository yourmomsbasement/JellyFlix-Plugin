using System;
using System.IO;
using System.Net.Mime;
using JellyFlix.Configuration;
using MediaBrowser.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace JellyFlix.WebUpdater;

/// <summary>
/// Serves the installed JellyFlix web client at <c>/web</c>, in place of the web client that ships with Jellyfin.
/// </summary>
/// <remarks>
/// This runs ahead of Jellyfin's own web hosting, so nothing has to be configured on the server (no <c>--webdir</c>).
/// Requests fall through to Jellyfin's stock web client while no JellyFlix build is installed, for files the build
/// doesn't have, and whenever serving is turned off in the plugin settings.
/// </remarks>
public sealed class WebClientHost : IStartupFilter, IDisposable
{
    private const string WebPath = "/web";

    private readonly string _installPath;
    private readonly Func<PluginConfiguration> _configuration;
    private readonly Func<string> _baseUrl;
    private readonly FileExtensionContentTypeProvider _contentTypes = new();
    private readonly object _providerLock = new();
    private PhysicalFileProvider? _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebClientHost"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="configuration">Returns the current plugin configuration.</param>
    /// <param name="baseUrl">Returns Jellyfin's base URL (e.g. "/jellyfin"), or an empty string.</param>
    public WebClientHost(IApplicationPaths applicationPaths, Func<PluginConfiguration> configuration, Func<string> baseUrl)
    {
        _installPath = WebUpdateService.GetInstallPath(applicationPaths);
        _configuration = configuration;
        _baseUrl = baseUrl;

        // Same extras as Jellyfin's own web hosting (subtitles octopus needs these)
        _contentTypes.Mappings[".data"] = MediaTypeNames.Application.Octet;
        _contentTypes.Mappings[".mem"] = MediaTypeNames.Application.Octet;
    }

    /// <summary>
    /// Gets a value indicating whether a complete JellyFlix build is installed.
    /// </summary>
    public bool IsInstalled => File.Exists(Path.Combine(_installPath, "index.html"));

    /// <summary>
    /// Gets a value indicating whether <c>/web</c> currently serves the installed JellyFlix build.
    /// </summary>
    public bool IsServing => _configuration().ServeWebClient && IsInstalled;

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            var webPath = new PathString(NormalizeBaseUrl(_baseUrl()) + WebPath);
            app.UseWhen(
                context => IsServing && context.Request.Path.StartsWithSegments(webPath, StringComparison.OrdinalIgnoreCase),
                branch =>
                {
                    var files = new InstalledFiles(this);
                    branch.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files, RequestPath = webPath });
                    branch.UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = files,
                        RequestPath = webPath,
                        ContentTypeProvider = _contentTypes,
                        OnPrepareResponse = context =>
                        {
                            // The page and service worker must be fetched fresh so updates show up; the rest is versioned
                            var name = Path.GetFileName(context.File.Name);
                            if (name.Equals("index.html", StringComparison.Ordinal) || name.StartsWith("serviceworker", StringComparison.Ordinal))
                            {
                                context.Context.Response.Headers.CacheControl = new StringValues("no-cache");
                            }
                        }
                    });
                });
            next(app);
        };

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_providerLock)
        {
            _provider?.Dispose();
            _provider = null;
        }
    }

    /// <summary>
    /// Turns Jellyfin's base URL setting ("jellyfin", "/jellyfin/", "") into a path prefix ("/jellyfin", "").
    /// </summary>
    /// <param name="baseUrl">The base URL setting.</param>
    /// <returns>The path prefix.</returns>
    internal static string NormalizeBaseUrl(string? baseUrl)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim().Trim('/');
        return trimmed.Length == 0 ? string.Empty : "/" + trimmed;
    }

    private IFileProvider? GetProvider()
    {
        lock (_providerLock)
        {
            if (_provider is null && Directory.Exists(_installPath))
            {
                _provider = new PhysicalFileProvider(_installPath);
            }

            return _provider;
        }
    }

    /// <summary>
    /// The install folder, opened once it exists. Files are looked up by path, so an update swapping in a new folder
    /// is picked up straight away.
    /// </summary>
    private sealed class InstalledFiles(WebClientHost host) : IFileProvider
    {
        public IDirectoryContents GetDirectoryContents(string subpath)
            => host.GetProvider()?.GetDirectoryContents(subpath) ?? NotFoundDirectoryContents.Singleton;

        public IFileInfo GetFileInfo(string subpath)
            => host.GetProvider()?.GetFileInfo(subpath) ?? new NotFoundFileInfo(subpath);

        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }
}
