using System;
using System.Linq;
using System.Threading.Tasks;
using JellyFlix.Profiles;

namespace JellyFlix.Requests;

/// <summary>
/// Resolves requesters through the households kept by the profiles feature.
/// </summary>
/// <param name="profiles">The profile service.</param>
public sealed class HouseholdRequesterResolver(ProfileService profiles) : IRequesterResolver
{
    /// <inheritdoc />
    public async Task<Requester> ResolveAsync(Guid userId)
    {
        var household = await profiles.GetHouseholdAsync(userId).ConfigureAwait(false);
        var profile = household.Profiles.FirstOrDefault(p => p.Id.Equals(userId));
        return new Requester(household.OwnerId, profile?.IsKids ?? false);
    }
}
