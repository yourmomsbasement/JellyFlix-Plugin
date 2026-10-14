namespace JellyFlix.Requests.Ombi;

/// <summary>
/// An Ombi user (<c>GET /api/v1/Identity/Users</c>).
/// </summary>
public class OmbiUser
{
    /// <summary>
    /// Gets or sets the user name; users imported from Jellyfin keep their Jellyfin name.
    /// </summary>
    public string? UserName { get; set; }
}
