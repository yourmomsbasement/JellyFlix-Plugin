using System;
using JellyFlix.Companion;
using Xunit;

namespace JellyFlix.Tests.Companion;

public class CollectionOrderTests
{
    private static readonly Guid _first = Guid.NewGuid();
    private static readonly Guid _second = Guid.NewGuid();
    private static readonly Guid _third = Guid.NewGuid();

    [Fact]
    public void NextIsTheFollowingRelease()
    {
        // Listed out of order, as collections often are
        var titles = new (Guid, DateTime?, int?)[]
        {
            (_third, new DateTime(2003, 12, 17, 0, 0, 0, DateTimeKind.Utc), 2003),
            (_first, new DateTime(2001, 12, 19, 0, 0, 0, DateTimeKind.Utc), 2001),
            (_second, null, 2002)
        };

        Assert.Equal(_second, CollectionOrder.NextAfter(titles, _first));
        Assert.Equal(_third, CollectionOrder.NextAfter(titles, _second));
        Assert.Null(CollectionOrder.NextAfter(titles, _third));
    }

    [Fact]
    public void NothingIsNextForATitleOutsideTheCollection()
    {
        Assert.Null(CollectionOrder.NextAfter([(_first, null, 2001)], _second));
    }
}
