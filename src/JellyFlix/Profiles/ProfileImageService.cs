using System;
using System.IO;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace JellyFlix.Profiles;

/// <summary>
/// Sets and removes profile photos. Profiles are Jellyfin users, so the photo is the user's own image, but the
/// household owner may change it for any profile, which Jellyfin's own endpoint allows only administrators.
/// </summary>
public class ProfileImageService
{
    private readonly ProfileService _profileService;
    private readonly IUserManager _userManager;
    private readonly IProviderManager _providerManager;
    private readonly IServerConfigurationManager _configurationManager;
    private readonly ILogger<ProfileImageService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileImageService"/> class.
    /// </summary>
    /// <param name="profileService">The profile service.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="providerManager">The provider manager.</param>
    /// <param name="configurationManager">The server configuration manager.</param>
    /// <param name="logger">The logger.</param>
    public ProfileImageService(
        ProfileService profileService,
        IUserManager userManager,
        IProviderManager providerManager,
        IServerConfigurationManager configurationManager,
        ILogger<ProfileImageService> logger)
    {
        _profileService = profileService;
        _userManager = userManager;
        _providerManager = providerManager;
        _configurationManager = configurationManager;
        _logger = logger;
    }

    /// <summary>
    /// Sets a profile's photo.
    /// </summary>
    /// <param name="callerId">The calling user id.</param>
    /// <param name="profileId">The profile.</param>
    /// <param name="data">The photo: a JPEG, PNG or WebP image.</param>
    /// <returns>A task.</returns>
    public async Task SetAsync(Guid callerId, Guid profileId, byte[] data)
    {
        if (data.Length > ProfileImageFormat.MaxBytes)
        {
            throw new ProfileException(ProfileError.BadRequest, "The photo is too large.");
        }

        var format = ProfileImageFormat.Detect(data)
            ?? throw new ProfileException(ProfileError.BadRequest, "The photo must be a JPEG, PNG or WebP image.");
        await _profileService.EnsureCanEditAsync(callerId, profileId).ConfigureAwait(false);

        var user = _userManager.GetUserById(profileId)
            ?? throw new ProfileException(ProfileError.NotFound, "Profile not found.");

        // Where Jellyfin keeps user images
        var usersPath = _configurationManager.ApplicationPaths.UserConfigurationDirectoryPath;
        var userPath = Path.GetFullPath(Path.Combine(usersPath, user.Username));
        if (!userPath.StartsWith(Path.GetFullPath(usersPath) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ProfileException(ProfileError.BadRequest, "Invalid profile.");
        }

        if (user.ProfileImage is not null)
        {
            DeleteFile(user.ProfileImage.Path);
            await _userManager.ClearProfileImageAsync(user).ConfigureAwait(false);
        }

        user.ProfileImage = new Jellyfin.Database.Implementations.Entities.ImageInfo(Path.Combine(userPath, "profile" + format.Extension));
        using var stream = new MemoryStream(data, false);
        await _providerManager.SaveImage(stream, format.MediaType, user.ProfileImage.Path).ConfigureAwait(false);
        await _userManager.UpdateUserAsync(user).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a profile's photo, so its generated avatar shows again.
    /// </summary>
    /// <param name="callerId">The calling user id.</param>
    /// <param name="profileId">The profile.</param>
    /// <returns>A task.</returns>
    public async Task RemoveAsync(Guid callerId, Guid profileId)
    {
        await _profileService.EnsureCanEditAsync(callerId, profileId).ConfigureAwait(false);
        var user = _userManager.GetUserById(profileId)
            ?? throw new ProfileException(ProfileError.NotFound, "Profile not found.");
        if (user.ProfileImage is null)
        {
            return;
        }

        DeleteFile(user.ProfileImage.Path);
        await _userManager.ClearProfileImageAsync(user).ConfigureAwait(false);
    }

    private void DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Couldn't delete the old profile photo {Path}", path);
        }
    }
}
