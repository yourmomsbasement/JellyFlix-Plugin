using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JellyFlix.Configuration;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace JellyFlix.WebUpdater;

/// <summary>
/// Keeps the JellyFlix web client up to date from published releases.
/// </summary>
/// <remarks>
/// The client is installed into a folder inside Jellyfin's own data directory, which the Jellyfin service can always
/// write to and which package upgrades never touch. Jellyfin serves it once its web folder (<c>--webdir</c> or
/// <c>JELLYFIN_WEB_DIR</c>) points there. Only releases built for this server's Jellyfin version are installed, every
/// download is checked against its published SHA-256, and a new build replaces the old one in a single folder swap, so
/// a failed update never leaves a half-written client behind.
/// </remarks>
public sealed class WebUpdateService : IDisposable
{
    /// <summary>
    /// The metadata file written next to the installed web files.
    /// </summary>
    public const string MetadataFileName = "jellyflix-release.json";

    private const string InstallFolderName = "jellyflix-web";

    private readonly IReleaseSource _source;
    private readonly IApplicationPaths _applicationPaths;
    private readonly Func<Version> _serverVersion;
    private readonly Func<PluginConfiguration> _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebUpdateService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private DateTimeOffset? _lastCheck;
    private string? _lastResult;
    private string? _latestVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebUpdateService"/> class.
    /// </summary>
    /// <param name="source">Where releases come from.</param>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="serverVersion">Returns the running Jellyfin version.</param>
    /// <param name="configuration">Returns the current plugin configuration.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public WebUpdateService(
        IReleaseSource source,
        IApplicationPaths applicationPaths,
        Func<Version> serverVersion,
        Func<PluginConfiguration> configuration,
        TimeProvider timeProvider,
        ILogger<WebUpdateService> logger)
    {
        _source = source;
        _applicationPaths = applicationPaths;
        _serverVersion = serverVersion;
        _configuration = configuration;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the folder the web client is installed into.
    /// </summary>
    public string InstallPath => GetInstallPath(_applicationPaths);

    /// <summary>
    /// Gets the folder the web client is installed into: <c>jellyflix-web</c> in Jellyfin's data folder.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <returns>The install folder.</returns>
    public static string GetInstallPath(IApplicationPaths applicationPaths)
        => Path.Combine(applicationPaths.ProgramDataPath, InstallFolderName);

    /// <summary>
    /// Gets the current status.
    /// </summary>
    /// <returns>The status.</returns>
    public WebUpdateStatus GetStatus()
    {
        var configuration = _configuration();
        var installed = ReadInstalled(InstallPath);
        var isWebDir = SamePath(_applicationPaths.WebPath, InstallPath);
        return new()
        {
            Enabled = configuration.WebUpdatesEnabled,
            InstallPath = InstallPath,
            ServerWebPath = _applicationPaths.WebPath,
            IsServedByPlugin = configuration.ServeWebClient && installed is not null && !isWebDir,
            IsServerUsingInstall = isWebDir || (configuration.ServeWebClient && installed is not null),
            Installed = installed,
            LatestVersion = _latestVersion,
            LastCheck = _lastCheck,
            LastResult = _lastResult
        };
    }

    /// <summary>
    /// Checks for a newer compatible release and installs it.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The status afterwards.</returns>
    public async Task<WebUpdateStatus> CheckAndInstallAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _lastResult = await CheckAndInstallCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "JellyFlix web update failed");
            _lastResult = "Update failed: " + ex.Message;
        }
        finally
        {
            _lastCheck = _timeProvider.GetUtcNow();
            _lock.Release();
        }

        return GetStatus();
    }

    /// <inheritdoc />
    public void Dispose() => _lock.Dispose();

    private static bool SamePath(string a, string b)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static InstalledWebClient? ReadInstalled(string folder)
    {
        var path = Path.Combine(folder, MetadataFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<InstalledWebClient>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private async Task<string> CheckAndInstallCoreAsync(CancellationToken cancellationToken)
    {
        var server = _serverVersion();
        var serverVersion = $"{server.Major}.{server.Minor}";
        var releases = await _source.GetReleasesAsync(cancellationToken).ConfigureAwait(false);
        var target = releases.FirstOrDefault(r => r.ServerVersion == serverVersion);
        _latestVersion = target?.Version;

        if (target is null)
        {
            return releases.Count == 0
                ? "No JellyFlix web releases found."
                : $"No JellyFlix web release is built for Jellyfin {serverVersion} yet (newest is for {releases[0].ServerVersion}).";
        }

        var installed = ReadInstalled(InstallPath);
        if (installed?.Commit == target.Commit && File.Exists(Path.Combine(InstallPath, "index.html")))
        {
            return $"Up to date ({installed.Version}).";
        }

        await InstallAsync(target, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Installed JellyFlix web {Version} ({Commit}) into {Path}", target.Version, target.Commit, InstallPath);
        return $"Installed {target.Version}.";
    }

    private async Task InstallAsync(WebRelease release, CancellationToken cancellationToken)
    {
        var download = InstallPath + ".download.zip";
        var staging = InstallPath + ".staging";
        var previous = InstallPath + ".previous";

        try
        {
            // The published checksum: the first token of the .sha256 file
            string expected;
            var shaStream = await _source.DownloadAsync(release.Sha256Url, cancellationToken).ConfigureAwait(false);
            await using (shaStream.ConfigureAwait(false))
            {
                using var reader = new StreamReader(shaStream, Encoding.UTF8);
                expected = (await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false)).Trim().Split(' ', '\t', '\n')[0];
            }

            // Download and hash in one pass
            Directory.CreateDirectory(Path.GetDirectoryName(download)!);
            string actual;
            var zipStream = await _source.DownloadAsync(release.ZipUrl, cancellationToken).ConfigureAwait(false);
            await using (zipStream.ConfigureAwait(false))
            {
                var file = File.Create(download);
                await using (file.ConfigureAwait(false))
                {
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = new byte[81920];
                    int read;
                    while ((read = await zipStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        hash.AppendData(buffer, 0, read);
                        await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    }

                    actual = Convert.ToHexStringLower(hash.GetHashAndReset());
                }
            }

            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Checksum mismatch for {release.Version}: expected {expected}, got {actual}.");
            }

            // Unpack next to the install folder (ExtractToDirectory refuses entries that escape the folder)
            DeleteIfExists(staging);
            await ZipFile.ExtractToDirectoryAsync(download, staging, cancellationToken).ConfigureAwait(false);
            if (!File.Exists(Path.Combine(staging, "index.html")))
            {
                throw new InvalidDataException($"Release {release.Version} has no index.html at its root.");
            }

            var metadata = new InstalledWebClient
            {
                Version = release.Version,
                Commit = release.Commit,
                ServerVersion = release.ServerVersion,
                InstalledAt = _timeProvider.GetUtcNow()
            };
            await File.WriteAllTextAsync(Path.Combine(staging, MetadataFileName), JsonSerializer.Serialize(metadata), cancellationToken).ConfigureAwait(false);

            // Swap folders: the old client stays in place until the new one is complete
            DeleteIfExists(previous);
            if (Directory.Exists(InstallPath))
            {
                Directory.Move(InstallPath, previous);
            }

            Directory.Move(staging, InstallPath);
            DeleteIfExists(previous);
        }
        finally
        {
            DeleteIfExists(download);
            DeleteIfExists(staging);
        }
    }
}
