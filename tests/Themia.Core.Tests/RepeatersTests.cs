using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Series;
using Xunit;

namespace Themia.Core.Tests;

public class RepeatersTests
{
    // 1000 × 1000 DIPs: the grid is 100 × 100 cells of 0.01.
    private static readonly ScreenMetrics Screen = new(1000, 1000);

    private static WidgetInstance W(string id, WidgetConfig config, double x, double y, double w, double h)
        => new() { Id = id, Config = config, Rect = new FracRect(x, y, w, h) };

    private static RepeaterFake R(WidgetInstance repeater) => (RepeaterFake)repeater.Config;

    /// <summary>A day repeater at (0.1, 0.1), 0.2 wide and 0.5 tall, holding one day atom 0.2 × 0.1 at its corner.</summary>
    private static (ViewState View, WidgetInstance Repeater, WidgetInstance Atom) Days(int spacing = 0, RepeatDirection direction = RepeatDirection.Down)
    {
        var atom = W("atom", new DayFake(), 0.1, 0.1, 0.2, 0.1);
        var repeater = W("rep", new RepeaterFake { Members = ["atom"], RepeatBy = SeriesType.Day, Spacing = spacing, Direction = direction }, 0.1, 0.1, 0.2, 0.5);
        var view = ViewState.Create("v");
        view.Widgets.AddRange([repeater, atom]);
        return (view, repeater, atom);
    }

    [Fact]
    public void S15503_S15514_the_template_extent_and_spacing_make_the_pitch_and_the_box_decides_how_many_slots()
    {
        var (view, repeater, _) = Days(spacing: 2);
        var grid = Repeaters.Grid(view, new RepeaterOn(repeater, R(repeater)), Screen);

        Assert.Equal(0.1, grid.ExtentH, 9);
        Assert.Equal(0.12, grid.PitchY, 9);
        Assert.Equal(4, grid.Fit); // 0.5 + 0.02 over 0.12
        Assert.Equal((0, 0.36), (grid.Offset(3).X, Math.Round(grid.Offset(3).Y, 9)));
    }

    [Fact]
    public void S15504_wrap_fills_across_then_down_and_scrolls_by_lines()
    {
        var (view, repeater, atom) = Days(direction: RepeatDirection.Wrap);
        atom.Rect = new FracRect(0.1, 0.1, 0.1, 0.1);
        repeater.Rect = new FracRect(0.1, 0.1, 0.3, 0.2);
        var grid = Repeaters.Grid(view, new RepeaterOn(repeater, R(repeater)), Screen);

        Assert.Equal((3, 2), (grid.Columns, grid.Lines));
        Assert.Equal(new FracRect(0.1, 0.2, 0.1, 0.1), Round(grid.Slot(3)));
        Assert.Equal(3, grid.ScrollUnit);
        Assert.Equal(3, grid.MaxScroll(14)); // 5 lines, 2 shown
        Assert.Equal(6, grid.First(2, 14));
        Assert.Equal(9, grid.First(9, 14));
    }

    private static FracRect Round(FracRect r) => new(Math.Round(r.X, 9), Math.Round(r.Y, 9), Math.Round(r.W, 9), Math.Round(r.H, 9));

    [Fact]
    public void S15502_S15514_a_repeater_draws_a_copy_of_its_template_in_each_slot_it_has_a_value_for()
    {
        var (view, repeater, _) = Days();
        R(repeater).Start = 11; // days 11, 12 and 13: three values, five slots

        var plan = Repeaters.Plan(view, Screen);

        Assert.Equal(2, plan.Copies.Count);
        Assert.Equal([0.2, 0.3], plan.Copies.Select(c => Math.Round(c.Rect.Y, 9)));
        Assert.All(plan.Copies, c => Assert.Equal("rep", c.Owner));
        Assert.Equal(0, plan.Templates["atom"]["rep"]);
        Assert.Equal([1, 2], plan.Copies.Select(c => c.Path["rep"]));
        Assert.Empty(plan.Statuses);
    }

    [Fact]
    public void S15511_a_row_shows_its_value_in_the_bound_slot_and_the_stored_config_is_untouched()
    {
        var (view, repeater, atom) = Days();
        R(repeater).Start = 2;
        var plan = Repeaters.Plan(view, Screen);
        var row = plan.Copies[1];

        var resolved = (DayFake)Repeaters.Resolve(view, atom, atom.Config, row.Path, plan.Values);

        Assert.Equal(4, resolved.Day);
        Assert.Equal(0, ((DayFake)atom.Config).Day);
        Assert.Equal(2, ((DayFake)Repeaters.Resolve(view, atom, atom.Config, plan.Templates["atom"], plan.Values)).Day);
    }

    [Fact]
    public void S15512_a_slot_taken_out_by_hand_keeps_its_own_value_in_every_row_and_can_go_back()
    {
        var (view, repeater, atom) = Days();
        ((DayFake)atom.Config).Day = 5;

        Assert.True(Repeaters.Unbind(view, atom, SeriesSlots.Day));
        var plan = Repeaters.Plan(view, Screen);
        Assert.All(plan.Copies, c => Assert.Equal(5, ((DayFake)Repeaters.Resolve(view, atom, atom.Config, c.Path, plan.Values)).Day));

        Assert.True(Repeaters.Bind(view, atom, SeriesSlots.Day));
        Assert.True(Repeaters.IsBound(view, atom, SeriesSlots.Day));
        Assert.Empty(R(repeater).Unbound);
    }

    [Fact]
    public void S15512_a_widget_with_two_slots_of_the_type_binds_neither_until_one_is_chosen()
    {
        var (view, repeater, _) = Days();
        var twoDrives = new TwoDrivesFake();
        var both = W("both", twoDrives, 0.1, 0.1, 0.2, 0.1);
        view.Widgets.Add(both);
        R(repeater).RepeatBy = SeriesType.Drive;
        R(repeater).Members = ["both"];

        Assert.Empty(Repeaters.Bound(view, both));

        Assert.True(Repeaters.Bind(view, both, SeriesSlots.DiskDrive));
        Assert.Equal([SeriesSlots.DiskDrive], Repeaters.Bound(view, both).Select(b => b.Slot));

        Assert.True(Repeaters.Bind(view, both, SeriesSlots.Drive));
        Assert.Equal([SeriesSlots.Drive], Repeaters.Bound(view, both).Select(b => b.Slot));
    }

    [Fact]
    public void S15520_a_repeater_with_no_value_draws_its_template_empty_and_says_so_in_the_next_slot()
    {
        var (view, repeater, atom) = Days();
        R(repeater).Start = 20; // past any forecast: no day

        var plan = Repeaters.Plan(view, Screen);

        Assert.Empty(plan.Copies);
        var status = Assert.Single(plan.Statuses);
        Assert.Equal(0.2, Math.Round(status.Rect.Y, 9));
        Assert.Equal(DayRangeSource.Reach, ((DayFake)Repeaters.Resolve(view, atom, atom.Config, plan.Templates["atom"], plan.Values)).Day);
    }

    [Fact]
    public void S15520_a_repeater_holding_nothing_says_how_it_is_used_rather_than_show_nothing()
    {
        var repeater = W("rep", new RepeaterFake(), 0.1, 0.1, 0.2, 0.2);
        var view = ViewState.Create("v");
        view.Widgets.Add(repeater);

        var shown = Repeaters.ShownOf(view, new RepeaterOn(repeater, R(repeater)), Screen, Repeaters.ValuesOf(view), editing: false);

        Assert.False(string.IsNullOrEmpty(shown.Status));
        Assert.Equal(new RepeaterBox(0, 0, 1, 1), shown.StatusSlot);
    }

    [Fact]
    public void S15515_the_scroll_shifts_which_values_the_slots_show_and_stays_in_range()
    {
        var (view, repeater, _) = Days(); // 14 days, 5 slots
        R(repeater).Scroll = 99;

        Assert.True(Repeaters.Lay(view, Screen));
        Assert.Equal(9, R(repeater).Scroll);
        var plan = Repeaters.Plan(view, Screen);
        Assert.Equal(9, plan.Templates["atom"]["rep"]);
        Assert.Equal(13, plan.Copies[^1].Path["rep"]);
    }

    [Fact]
    public void S15526_a_widget_let_go_on_a_copied_row_joins_the_template_at_the_same_place_in_the_row()
    {
        var (view, repeater, _) = Days();
        var drop = W("drop", new DayFake(), 0.15, 0.32, 0.05, 0.05); // slot 2 (0.3..0.4), 0.05 in from its left, 0.02 down
        view.Widgets.Add(drop);

        Assert.True(Repeaters.Land(view, ["drop"], Screen));

        Assert.Equal(["atom", "drop"], R(repeater).Members);
        Assert.Equal(new FracRect(0.15, 0.12, 0.05, 0.05), Round(drop.Rect));
    }

    [Fact]
    public void S15526_a_member_let_go_outside_leaves_and_keeps_the_first_rows_value()
    {
        var (view, repeater, atom) = Days();
        R(repeater).Start = 3;
        atom.Rect = new FracRect(0.6, 0.6, 0.2, 0.1);

        Assert.True(Repeaters.Land(view, ["atom"], Screen));

        Assert.Empty(R(repeater).Members);
        Assert.Equal(3, ((DayFake)atom.Config).Day);
    }

    [Fact]
    public void S15526_members_carried_with_their_repeater_stay_where_they_are_in_it()
    {
        var (view, repeater, atom) = Days();
        repeater.Rect = repeater.Rect with { X = 0.5 };
        atom.Rect = atom.Rect with { X = 0.5 };

        Assert.False(Repeaters.Land(view, ["rep", "atom"], Screen));
        Assert.Equal(["atom"], R(repeater).Members);
    }

    [Fact]
    public void S15533_a_member_moved_on_its_template_moves_its_copies_before_the_drop_and_nothing_is_stored()
    {
        var (view, repeater, atom) = Days();
        (R(repeater).DayWidth, R(repeater).DayHeight) = (20, 10); // laid once: its day box is its part's reach

        var (shown, plan) = Repeaters.Preview(view, new Dictionary<string, FracRect> { ["atom"] = new(0.15, 0.1, 0.2, 0.1) }, Screen);

        Assert.Equal(4, plan.Copies.Count);
        Assert.All(plan.Copies, c => Assert.Equal(0.15, Math.Round(c.Rect.X, 9)));
        Assert.Equal(0.25, Math.Round(shown.Find("rep")!.Rect.W, 9)); // grown to its template row, as the drop would
        Assert.Equal(new FracRect(0.1, 0.1, 0.2, 0.1), atom.Rect);
        Assert.Equal(new FracRect(0.1, 0.1, 0.2, 0.5), repeater.Rect);
    }

    [Fact]
    public void S15533_a_widget_dragged_onto_a_row_shows_its_copies_before_the_drop_and_does_not_join_yet()
    {
        var (view, repeater, _) = Days();
        view.Widgets.Add(W("drop", new DayFake(), 0.6, 0.6, 0.05, 0.05));

        var (_, plan) = Repeaters.Preview(view, new Dictionary<string, FracRect> { ["drop"] = new(0.15, 0.32, 0.05, 0.05) }, Screen);

        Assert.Equal(4, plan.Copies.Count(c => c.Template.Id == "drop"));
        Assert.Contains("drop", plan.Templates.Keys);
        Assert.Equal(["atom"], R(repeater).Members);
        Assert.Equal(new FracRect(0.6, 0.6, 0.05, 0.05), view.Find("drop")!.Rect);
    }

    [Fact]
    public void S15533_a_member_dragged_out_loses_its_copies_and_keeps_its_own_value_until_the_drop()
    {
        var (view, repeater, atom) = Days();
        R(repeater).Start = 3;

        var (_, plan) = Repeaters.Preview(view, new Dictionary<string, FracRect> { ["atom"] = new(0.6, 0.6, 0.2, 0.1) }, Screen);

        Assert.Empty(plan.Copies);
        Assert.Equal(["atom"], R(repeater).Members);
        Assert.Equal(0, ((DayFake)atom.Config).Day);
    }

    [Fact]
    public void S15528_a_container_takes_only_what_its_day_can_show_and_never_another_container()
    {
        var fence = W("fence", new FenceFake(), 0.1, 0.1, 0.1, 0.1);
        var icon = W("icon", new DeskIconFake(), 0.1, 0.1, 0.1, 0.1);
        var plain = W("plain", new PlainFake(), 0.1, 0.1, 0.1, 0.1);
        var day = W("day", new DayFake(), 0.1, 0.1, 0.1, 0.1);
        var a = W("a", new RepeaterFake { RepeatBy = SeriesType.Day, Members = ["b", "fence", "icon", "gone", "a", "plain", "day"] }, 0.1, 0.1, 0.4, 0.4);
        var b = W("b", new RepeaterFake { RepeatBy = SeriesType.Day }, 0.1, 0.1, 0.2, 0.2);
        var state = new AppState { Views = [ViewState.Create("v")] };
        state.Views[0].Widgets.AddRange([a, b, fence, icon, plain, day]);

        var said = Repeaters.Repair(state);

        Assert.Equal(["day"], R(a).Members);
        Assert.NotEmpty(said);
        Assert.True(Repeaters.CanHold(R(a), day));
        Assert.False(Repeaters.CanHold(R(a), plain));
        Assert.False(Repeaters.CanHold(R(a), b));
    }

    [Fact]
    public void S15528_a_widget_it_does_not_take_let_go_over_it_stays_outside()
    {
        var (view, repeater, _) = Days();
        var drop = W("drop", new PlainFake(), 0.15, 0.32, 0.05, 0.05);
        view.Widgets.Add(drop);

        Assert.False(Repeaters.Land(view, ["drop"], Screen));

        Assert.Equal(["atom"], R(repeater).Members);
        Assert.Equal(new FracRect(0.15, 0.32, 0.05, 0.05), drop.Rect);
    }

    [Fact]
    public void S15526_a_part_let_go_half_out_at_the_top_or_the_start_lands_at_the_containers_corner()
    {
        var (view, repeater, atom) = Days();
        atom.Rect = new FracRect(0.06, 0.07, 0.2, 0.1); // its centre still on the first day, its corner before the container's

        Assert.True(Repeaters.Land(view, ["atom"], Screen));

        Assert.Equal(["atom"], R(repeater).Members);
        Assert.Equal(new FracRect(0.1, 0.1, 0.2, 0.1), Round(atom.Rect));
    }

    [Fact]
    public void S15529_a_part_shows_its_containers_place_and_keeps_it_when_it_leaves()
    {
        var part = W("part", new PlacedDayFake(), 0.1, 0.1, 0.2, 0.1);
        var forecast = W("fc", new ForecastFake { Members = ["part"], Latitude = 47.37, Longitude = 8.54, LocationName = "Zurich" }, 0.1, 0.1, 0.2, 0.5);
        var view = ViewState.Create("v");
        view.Widgets.AddRange([forecast, part]);

        var plan = Repeaters.Plan(view, Screen);
        var shown = (PlacedDayFake)Repeaters.Resolve(view, part, part.Config, plan.Copies[0].Path, plan.Values);

        Assert.Equal(47.37, shown.Latitude);
        Assert.Equal(8.54, shown.Longitude);
        Assert.Equal("Zurich", shown.LocationName);
        Assert.Equal(1, shown.Day);
        Assert.Null(((PlacedDayFake)part.Config).Latitude); // the stored config is never touched
        Assert.Same(forecast.Config, Repeaters.Placer(view, part, part.Config));

        part.Rect = new FracRect(0.7, 0.7, 0.2, 0.1);
        Assert.True(Repeaters.Land(view, ["part"], Screen));
        Assert.Equal(47.37, ((PlacedDayFake)part.Config).Latitude);
        Assert.Equal("Zurich", ((PlacedDayFake)part.Config).LocationName);
    }

    // ---- [S-15535] the day box and the container's size are one ------------------------------------

    /// <summary>A day row of three parts side by side (8, 4 and 8 cells wide, 4 tall) at the container's corner, its day box 20 × 4.</summary>
    private static (ViewState View, WidgetInstance Forecast, WidgetInstance[] Parts) Row(RepeatDirection direction = RepeatDirection.Down)
    {
        var parts = new[] { W("a", new DayFake(), 0.10, 0.1, 0.08, 0.04), W("b", new DayFake(), 0.18, 0.1, 0.04, 0.04), W("c", new DayFake(), 0.22, 0.1, 0.08, 0.04) };
        var forecast = W("fc", new RepeaterFake { Members = ["a", "b", "c"], RepeatBy = SeriesType.Day, Direction = direction, DayWidth = 20, DayHeight = 4 }, 0.1, 0.1, 0.2, 0.28);
        var view = ViewState.Create("v");
        view.Widgets.Add(forecast);
        view.Widgets.AddRange(parts);
        return (view, forecast, parts);
    }

    [Fact]
    public void S15535_parts_that_touch_still_touch_on_whole_cells_at_every_size_of_the_day_box()
    {
        for (var width = 3; width <= 60; width++)
        {
            var (view, forecast, parts) = Row();
            Repeaters.Resize(view, new RepeaterOn(forecast, R(forecast)), width, 4, Screen);

            Assert.Equal(parts[0].Rect.Right, parts[1].Rect.X, 9);
            Assert.Equal(parts[1].Rect.Right, parts[2].Rect.X, 9);
            Assert.Equal(forecast.Rect.X, parts[0].Rect.X, 9);
            Assert.All(parts, p => Assert.Equal(Math.Round(p.Rect.X * 100), p.Rect.X * 100, 6));
            Assert.All(parts, p => Assert.True(p.Rect.W * 100 >= 1 - 1e-6));
            Assert.Equal(R(forecast).DayWidth / 100.0, parts[2].Rect.Right - forecast.Rect.X, 9);
        }
    }

    [Fact]
    public void S15535_the_day_box_resized_resizes_the_container_on_its_tied_side()
    {
        var (rows, forecast, _) = Row();
        Assert.True(Repeaters.Resize(rows, new RepeaterOn(forecast, R(forecast)), 30, 5, Screen));
        Assert.Equal((0.3, 0.28), (Math.Round(forecast.Rect.W, 9), Math.Round(forecast.Rect.H, 9)));
        Assert.Equal((30, 5), (R(forecast).DayWidth, R(forecast).DayHeight));

        var (columns, across, _) = Row(RepeatDirection.Across);
        Repeaters.Resize(columns, new RepeaterOn(across, R(across)), 30, 5, Screen);
        Assert.Equal((0.2, 0.05), (Math.Round(across.Rect.W, 9), Math.Round(across.Rect.H, 9)));

        var (grid, wrap, _) = Row(RepeatDirection.Wrap);
        Repeaters.Resize(grid, new RepeaterOn(wrap, R(wrap)), 30, 5, Screen);
        Assert.Equal((0.2, 0.28), (Math.Round(wrap.Rect.W, 9), Math.Round(wrap.Rect.H, 9)));
    }

    [Fact]
    public void S15535_the_container_resized_on_its_tied_side_resizes_the_day_box_and_its_parts()
    {
        var (view, forecast, parts) = Row();
        forecast.Rect = forecast.Rect with { W = 0.4 };

        Assert.True(Repeaters.Lay(view, Screen));

        Assert.Equal(40, R(forecast).DayWidth);
        Assert.Equal(0.5, Math.Round(parts[2].Rect.Right, 9));
        Assert.Equal(parts[0].Rect.Right, parts[1].Rect.X, 9);

        // Shorter than one day, it grows back to a day: it never shows less.
        forecast.Rect = forecast.Rect with { H = 0.01 };
        Repeaters.Lay(view, Screen);
        Assert.Equal(0.04, Math.Round(forecast.Rect.H, 9));
    }

    [Fact]
    public void S15535_a_part_moved_past_the_day_box_grows_it_and_scales_nothing()
    {
        var (view, forecast, parts) = Row();
        parts[2].Rect = parts[2].Rect with { X = 0.3 }; // its right edge 18 cells past the corner... now 28

        Assert.True(Repeaters.Lay(view, Screen));

        Assert.Equal(28, R(forecast).DayWidth);
        Assert.Equal(0.28, Math.Round(forecast.Rect.W, 9));
        Assert.Equal(new FracRect(0.10, 0.1, 0.08, 0.04), parts[0].Rect);
        Assert.Equal(new FracRect(0.3, 0.1, 0.08, 0.04), parts[2].Rect);
    }

    [Fact]
    public void S15536_it_shows_as_many_days_as_it_is_set_to_and_the_rest_of_them_scroll()
    {
        var (view, forecast, _) = Row();
        var fake = (CountedFake)(forecast.Config = new CountedFake { Members = ["a", "b", "c"], RepeatBy = SeriesType.Day, DayWidth = 20, DayHeight = 4, Days = 5 });

        Assert.Equal(["0", "1", "2", "3", "4"], Repeaters.ValuesOf(view)["fc"]);

        // Room for three days of the five: two scroll.
        forecast.Rect = forecast.Rect with { H = 0.12 };
        var grid = Repeaters.Grid(view, new RepeaterOn(forecast, fake), Screen);
        Assert.Equal((3, 2), (grid.Fit, grid.MaxScroll(5)));
        fake.Scroll = 9;
        Repeaters.Lay(view, Screen);
        Assert.Equal(2, fake.Scroll);
        Assert.Equal([3, 4], Repeaters.Plan(view, Screen).Copies.Where(c => c.Template.Id == "a").Select(c => c.Path["fc"]));
    }

    [Fact]
    public void S15527_a_repeaters_descendants_are_everything_it_holds_however_deep()
    {
        var atom = W("atom", new DayFake(), 0.1, 0.1, 0.1, 0.1);
        var inner = W("inner", new RepeaterFake { Members = ["atom"] }, 0.1, 0.1, 0.1, 0.1);
        var outer = W("outer", new RepeaterFake { Members = ["inner"] }, 0.1, 0.1, 0.2, 0.2);
        var view = ViewState.Create("v");
        view.Widgets.AddRange([outer, inner, atom]);

        Assert.Equal(["inner", "atom"], Repeaters.Descendants(view, "outer"));
        Assert.Equal(["inner", "atom", "outer"], Repeaters.WithDescendants(view, ["inner", "outer"]));
        Assert.Equal("outer", Repeaters.Outermost(view, "atom")?.Widget.Id);
    }

    [Fact]
    public void S15500_a_copy_renames_its_members_and_binds_and_leaves_out_what_it_did_not_copy()
    {
        var copy = W("new-rep", new RepeaterFake { Members = ["atom", "left-out"], Unbound = ["atom/day", "left-out/day"] }, 0, 0, 0.1, 0.1);

        Repeaters.Remap([copy], new Dictionary<string, string> { ["atom"] = "new-atom", ["rep"] = "new-rep" });

        Assert.Equal(["new-atom"], R(copy).Members);
        Assert.Equal(["new-atom/day"], R(copy).Unbound);
    }

    [Fact]
    public void S15514_its_natural_is_a_slot_for_every_value()
    {
        var (view, repeater, _) = Days(spacing: 1);
        R(repeater).Start = 10; // four days

        Assert.True(Repeaters.FitNatural(view, new RepeaterOn(repeater, R(repeater)), Screen));

        Assert.Equal(new FracRect(0.1, 0.1, 0.2, 0.43), Round(repeater.Rect));
    }

    [Fact]
    public void S15500_an_undo_snapshot_keeps_the_members()
    {
        var (_, repeater, _) = Days();
        var shape = WidgetShape.Of(repeater);
        R(repeater).Members.Clear();

        Assert.Equal(["atom"], shape.Held);
    }
}

/// <summary>A widget with two drive slots: one to choose.</summary>
internal sealed class TwoDrivesFake : WidgetConfig, IDriveChoice, IDiskScope
{
    public string Drive { get; set; } = string.Empty;

    public DiskScope Scope { get; set; }

    public override WidgetConfig Clone() => new TwoDrivesFake { Drive = Drive, Scope = Scope };
}
