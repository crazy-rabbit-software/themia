using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5400]..[S-5403] The view bar as chips or as one drop-down button.</summary>
public sealed class ViewBarStyleTests
{
    private static readonly PixelRect Area = new(0, 0, 960, 492);

    private static AppState ThreeViews()
    {
        var state = new AppState();
        foreach (var name in new[] { "A", "B", "C" })
        {
            var view = ViewState.Create(name);
            view.Id = name.ToLowerInvariant();
            state.Views.Add(view);
        }

        state.ActiveViewId = "a";
        return state;
    }

    [Fact]
    public void S5400_a_display_without_an_entry_has_chips()
        => Assert.Equal(ViewBarStyle.Chips, ViewBarRules.For(ThreeViews(), "m1").Style);

    [Fact]
    public void S5400_the_style_is_per_display_and_round_trips_through_the_state_file()
    {
        var state = ThreeViews();
        ViewBarRules.Update(state, "m1", bar => bar.Style = ViewBarStyle.Dropdown);

        var json = System.Text.Json.JsonSerializer.Serialize(state, AppStateJsonContext.Default.AppState);
        var parsed = AppStateStore.Parse(json);

        Assert.NotNull(parsed.State);
        Assert.Equal(ViewBarStyle.Dropdown, parsed.State!.ViewBars["m1"].Style);
        Assert.Equal(ViewBarStyle.Chips, ViewBarRules.For(parsed.State, "m2").Style);
        Assert.Equal(ViewBarStyle.Dropdown, parsed.State.ViewBars["m1"].Clone().Style);
    }

    [Fact]
    public void S5400_load_resets_an_unknown_style_to_chips_and_says_so()
    {
        var state = ThreeViews();
        state.ViewBars["m1"] = new ViewBarSettings { Style = (ViewBarStyle)9, Anchor = ViewBarAnchor.TopRight };

        var repairs = AppStateStore.Validate(state);

        Assert.Contains(repairs, r => r.Contains("unknown style", StringComparison.Ordinal));
        Assert.Equal(ViewBarStyle.Chips, state.ViewBars["m1"].Style);
        Assert.Equal(ViewBarAnchor.TopRight, state.ViewBars["m1"].Anchor);
    }

    [Theory]
    [InlineData(ViewBarAnchor.LeftCenter)]
    [InlineData(ViewBarAnchor.RightCenter)]
    [InlineData(ViewBarAnchor.BottomLeft)]
    [InlineData(ViewBarAnchor.TopCenter)]
    public void S5401_a_dropdown_is_a_row_at_every_anchor_with_the_width_as_its_room(ViewBarAnchor anchor)
    {
        Assert.False(ViewBarRules.IsVertical(anchor, ViewBarStyle.Dropdown));
        Assert.Equal(960 - (2 * ViewBarRules.EdgeMargin), ViewBarRules.Room(Area, anchor, new PixelRect(250, 8, 460, 44), ViewBarStyle.Dropdown));
    }

    [Fact]
    public void S5401_chips_at_a_side_still_stack()
    {
        Assert.True(ViewBarRules.IsVertical(ViewBarAnchor.LeftCenter, ViewBarStyle.Chips));
        Assert.True(ViewBarRules.IsVertical(ViewBarAnchor.RightCenter));
    }

    [Fact]
    public void S5402_a_dropdown_lists_every_view_on_the_bar_in_bar_order()
    {
        var everyChip = new ViewBarFit([0, 1, 2, 3], Overflow: false);

        Assert.Equal([0, 1, 2, 3], ViewBarRules.MenuViews(ViewBarStyle.Dropdown, 4, everyChip));
        Assert.Empty(ViewBarRules.MenuViews(ViewBarStyle.Dropdown, 0, new ViewBarFit([], false)));
    }

    [Fact]
    public void S5402_the_chip_bar_lists_only_the_views_left_off_it()
    {
        var fit = new ViewBarFit([0, 1, 4], Overflow: true);

        Assert.Equal([2, 3, 5], ViewBarRules.MenuViews(ViewBarStyle.Chips, 6, fit));
        Assert.Empty(ViewBarRules.MenuViews(ViewBarStyle.Chips, 3, new ViewBarFit([0, 1, 2], false)));
    }

    [Theory]
    [InlineData(ViewBarAnchor.BottomCenter, ViewBarMenuSide.Above)]
    [InlineData(ViewBarAnchor.TopLeft, ViewBarMenuSide.Below)]
    [InlineData(ViewBarAnchor.LeftCenter, ViewBarMenuSide.Right)]
    [InlineData(ViewBarAnchor.RightCenter, ViewBarMenuSide.Left)]
    public void S5402_the_dropdown_menu_opens_into_the_display_like_the_overflow_menu(ViewBarAnchor anchor, ViewBarMenuSide side)
    {
        Assert.Equal(side, ViewBarRules.MenuSide(anchor));

        // A button at the bottom: the menu above it is capped a margin short of the top of the area.
        var button = new PixelRect(16, 444, 140, 32);
        Assert.Equal(444 - ViewBarRules.EdgeMargin, ViewBarRules.MenuHeight(Area, button, ViewBarMenuSide.Above));
    }

    [Theory]
    [InlineData(-1, "g", 1)]
    [InlineData(1, "g", 3)]
    [InlineData(3, "g", 1)]
    [InlineData(0, "H", 2)]
    [InlineData(2, "w", 0)]
    [InlineData(0, "w", 0)]
    [InlineData(0, "z", -1)]
    [InlineData(0, " ", -1)]
    [InlineData(9, "g", 1)]
    public void S5403_type_ahead_steps_through_the_views_starting_with_the_letter_and_wraps(int from, string typed, int expected)
    {
        string[] names = ["Work", "Gaming", "home office", "Garden"];

        Assert.Equal(expected, ViewBarRules.TypeAhead(names, from, typed));
    }

    [Fact]
    public void S5403_type_ahead_in_an_empty_menu_finds_nothing()
        => Assert.Equal(-1, ViewBarRules.TypeAhead([], 0, "a"));
}
