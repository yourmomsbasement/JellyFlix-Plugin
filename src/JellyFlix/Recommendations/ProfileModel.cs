using System;
using System.Collections.Generic;
using JellyFlix.Recommendations.Engine;
using MediaBrowser.Controller.Entities;

namespace JellyFlix.Recommendations;

/// <summary>
/// Everything needed to answer recommendation requests for one profile.
/// </summary>
/// <param name="Engine">The engine over the profile's catalog.</param>
/// <param name="Taste">The profile's taste.</param>
/// <param name="Signals">The profile's signals.</param>
/// <param name="Exclude">Titles not to recommend.</param>
/// <param name="Watched">Titles the profile watched.</param>
/// <param name="Items">The catalog's library items by id.</param>
/// <param name="BuiltAt">When the model was built.</param>
/// <param name="CatalogVersion">The version of the shared catalog the model was built from.</param>
public sealed record ProfileModel(
    RecommendationEngine Engine,
    SparseVector Taste,
    IReadOnlyList<TasteSignal> Signals,
    IReadOnlySet<Guid> Exclude,
    IReadOnlySet<Guid> Watched,
    IReadOnlyDictionary<Guid, BaseItem> Items,
    DateTime BuiltAt,
    int CatalogVersion);
