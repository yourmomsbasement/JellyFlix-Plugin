using System;

namespace JellyFlix.Profiles;

/// <summary>
/// Recognizes uploaded profile photos by their content, not by what the browser says they are.
/// </summary>
public static class ProfileImageFormat
{
    /// <summary>
    /// The largest photo that's accepted. Clients shrink photos before uploading, so this is generous.
    /// </summary>
    public const int MaxBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Works out an image's type from its first bytes.
    /// </summary>
    /// <param name="data">The image.</param>
    /// <returns>The media type and file extension, or null when it isn't a JPEG, PNG or WebP image.</returns>
    public static (string MediaType, string Extension)? Detect(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return ("image/jpeg", ".jpg");
        }

        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return ("image/png", ".png");
        }

        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return ("image/webp", ".webp");
        }

        return null;
    }
}
