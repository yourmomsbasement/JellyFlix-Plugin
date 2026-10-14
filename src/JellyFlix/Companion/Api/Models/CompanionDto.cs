using System.Collections.Generic;
using MediaBrowser.Model.Dto;

namespace JellyFlix.Companion.Api.Models;

/// <summary>
/// What to offer once a title finishes.
/// </summary>
public class CompanionDto
{
    /// <summary>
    /// Gets or sets the name of the collection the title is in, such as "The Lord of the Rings Collection".
    /// </summary>
    public string? CollectionName { get; set; }

    /// <summary>
    /// Gets or sets the collection's next title.
    /// </summary>
    public BaseItemDto? NextInCollection { get; set; }

    /// <summary>
    /// Gets or sets the title's extras: behind the scenes, featurettes, deleted scenes and so on.
    /// </summary>
    public IReadOnlyList<BaseItemDto> Extras { get; set; } = [];

    /// <summary>
    /// Gets or sets who wrote the music.
    /// </summary>
    public IReadOnlyList<string> Composers { get; set; } = [];

    /// <summary>
    /// Gets or sets the soundtrack albums in the music library.
    /// </summary>
    public IReadOnlyList<BaseItemDto> Soundtracks { get; set; } = [];
}
