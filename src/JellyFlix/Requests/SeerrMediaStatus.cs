using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace JellyFlix.Requests;

/// <summary>
/// Seerr's media status (<c>mediaInfo.status</c>).
/// </summary>
public enum SeerrMediaStatus
{
    /// <summary>No status given.</summary>
    None = 0,

    /// <summary>Not known to Seerr.</summary>
    Unknown = 1,

    /// <summary>Requested, waiting for approval.</summary>
    Pending = 2,

    /// <summary>Approved and being downloaded.</summary>
    Processing = 3,

    /// <summary>Some seasons are available.</summary>
    PartiallyAvailable = 4,

    /// <summary>In the library.</summary>
    Available = 5,

    /// <summary>Blocked from being requested.</summary>
    Blocklisted = 6,

    /// <summary>Was available, then removed.</summary>
    Deleted = 7
}
