using Themia.Core.Geometry;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1597] A selected card's handles and body take the pointer before any card drawn over it; [S-11809] widgets that
/// touch are fitted as one block in a picture.
/// </summary>
public sealed class SelectedHandlesTests
{
    /// <summary>A selected card under another that overlaps its right half; the drawing order stays: "top" over "under".</summary>
    private static readonly (string Id, DipRect Box)[] Cards = [("under", new DipRect(100, 100, 200, 100)), ("top", new DipRect(200, 80, 200, 160))];

    [Fact]
    public void S1597_regression_a_press_on_a_selected_cards_covered_edge_corner_or_body_goes_to_it()
    {
        // Owner, 2026-11-17: "when a widget is selected even if it clips behind another one i should be able to grab all
        // its edges and corners to resize it or move it".
        var selected = new HashSet<string> { "under" };

        Assert.Equal("under", SelectionRules.PressTarget(Cards, selected, 300, 150, grip: 6)); // its right edge, under "top"
        Assert.Equal("under", SelectionRules.PressTarget(Cards, selected, 303, 199, grip: 6)); // its bottom-right corner's grip, outside it
        Assert.Equal("under", SelectionRules.PressTarget(Cards, selected, 250, 150, grip: 6)); // its body, under "top"

        // Past its reach the card on top takes the press.
        Assert.Equal("top", SelectionRules.PressTarget(Cards, selected, 350, 150, grip: 6));
        Assert.Null(SelectionRules.PressTarget(Cards, selected, 10, 10, grip: 6));
    }

    [Fact]
    public void S1597_with_nothing_selected_the_card_on_top_takes_the_press()
    {
        Assert.Equal("top", SelectionRules.PressTarget(Cards, new HashSet<string>(), 250, 150, grip: 6));
        Assert.Equal("under", SelectionRules.PressTarget(Cards, new HashSet<string>(), 150, 150, grip: 6));
    }

    [Fact]
    public void S1597_two_selected_cards_overlapping_the_one_drawn_on_top_wins()
        => Assert.Equal("top", SelectionRules.PressTarget(Cards, new HashSet<string> { "under", "top" }, 250, 150, grip: 6));

    [Fact]
    public void S11809_widgets_that_touch_are_one_block_and_a_corner_alone_is_not_touching()
    {
        FracRect[] rects =
        [
            new(0.1, 0.1, 0.1, 0.1),
            new(0.2, 0.1, 0.1, 0.1), // beside the first
            new(0.1, 0.2, 0.2, 0.1), // under both
            new(0.3, 0.3, 0.1, 0.1), // corner to corner with the third only
            new(0.6, 0.6, 0.1, 0.1), // apart
        ];

        var blocks = SizeFit.Touching(rects);

        Assert.Equal([[0, 1, 2], [3], [4]], blocks);
    }
}
