using Themia.Core.Desktop;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5500]..[S-5591] The desktop layer's rules: the known set, the reconcile, the next free cell.</summary>
public sealed partial class DesktopLayerTests
{
    [Fact]
    public void S5522_the_known_set_keeps_distinct_non_empty_keys()
    {
        var state = new AppState();
        state.Desktop.Known = [@"C:\Users\a\Desktop\x.txt", "", @"c:\users\a\desktop\X.TXT", "  ", new string('k', DesktopReconcile.MaxKeyLength + 1), "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"];

        var repairs = DesktopReconcile.RepairKnown(state);

        Assert.Single(repairs);
        Assert.Equal([@"C:\Users\a\Desktop\x.txt", "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}"], state.Desktop.Known);
    }

    [Fact]
    public void S5522_a_null_known_set_is_an_empty_one_said_once()
    {
        var state = new AppState { Desktop = null! };
        Assert.Single(DesktopReconcile.RepairKnown(state));
        Assert.Empty(state.Desktop.Known);

        state.Desktop.Known = null!;
        Assert.Single(DesktopReconcile.RepairKnown(state));
        Assert.Empty(state.Desktop.Known);

        Assert.Empty(DesktopReconcile.RepairKnown(state));
    }
}
