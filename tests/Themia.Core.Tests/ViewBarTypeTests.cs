using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5400]..[S-5406] The view bar inline or as one select button, and its labels.</summary>
public sealed class ViewBarTypeTests
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
    public void S5400_a_display_without_an_entry_is_inline_with_icon_and_text()
    {
        var bar = ViewBarRules.For(ThreeViews(), "m1");

        Assert.Equal((ViewBarType.Inline, ViewBarLabels.IconAndText), (bar.Type, bar.Labels));
    }

    [Fact]
    public void S5400_the_type_and_labels_are_per_display_and_round_trip_through_the_state_file()
    {
        var state = ThreeViews();
        ViewBarRules.Update(state, "m1", bar =>
        {
            bar.Type = ViewBarType.Select;
            bar.Labels = ViewBarLabels.TextOnly;
        });

        var json = System.Text.Json.JsonSerializer.Serialize(state, StateJson.AppState);
        var parsed = AppStateStore.Parse(json);

        Assert.NotNull(parsed.State);
        var bar = parsed.State!.ViewBars["m1"];
        var other = ViewBarRules.For(parsed.State, "m2");
        Assert.Equal((ViewBarType.Select, ViewBarLabels.TextOnly), (bar.Type, bar.Labels));
        Assert.Equal((ViewBarType.Inline, ViewBarLabels.IconAndText), (other.Type, other.Labels));
        Assert.Equal((ViewBarType.Select, ViewBarLabels.TextOnly), (bar.Clone().Type, bar.Clone().Labels));
    }

    [Fact]
    public void S5400_load_resets_an_unknown_type_to_inline_and_says_so()
    {
        var state = ThreeViews();
        state.ViewBars["m1"] = new ViewBarSettings { Type = (ViewBarType)9, Labels = ViewBarLabels.IconOnly, Anchor = ViewBarAnchor.TopRight };

        var repairs = AppStateStore.Validate(state);

        Assert.Contains(repairs, r => r.Contains("unknown type", StringComparison.Ordinal));
        Assert.Equal(ViewBarType.Inline, state.ViewBars["m1"].Type);
        Assert.Equal(ViewBarLabels.IconOnly, state.ViewBars["m1"].Labels);
        Assert.Equal(ViewBarAnchor.TopRight, state.ViewBars["m1"].Anchor);
    }

    [Fact]
    public void S5405_load_resets_unknown_labels_to_icon_and_text_and_says_so()
    {
        var state = ThreeViews();
        state.ViewBars["m1"] = new ViewBarSettings { Labels = (ViewBarLabels)7, Type = ViewBarType.Select };

        var repairs = AppStateStore.Validate(state);

        Assert.Contains(repairs, r => r.Contains("unknown labels", StringComparison.Ordinal));
        Assert.Equal(ViewBarLabels.IconAndText, state.ViewBars["m1"].Labels);
        Assert.Equal(ViewBarType.Select, state.ViewBars["m1"].Type);
    }

    [Fact]
    public void S5405_an_unknown_labels_number_in_the_file_is_repaired_as_it_is_read()
    {
        var state = ThreeViews();
        ViewBarRules.Update(state, "m1", bar => bar.Labels = ViewBarLabels.TextOnly);
        var written = System.Text.Json.JsonSerializer.Serialize(state, StateJson.AppState);
        var json = written.Replace("\"labels\": 2", "\"labels\": 42", StringComparison.Ordinal);

        var parsed = AppStateStore.Parse(json);

        Assert.NotEqual(written, json);
        Assert.Equal(ViewBarLabels.IconAndText, parsed.State!.ViewBars["m1"].Labels);
    }

    [Theory]
    [InlineData(ViewBarLabels.IconAndText, true, true)]
    [InlineData(ViewBarLabels.IconOnly, true, false)]
    [InlineData(ViewBarLabels.TextOnly, false, true)]
    public void S5405_each_label_choice_draws_the_mark_the_name_or_both(ViewBarLabels labels, bool mark, bool name)
        => Assert.Equal((mark, name), (ViewBarRules.ShowsMark(labels), ViewBarRules.ShowsName(labels)));

    [Theory]
    [InlineData(ViewBarLabels.IconOnly, null, "Work")]
    [InlineData(ViewBarLabels.IconAndText, null, null)]
    [InlineData(ViewBarLabels.TextOnly, null, null)]
    [InlineData(ViewBarLabels.TextOnly, "Work (shown on new displays)", "Work (shown on new displays)")]
    [InlineData(ViewBarLabels.IconOnly, "Work (shown on new displays)", "Work (shown on new displays)")]
    public void S5405_a_chip_has_its_name_as_a_tooltip_only_where_it_does_not_say_it(ViewBarLabels labels, string? note, string? tip)
        => Assert.Equal(tip, ViewBarRules.ChipTip(labels, "Work", note));

    [Theory]
    [InlineData(ViewBarLabels.TextOnly, false, true, false, ViewBarDot.Drawn)]
    [InlineData(ViewBarLabels.TextOnly, true, true, true, ViewBarDot.Drawn)]
    [InlineData(ViewBarLabels.TextOnly, false, false, true, ViewBarDot.None)]
    [InlineData(ViewBarLabels.TextOnly, true, false, true, ViewBarDot.Room)]
    [InlineData(ViewBarLabels.TextOnly, true, false, false, ViewBarDot.None)]
    [InlineData(ViewBarLabels.IconAndText, false, true, true, ViewBarDot.None)]
    [InlineData(ViewBarLabels.IconOnly, true, true, true, ViewBarDot.None)]
    public void S5406_text_only_shows_a_views_colour_as_a_dot_and_a_column_keeps_its_room(ViewBarLabels labels, bool vertical, bool colour, bool anyColour, ViewBarDot dot)
        => Assert.Equal(dot, ViewBarRules.Dot(labels, vertical, colour, anyColour));

    [Theory]
    [InlineData(ViewBarAnchor.LeftCenter)]
    [InlineData(ViewBarAnchor.RightCenter)]
    [InlineData(ViewBarAnchor.BottomLeft)]
    [InlineData(ViewBarAnchor.TopCenter)]
    public void S5401_a_select_bar_is_a_row_at_every_anchor_with_the_width_as_its_room(ViewBarAnchor anchor)
    {
        Assert.False(ViewBarRules.IsVertical(anchor, ViewBarType.Select));
        Assert.Equal(960 - (2 * ViewBarRules.EdgeMargin), ViewBarRules.Room(Area, anchor, new PixelRect(250, 8, 460, 44), ViewBarType.Select));
    }

    [Fact]
    public void S5401_an_inline_bar_at_a_side_still_stacks()
    {
        Assert.True(ViewBarRules.IsVertical(ViewBarAnchor.LeftCenter, ViewBarType.Inline));
        Assert.True(ViewBarRules.IsVertical(ViewBarAnchor.RightCenter));
    }

    [Fact]
    public void S5402_a_select_bar_lists_every_view_on_the_bar_in_bar_order()
    {
        var everyChip = new ViewBarFit([0, 1, 2, 3], Overflow: false);

        Assert.Equal([0, 1, 2, 3], ViewBarRules.MenuViews(ViewBarType.Select, 4, everyChip));
        Assert.Empty(ViewBarRules.MenuViews(ViewBarType.Select, 0, new ViewBarFit([], false)));
    }

    [Fact]
    public void S5402_a_select_bar_lists_only_the_views_on_the_bar_in_the_view_order()
    {
        var state = ThreeViews();
        ViewBarRules.Update(state, "m1", bar =>
        {
            bar.Type = ViewBarType.Select;
            bar.VisibleViews = ["c", "a"];
        });

        var offered = ViewBarRules.VisibleViews(state.Views, ViewBarRules.For(state, "m1"));
        var listed = ViewBarRules.MenuViews(ViewBarType.Select, offered.Count, new ViewBarFit([], false)).Select(i => offered[i].Id);

        Assert.Equal(["a", "c"], listed);
    }

    [Fact]
    public void S5402_the_inline_bar_lists_only_the_views_left_off_it()
    {
        var fit = new ViewBarFit([0, 1, 4], Overflow: true);

        Assert.Equal([2, 3, 5], ViewBarRules.MenuViews(ViewBarType.Inline, 6, fit));
        Assert.Empty(ViewBarRules.MenuViews(ViewBarType.Inline, 3, new ViewBarFit([0, 1, 2], false)));
    }

    [Theory]
    [InlineData(ViewBarAnchor.BottomCenter, ViewBarMenuSide.Above)]
    [InlineData(ViewBarAnchor.TopLeft, ViewBarMenuSide.Below)]
    [InlineData(ViewBarAnchor.LeftCenter, ViewBarMenuSide.Right)]
    [InlineData(ViewBarAnchor.RightCenter, ViewBarMenuSide.Left)]
    public void S5402_the_select_menu_opens_into_the_display_like_the_overflow_menu(ViewBarAnchor anchor, ViewBarMenuSide side)
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
