using JellyFlix.Profiles;
using Xunit;

namespace JellyFlix.Tests.Profiles;

public class ProfileImageFormatTests
{
    [Fact]
    public void RecognizesPhotosByTheirContent()
    {
        Assert.Equal("image/jpeg", ProfileImageFormat.Detect([0xFF, 0xD8, 0xFF, 0xE0, 0, 0])?.MediaType);
        Assert.Equal(".png", ProfileImageFormat.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0])?.Extension);
        Assert.Equal("image/webp", ProfileImageFormat.Detect("RIFF\0\0\0\0WEBPVP8 "u8)?.MediaType);
    }

    [Fact]
    public void RefusesAnythingElse()
    {
        Assert.Null(ProfileImageFormat.Detect("<svg xmlns='http://www.w3.org/2000/svg'/>"u8));
        Assert.Null(ProfileImageFormat.Detect("GIF89a"u8));
        Assert.Null(ProfileImageFormat.Detect([]));
    }
}
