using Themia.App.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-7032] The pure ladder's lined measures are the WinUI parts' own, so what it plans is what the list draws.</summary>
public sealed class KitLinedConstantsTests
{
    [Fact]
    public void S7032_the_ladders_measures_are_the_lists()
    {
        Assert.Equal(Fluent.Space4 + RowFocus.Ring, KitLadder.LinedInset);
        Assert.Equal(KitLadder.LeadSize + Fluent.Space2, KitLadder.LeadLane);
        Assert.Equal(Fluent.RowHeightCompact, KitLadder.CompactRow);
        Assert.Equal(Fluent.CompactBelow, KitLadder.CompactBelow);
        Assert.Equal(Fluent.Space2, KitLadder.ColumnGap);
    }
}
