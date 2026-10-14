using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace JellyFlix.Artwork;

/// <summary>
/// Names for options and downloaded files.
/// </summary>
public static partial class ArtworkFiles
{
    /// <summary>
    /// The prefix of options that are the library's own images.
    /// </summary>
    public const string LibraryPrefix = "library:";

    /// <summary>
    /// The prefix of options from metadata providers.
    /// </summary>
    public const string RemotePrefix = "remote:";

    /// <summary>
    /// The largest image that's downloaded.
    /// </summary>
    public const long MaxImageBytes = 25 * 1024 * 1024;

    /// <summary>
    /// The id of a provider's image.
    /// </summary>
    /// <param name="url">The image's address.</param>
    /// <returns>The id.</returns>
    public static string RemoteId(string url) => RemotePrefix + Hash(url)[..20];

    /// <summary>
    /// The name a provider's image is downloaded to.
    /// </summary>
    /// <param name="url">The image's address.</param>
    /// <param name="mediaType">The type the provider answered with.</param>
    /// <returns>The file name, or null when it isn't an image type that's kept.</returns>
    public static string? FileName(string url, string? mediaType)
    {
        var extension = mediaType?.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => null
        };
        return extension is null ? null : Hash(url)[..32] + extension;
    }

    /// <summary>
    /// Whether a name is one this plugin would have given a download: anything else is never served.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>Whether it's valid.</returns>
    public static bool IsValidFileName(string? name) => name is not null && FileNamePattern().IsMatch(name);

    /// <summary>
    /// The media type of a downloaded file.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <returns>The media type.</returns>
    public static string MediaType(string name) => name[(name.LastIndexOf('.') + 1)..] switch
    {
        "png" => "image/png",
        "webp" => "image/webp",
        _ => "image/jpeg"
    };

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    [GeneratedRegex("^[0-9a-f]{32}\\.(jpg|png|webp)$", RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();
}
