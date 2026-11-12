using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6702], [S-6710]..[S-6717] The seven blocks' rules: which step, which parts, how many lines, what folds.</summary>
public sealed class FamilyBlockRulesTests
{
    /// <summary>A tier's view area under a header, and the box inside the inset a readout has.</summary>
    private static (WidgetArea Area, double Width, double Height) Under(string tier, bool header = true)
    {
        var t = WidgetTiers.All.Single(x => x.Name == tier);
        var top = header ? FamilySpace.ContentTop : FamilySpace.Inset;
        var area = new WidgetArea(t.Width, t.Height - (header ? FamilySpace.ContentTop : 0));
        return (area, t.Width - (2 * FamilySpace.Inset), t.Height - top - FamilySpace.Inset);
    }

    [Theory]
    [InlineData("XS", 28)]
    [InlineData("S", 28)]
    [InlineData("M", 40)]
    [InlineData("Wide", 40)]
    [InlineData("Tall", 40)]
    [InlineData("L", 68)]
    [InlineData("XL", 68)]
    public void S6702_a_data_widgets_number_climbs_28_40_68_and_stops(string tier, double expected)
    {
        var (area, width, height) = Under(tier);

        var step = ReadoutRules.Step(area, width, height, new ReadoutText("62", "%", LineUnder: true));

        Assert.Equal(expected, step.FontSize);
        Assert.True(step.SemiBold);
    }

    [Theory]
    [InlineData("XS", 28)]
    [InlineData("M", 68)]
    [InlineData("L", 136)]
    [InlineData("XL", 192)]
    public void S6702_a_one_readout_widget_climbs_the_hero_steps_as_far_as_they_fit(string tier, double expected)
    {
        var (area, width, height) = Under(tier, header: false);

        var step = ReadoutRules.Step(area, width, height, new ReadoutText("10:42", " AM", LineUnder: true, OneReadout: true));

        Assert.Equal(expected, step.FontSize);
    }

    [Fact]
    public void S6702_a_readout_that_does_not_fit_its_ceiling_takes_the_step_that_does()
    {
        var (area, _, height) = Under("L");

        // A long reading in a narrow column of an L tile: 68 is allowed but too wide, 40 fits.
        var step = ReadoutRules.Step(area, 200, height, new ReadoutText("188.8", " KB/s"));
        Assert.Equal(40, step.FontSize);

        // An unmeasured box is the floor, never a NaN.
        Assert.Equal(28, ReadoutRules.Step(area, double.NaN, 0, new ReadoutText("1")).FontSize);
        Assert.Equal(28, ReadoutRules.Step(area, 10, 10, new ReadoutText("123456")).FontSize);
    }

    [Fact]
    public void S6702_a_lead_glyph_and_the_unit_are_part_of_the_width()
    {
        var step = FamilyType.Big40;
        var plain = ReadoutRules.Width(step, new ReadoutText("14"));
        var withUnit = ReadoutRules.Width(step, new ReadoutText("14", "°C"));
        var withLead = ReadoutRules.Width(step, new ReadoutText("14", "°C", Lead: true));

        Assert.Equal(HeroFit.Ems("14") * 40, plain, 3);
        Assert.Equal(plain + (HeroFit.Ems("°C") * 20), withUnit, 3);
        Assert.Equal(withUnit + 40 + 8, withLead, 3);
        Assert.Equal(52 + 20, ReadoutRules.Height(step, new ReadoutText("14", LineUnder: true)));
    }

    [Fact]
    public void S6710_the_label_gives_way_first_then_buttons_from_the_last_then_the_count()
    {
        HeaderPart[] parts =
        [
            new(HeaderPartKind.Problem, 16), new(HeaderPartKind.Label, 50), new(HeaderPartKind.Button, 24),
            new(HeaderPartKind.Button, 24), new(HeaderPartKind.Count, 20),
        ];

        // Everything fits a wide header.
        Assert.Equal([true, true, true, true, true], HeaderRules.Shown(400, 80, parts));

        // Narrower: the label goes, then the second button, then the first, then the count.
        var needs = HeaderRules.Needs(80, parts, [true, false, true, true, true]);
        Assert.Equal([true, false, true, true, true], HeaderRules.Shown(needs, 80, parts));
        Assert.Equal([true, false, true, false, true], HeaderRules.Shown(needs - 1, 80, parts));
        Assert.Equal([true, false, false, false, true], HeaderRules.Shown(HeaderRules.Needs(80, parts, [true, false, false, false, true]), 80, parts));
        Assert.Equal([true, false, false, false, false], HeaderRules.Shown(60, 80, parts));

        // An unmeasured header shows all.
        Assert.All(HeaderRules.Shown(0, 80, parts), Assert.True);
    }

    [Fact]
    public void S6710_two_buttons_stand_side_by_side_and_the_last_ones_glyph_sits_on_the_inset()
    {
        HeaderPart[] two = [new(HeaderPartKind.Button, 24), new(HeaderPartKind.Button, 24)];

        // Leading 24, the name, 8 before the trail, two buttons with no gap, less the last one's overhang.
        Assert.Equal(24 + 100 + 8 + 24 + 24 - 4, HeaderRules.Needs(100, two, [true, true]));
        Assert.Equal(0, HeaderRules.Gap(HeaderPartKind.Button, HeaderPartKind.Button));
        Assert.Equal(4, HeaderRules.Gap(HeaderPartKind.Count, HeaderPartKind.Button));
        Assert.Throws<ArgumentException>(() => HeaderRules.Shown(300, 50, [.. two, new(HeaderPartKind.Button, 24)]));
    }

    [Fact]
    public void S6710_a_part_with_nothing_to_say_takes_no_room_and_folds_nothing_away()
    {
        // Regression: the kit's header folded its Clear all button on a 200 tile because its hidden
        // problem glyph and ring still counted a gap each (Notifications at S).
        HeaderPart[] parts = [new(HeaderPartKind.Problem, 0), new(HeaderPartKind.Busy, 0), new(HeaderPartKind.Button, 24), new(HeaderPartKind.Count, 19)];
        bool[] wanted = [false, false, true, true];

        // 24 leading + 81.5 name + 8 + 24 + 4 + 19 = 160.5 fits 164; with the hidden two it would need 168.5.
        Assert.Equal([false, false, true, true], HeaderRules.Shown(164, 81.5, parts, wanted));
        Assert.Equal([false, false, false, true], HeaderRules.Shown(150, 81.5, parts, wanted));
    }

    [Fact]
    public void S6711_what_is_read_is_primary()
    {
        var fact = RowRules.Roles(RowKind.Fact);
        var item = RowRules.Roles(RowKind.List);

        Assert.Equal((ColourRole.TextSecondary, ColourRole.TextPrimary), (fact.Name, fact.Value));
        Assert.Equal((ColourRole.TextPrimary, ColourRole.TextSecondary), (item.Name, item.Value));
        Assert.Equal((ColourRole.TextSecondary, ColourRole.TextTertiary), (item.Second, item.Third));
        Assert.All(new[] { RowRules.Roles(RowKind.Fact, available: false) }, r => Assert.Equal(ColourRole.TextDisabled, r.Value));
        Assert.Equal(24, RowRules.NameStart(16));
        Assert.Equal(40, RowRules.NameStart(32));
        Assert.Equal(0, RowRules.NameStart(0));
    }

    [Fact]
    public void S2270_a_read_row_of_a_list_that_tells_unread_by_its_names_has_a_secondary_name()
    {
        var read = RowRules.Roles(RowKind.List, quiet: true);
        var unread = RowRules.Roles(RowKind.List);

        Assert.Equal(ColourRole.TextSecondary, read.Name);
        Assert.Equal(ColourRole.TextPrimary, unread.Name);
        Assert.Equal((unread.Value, unread.Second, unread.Third), (read.Value, read.Second, read.Third));
        Assert.Equal(ColourRole.TextDisabled, RowRules.Roles(RowKind.List, available: false, quiet: true).Name);
    }

    [Theory]
    [InlineData("XS", 1)]
    [InlineData("S", 1)]
    [InlineData("Wide", 1)]
    [InlineData("M", 2)]
    [InlineData("Tall", 2)]
    [InlineData("L", 3)]
    [InlineData("XL", 3)]
    public void S6712_a_rows_lines_come_from_the_tier_and_the_pitch_from_the_lines(string tier, int lines)
    {
        var (area, _, _) = Under(tier);

        Assert.Equal(lines, RowRules.Lines(area, 3));
        Assert.Equal(1, RowRules.Lines(area, 1));
    }

    [Fact]
    public void S6712_only_whole_rows_fit()
    {
        Assert.Equal(5, RowRules.Fit(180, 1));
        Assert.Equal(6, RowRules.Fit(192, 1));
        Assert.Equal(3, RowRules.Fit(180, 2));
        Assert.Equal(0, RowRules.Fit(double.NaN, 1));
    }

    [Fact]
    public void S6713_a_cell_is_its_icon_over_a_caption()
    {
        Assert.Equal(32, RowRules.CellIcon(new WidgetArea(320, 196)));
        Assert.Equal(48, RowRules.CellIcon(new WidgetArea(560, 376)));
        Assert.Equal(32 + 4 + 16, RowRules.CellHeight(32, named: true));
        Assert.Equal(48, RowRules.CellHeight(48, named: false));
    }

    [Fact]
    public void S6714_a_status_keeps_its_glyph_and_action_and_sheds_the_sentence()
    {
        // S's content is 168 wide: "Set up" (about 55) leaves too little for a sentence; M's 288 has room.
        Assert.False(StatusRules.ShowsSentence(168, 55));
        Assert.True(StatusRules.ShowsSentence(288, 55));

        // With no action the sentence is all the row says: it stays, cut, at XS too.
        Assert.True(StatusRules.ShowsSentence(88, 0));
        Assert.True(StatusRules.ShowsSentence(0, 70));
        Assert.Equal(288 - 24 - 82, StatusRules.Room(288, 70));

        Assert.Equal(new StatusLook(ColourRole.TextTertiary, MainAction: true), StatusRules.Look(StatusKind.Setup));
        Assert.Equal(new StatusLook(ColourRole.Caution, MainAction: false), StatusRules.Look(StatusKind.Problem));
        Assert.Equal(new StatusLook(ColourRole.TextTertiary, MainAction: false), StatusRules.Look(StatusKind.Empty));
    }

    [Theory]
    [InlineData(88, 88, 40)]
    [InlineData(168, 118, 68)]
    [InlineData(288, 208, 136)]
    [InlineData(768, 528, 192)]
    [InlineData(40, 30, 28)]
    public void S6715_a_tiles_glyph_climbs_the_steps_above_its_two_lines(double width, double height, double expected)
        => Assert.Equal(expected, TileRules.GlyphStep(width, height).FontSize);

    [Fact]
    public void S6715_on_is_the_whole_tile_in_the_accent_and_missing_is_disabled()
    {
        Assert.Equal(new TileRoles(ColourRole.Accent, null, ColourRole.TextOnAccent, ColourRole.TextOnAccent, ColourRole.TextOnAccentSecondary), TileRules.Roles(TileLook.Tile, TileState.On));
        Assert.Equal(new TileRoles(null, null, ColourRole.TextPrimary, ColourRole.TextPrimary, ColourRole.TextSecondary), TileRules.Roles(TileLook.Tile, TileState.Off));
        Assert.Equal(ColourRole.Accent, TileRules.Roles(TileLook.Round, TileState.On).Circle);
        Assert.Null(TileRules.Roles(TileLook.Round, TileState.On).Fill);
        Assert.Null(TileRules.Roles(TileLook.Round, TileState.Off).Circle);
        var missing = TileRules.Roles(TileLook.Tile, TileState.Unavailable);
        Assert.Null(missing.Fill);
        Assert.Equal(ColourRole.TextDisabled, missing.Glyph);

        Assert.Equal(32, TileRules.RoundGlyph(68));
        Assert.Equal(48, TileRules.RoundGlyph(96));
        Assert.Equal(16, TileRules.RoundGlyph(28));
    }

    [Theory]
    [InlineData(TileLook.Tile)]
    [InlineData(TileLook.Round)]
    [InlineData(TileLook.Switch)]
    public void S6736_unknown_is_secondary_critical_is_critical_and_neither_fills(TileLook look)
    {
        Assert.Equal(new TileRoles(null, null, ColourRole.TextSecondary, ColourRole.TextSecondary, ColourRole.TextSecondary), TileRules.Roles(look, TileState.Unknown));
        Assert.Equal(new TileRoles(null, null, ColourRole.Critical, ColourRole.Critical, ColourRole.Critical), TileRules.Roles(look, TileState.Critical));
        Assert.Equal(new TileRoles(null, null, ColourRole.TextDisabled, ColourRole.TextDisabled, ColourRole.TextDisabled), TileRules.Roles(look, TileState.Unavailable));

        // The accent is only for on, never for text on the card.
        foreach (var state in Enum.GetValues<TileState>())
        {
            var roles = TileRules.Roles(look, state);
            Assert.NotEqual(ColourRole.Accent, roles.Label);
            Assert.NotEqual(ColourRole.Accent, roles.State);
            Assert.NotEqual(ColourRole.Accent, roles.Glyph);
            Assert.Equal(state == TileState.On && look != TileLook.Switch, roles.Fill is not null || roles.Circle is not null);
        }
    }

    [Theory]
    [InlineData(28, 16)]
    [InlineData(40, 28)]
    [InlineData(68, 48)]
    [InlineData(96, 68)]
    [InlineData(136, 96)]
    [InlineData(192, 136)]
    public void S6736_a_ringed_glyph_is_the_largest_family_size_within_three_quarters_of_its_ring(double ring, double glyph)
        => Assert.Equal(glyph, TileRules.RingGlyph(ring));

    [Theory]
    [InlineData(88, true, false)]
    [InlineData(168, false, true)]
    [InlineData(176, true, true)]
    [InlineData(288, true, true)]
    [InlineData(double.NaN, true, false)]
    public void S6736_the_switch_row_drops_its_glyph_first_then_its_lines(double width, bool glyph, bool lines)
        => Assert.Equal(new SwitchParts(glyph, lines), TileRules.Switch(width));

    [Fact]
    public void S6716_one_main_action_and_icons_give_way_first()
    {
        ControlPart[] parts = [new(ControlKind.Icon, 32), new(ControlKind.Main, 72), new(ControlKind.Icon, 32), new(ControlKind.Slider, 80)];

        Assert.All(ControlsRules.Shown(400, parts), Assert.True);
        Assert.Equal(32 + 72 + 32 + 80 + (3 * 8), ControlsRules.Needs(parts, [true, true, true, true]));
        Assert.Equal([true, true, false, true], ControlsRules.Shown(200, parts));
        Assert.Equal([false, true, false, false], ControlsRules.Shown(80, parts));
        Assert.Throws<ArgumentException>(() => ControlsRules.Check([new(ControlKind.Main, 50), new(ControlKind.Main, 50)]));
    }

    [Fact]
    public void S6717_a_chart_is_the_accent_until_its_threshold()
    {
        Assert.Equal(ColourRole.Accent, ChartRules.Role(50, 85, 95));
        Assert.Equal(ColourRole.Caution, ChartRules.Role(86, 85, 95));
        Assert.Equal(ColourRole.Critical, ChartRules.Role(95, 85, 95));
        Assert.Equal(ColourRole.Accent, ChartRules.Role(99));

        var line = ChartRules.Line([0, 1, double.NaN], 100, 50);
        Assert.Equal([new ChartPoint(0, 49), new ChartPoint(50, 1), new ChartPoint(100, 49)], line);
        Assert.Empty(ChartRules.Line([0.5], 100, 50));

        Assert.Equal((25, 50), ChartRules.Bar(100, 0.25, 0.75));
        Assert.Equal((0, 100), ChartRules.Bar(100, 4));
        Assert.Equal(90, ChartRules.Sweep(0.25));
        Assert.Equal(0, ChartRules.Sweep(double.NaN));
    }

    [Fact]
    public void S6717_a_range_bar_fills_between_its_two_ends_from_the_tracks_start_or_its_foot()
    {
        // A day's range from 8° to 16° on a week of 5° to 17°: along a row's track, or up a column's.
        var (start, length) = ChartRules.Bar(120, (16 - 5) / 12.0, (8 - 5) / 12.0);
        Assert.Equal(30, start, 6);
        Assert.Equal(80, length, 6);
        Assert.Equal(ChartRules.Bar(120, 0.25, 0.75), ChartRules.Bar(120, 0.75, 0.25));
    }

    [Fact]
    public void S6717_a_value_stands_inside_its_band_and_a_gauge_is_the_rings_upper_half()
    {
        // A 0 half a stroke over the foot, a 1 half a stroke under the top, a bad value at 0.
        Assert.Equal(49, ChartRules.Y(0, 50));
        Assert.Equal(1, ChartRules.Y(1, 50));
        Assert.Equal(49, ChartRules.Y(double.NaN, 50));
        Assert.Equal(1, ChartRules.Y(3, 50));

        // The gauge sweeps half a turn at full, from nine o'clock; its box is half the ring and half a stroke.
        Assert.Equal(90, ChartRules.GaugeSweep(0.5));
        Assert.Equal(180, ChartRules.GaugeSweep(2));
        Assert.Equal(0, ChartRules.GaugeSweep(double.NaN));
        Assert.Equal(51, ChartRules.GaugeHeight(100));
    }

    [Fact]
    public void S6707_a_delta_stands_beside_the_value_and_its_unit_where_they_fit()
    {
        var text = new ReadoutText("420.27", " USD");
        Assert.Equal(new ReadoutFold(Unit: true, DeltaBeside: true), ReadoutRules.Fold(FamilyType.Big28, text, "+88.88%", 288));
    }

    [Fact]
    public void S6707_the_unit_gives_way_before_the_delta_leaves_the_line()
    {
        var text = new ReadoutText("420.27", " USD");
        var bare = ReadoutRules.Width(FamilyType.Big28, text with { Unit = "" }) + ReadoutRules.DeltaWidth("+88.88%");
        Assert.Equal(new ReadoutFold(Unit: false, DeltaBeside: true), ReadoutRules.Fold(FamilyType.Big28, text, "+88.88%", bare));
    }

    [Fact]
    public void S6707_a_delta_with_no_room_beside_takes_the_line_under_and_the_unit_comes_back()
    {
        var text = new ReadoutText("420.27", " USD");
        var withUnit = ReadoutRules.Width(FamilyType.Big28, text);
        Assert.Equal(new ReadoutFold(Unit: true, DeltaBeside: false), ReadoutRules.Fold(FamilyType.Big28, text, "+88.88%", withUnit));
        Assert.Equal(new ReadoutFold(Unit: false, DeltaBeside: false), ReadoutRules.Fold(FamilyType.Big28, text, "+88.88%", withUnit - 1));
    }

    [Fact]
    public void S6707_no_delta_always_stands_beside()
        => Assert.True(ReadoutRules.Fold(FamilyType.Big40, new ReadoutText("1.0675", " EUR"), "", 200).DeltaBeside);
}
