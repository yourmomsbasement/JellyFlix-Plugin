using System.Collections.Generic;
using MediaBrowser.Model.Dto;

namespace JellyFlix.Search.Api.Models;

/// <summary>
/// Titles matching a description.
/// </summary>
public class DescriptionSearchDto
{
    /// <summary>
    /// Gets or sets the titles, best first.
    /// </summary>
    public IReadOnlyList<BaseItemDto> Items { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether titles were matched by meaning as well as by words.
    /// </summary>
    public bool IsSemantic { get; set; }
}
