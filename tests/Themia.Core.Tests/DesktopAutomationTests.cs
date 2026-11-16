using Themia.Core.Desktop;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5576] The desktop's list to UI Automation: its children in the desktop's order.</summary>
public sealed class DesktopAutomationTests
{
    [Fact]
    public void S5576_items_come_first_in_their_place_the_rest_after_them_as_they_were()
    {
        // Positions in set: 0 for a child that is no desktop item (a card, the bar).
        int[] positions = [0, 3, 0, 1, 2, 0];

        Assert.Equal([3, 4, 1, 0, 2, 5], DesktopAutomation.Order(positions)!);
    }

    [Fact]
    public void S5576_children_already_in_order_need_no_change()
    {
        Assert.Null(DesktopAutomation.Order([1, 2, 3, 0, 0]));
        Assert.Null(DesktopAutomation.Order([]));
        Assert.Null(DesktopAutomation.Order([0, 0]));
    }
}
