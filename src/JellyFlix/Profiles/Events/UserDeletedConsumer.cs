using System.Threading.Tasks;
using Jellyfin.Data.Events.Users;
using JellyFlix.Artwork;
using MediaBrowser.Controller.Events;

namespace JellyFlix.Profiles.Events;

/// <summary>
/// Removes deleted users from their household, and forgets their artwork choices.
/// </summary>
public class UserDeletedConsumer : IEventConsumer<UserDeletedEventArgs>
{
    private readonly ProfileService _profileService;
    private readonly ArtworkStore _artworkStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDeletedConsumer"/> class.
    /// </summary>
    /// <param name="profileService">The profile service.</param>
    /// <param name="artworkStore">The artwork choices.</param>
    public UserDeletedConsumer(ProfileService profileService, ArtworkStore artworkStore)
    {
        _profileService = profileService;
        _artworkStore = artworkStore;
    }

    /// <inheritdoc />
    public Task OnEvent(UserDeletedEventArgs eventArgs)
    {
        _artworkStore.RemoveUser(eventArgs.Argument.Id);
        return _profileService.OnUserDeletedAsync(eventArgs.Argument.Id);
    }
}
