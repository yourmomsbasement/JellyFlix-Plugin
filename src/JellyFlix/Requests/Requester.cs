using System;

namespace JellyFlix.Requests;

/// <summary>
/// Who a request is made for: profiles aren't Seerr users, so requests go to their household's owner.
/// </summary>
/// <param name="OwnerId">The Jellyfin user that owns the caller's household (the caller, outside a household).</param>
/// <param name="IsKids">Whether the caller is a kids profile.</param>
public sealed record Requester(Guid OwnerId, bool IsKids);
