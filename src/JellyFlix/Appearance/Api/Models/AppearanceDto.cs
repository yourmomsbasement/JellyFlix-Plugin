using System.Collections.Generic;

namespace JellyFlix.Appearance.Api.Models;

/// <summary>
/// How the JellyFlix interface should look right now.
/// </summary>
public class AppearanceDto
{
    /// <summary>
    /// Gets or sets the seasonal look of the home page.
    /// </summary>
    public Season Season { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether buttons, backgrounds and progress bars take their color from the title's artwork.
    /// </summary>
    public bool DynamicColors { get; set; }

    /// <summary>
    /// Gets or sets the season's curated shelves, with titles the profile may see.
    /// </summary>
    public IReadOnlyList<SeasonalShelfDto> Shelves { get; set; } = [];
}
