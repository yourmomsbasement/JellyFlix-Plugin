using System;
using System.Linq;
using System.Threading.Tasks;
using JellyFlix.Profiles;

namespace JellyFlix.Requests;

/// <summary>
/// Finds the <see cref="Requester"/> behind a Jellyfin user.
/// </summary>
public interface IRequesterResolver
{
    /// <summary>
    /// Finds who the user's requests are made for.
    /// </summary>
    /// <param name="userId">The signed-in Jellyfin user (a profile, or a plain account).</param>
    /// <returns>The requester.</returns>
    Task<Requester> ResolveAsync(Guid userId);
}
