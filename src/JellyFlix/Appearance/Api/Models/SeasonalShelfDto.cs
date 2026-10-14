using System.Collections.Generic;
using MediaBrowser.Model.Dto;

namespace JellyFlix.Appearance.Api.Models;

/// <summary>
/// A curated seasonal shelf.
/// </summary>
public class SeasonalShelfDto
{
    /// <summary>
    /// Gets or sets the shelf's name for the client, e.g. <c>SeasonalHalloweenPicks</c>.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the titles.
    /// </summary>
    public IReadOnlyList<BaseItemDto> Items { get; set; } = [];
}
