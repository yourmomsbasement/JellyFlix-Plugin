using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JellyFlix.Requests.Api.Models;

/// <summary>
/// Where a title stands, as the web client shows it.
/// </summary>
public enum RequestStatus
{
    /// <summary>Nobody has asked for it.</summary>
    None,

    /// <summary>Requested and waiting for approval.</summary>
    Requested,

    /// <summary>Approved and on its way.</summary>
    Processing,

    /// <summary>Some seasons are in the library.</summary>
    PartiallyAvailable,

    /// <summary>In the library.</summary>
    Available,

    /// <summary>Can't be requested.</summary>
    Blocked
}
