using System;

namespace JellyFlix.WebUpdater;

/// <summary>
/// A published build of the JellyFlix web client.
/// </summary>
/// <param name="Version">The build version, e.g. "2026.10.13.7".</param>
/// <param name="Commit">The JellyFlix-Web commit it was built from.</param>
/// <param name="ServerVersion">The Jellyfin server major.minor it is built for, e.g. "13.0".</param>
/// <param name="PublishedAt">When the release was published.</param>
/// <param name="ZipUrl">Where to download the web client zip.</param>
/// <param name="Sha256Url">Where to download the zip's SHA-256 checksum.</param>
public record WebRelease(string Version, string Commit, string ServerVersion, DateTimeOffset PublishedAt, Uri ZipUrl, Uri Sha256Url);
