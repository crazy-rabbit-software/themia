using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-6702], [S-6710]..[S-6717] The seven blocks' rules: which step, which parts, how many lines, what folds.</summary>
public sealed class FamilyBlockRulesTests
{
    [Theory]
    [InlineData(96, 96, 28)]
    [InlineData(296, 216, 68)]
    [InlineData(536, 396, 136)]
    [InlineData(776, 536, 192)]
    public void S6702_a_one_readout_box_holds_the_largest_hero_step_that_fits_it(double width, double height, double expected)
    {
        var step = ReadoutRules.Step(width, height, new ReadoutText("10:42", " AM", LineUnder: true, OneReadout: true));

        Assert.Equal(expected, step.FontSize);
    }

    [Fact]
    public void S6702_a_readout_takes_the_largest_step_that_fits_and_never_less_than_28()
    {
        // A long reading in a narrow box: no Hero step for a data reading, 40 fits.
        var step = ReadoutRules.Step(200, 372, new ReadoutText("188.8", " KB/s"));
        Assert.Equal(40, step.FontSize);

        // An unmeasured box is the floor, never a NaN.
        Assert.Equal(28, ReadoutRules.Step(double.NaN, 0, new ReadoutText("1")).FontSize);
        Assert.Equal(28, ReadoutRules.Step(10, 10, new ReadoutText("123456")).FontSize);
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
        LinePart[] parts =
        [
            new(LinePartKind.Problem, 16), new(LinePartKind.Label, 50), new(LinePartKind.Button, 24),
            new(LinePartKind.Button, 24), new(LinePartKind.Count, 20),
        ];

        // Everything fits a wide header.
        Assert.Equal([true, true, true, true, true], LineRules.Shown(400, 80, parts));

        // Narrower: the label goes, then the second button, then the first, then the count.
        var needs = LineRules.Needs(80, parts, [true, false, true, true, true]);
        Assert.Equal([true, false, true, true, true], LineRules.Shown(needs, 80, parts));
        Assert.Equal([true, false, true, false, true], LineRules.Shown(needs - 1, 80, parts));
        Assert.Equal([true, false, false, false, true], LineRules.Shown(LineRules.Needs(80, parts, [true, false, false, false, true]), 80, parts));
        Assert.Equal([true, false, false, false, false], LineRules.Shown(60, 80, parts));

        // An unmeasured header shows all.
        Assert.All(LineRules.Shown(0, 80, parts), Assert.True);
    }

    [Fact]
    public void S6710_two_buttons_stand_side_by_side_and_the_last_ones_glyph_sits_on_the_inset()
    {
        LinePart[] two = [new(LinePartKind.Button, 24), new(LinePartKind.Button, 24)];

        // The label, 8 before the trail, two buttons with no gap, less the last one's overhang.
        Assert.Equal(100 + 8 + 24 + 24 - 4, LineRules.Needs(100, two, [true, true]));
        Assert.Equal(0, LineRules.Gap(LinePartKind.Button, LinePartKind.Button));
        Assert.Equal(4, LineRules.Gap(LinePartKind.Count, LinePartKind.Button));
        Assert.Throws<ArgumentException>(() => LineRules.Shown(300, 50, [.. two, new(LinePartKind.Button, 24)]));
    }

    [Fact]
    public void S6710_a_part_with_nothing_to_say_takes_no_room_and_folds_nothing_away()
    {
        // Regression: the kit's line folded its Clear all button on a 200 tile because its hidden
        // problem glyph and ring still counted a gap each (Notifications at S).
        LinePart[] parts = [new(LinePartKind.Problem, 0), new(LinePartKind.Busy, 0), new(LinePartKind.Button, 24), new(LinePartKind.Count, 19)];
        bool[] wanted = [false, false, true, true];

        // 81.5 label + 8 + 24 + 4 + 19 = 136.5 fits 140; with the hidden two it would need 144.5.
        Assert.Equal([false, false, true, true], LineRules.Shown(140, 81.5, parts, wanted));
        Assert.Equal([false, false, false, true], LineRules.Shown(130, 81.5, parts, wanted));
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

    [Fact]
    public void S6712_only_whole_rows_fit()
    {
        Assert.Equal(6, RowRules.Fit(180, 1));
        Assert.Equal(7, RowRules.Fit(196, 1));
        Assert.Equal(4, RowRules.Fit(180, 2));
        Assert.Equal(3, RowRules.Fit(180, 3));
        Assert.Equal(0, RowRules.Fit(double.NaN, 1));
    }

    [Theory]
    [InlineData(120)]
    [InlineData(320)]
    [InlineData(560)]
    [InlineData(800)]
    public void S2220_a_grids_marks_start_4_inside_the_inset_and_end_within_the_card(double card)
    {
        // The marks stand 4 around the cells: from the card's edge they start at the inset less 4, and
        // the last one ends no further in from the other edge, so no cell is cut by the card.
        Assert.Equal(FamilySpace.Inset - FamilySpace.S1, RowRules.CellMarkEdge);
        var grid = RowRules.Cells(card - (2 * FamilySpace.Inset), 400, FamilyShape.IconLarge, named: true);
        var marks = (grid.Columns * grid.MarkWidth) + ((grid.Columns - 1) * FamilySpace.S1);
        Assert.True(RowRules.CellMarkEdge + marks <= card - RowRules.CellMarkEdge + 0.01, $"{card}: {marks}");
    }

    [Fact]
    public void S6713_a_cell_is_its_icon_over_a_caption()
    {
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

    [Fact]
    public void S11882_a_toggles_or_power_buttons_card_is_resized_freely_from_its_glyph_alone()
    {
        // Owner, 2026-11-15: "widgets like the toggle buttons should not have size limitations".
        foreach (var stateLine in new[] { true, false })
        {
            var tile = TileRules.Sizing(TileLook.Tile, stateLine);
            Assert.False(tile.FixedWidth || tile.FixedHeight);
            Assert.Empty(tile.Problems());
            Assert.Equal(tile, TileRules.Sizing(TileLook.Round, stateLine));

            // The least is the app's floor, one grid cell ([S-12806], [S-08]); the most is large on both axes, each free.
            Assert.Equal(new SizeDip(WidgetSizing.Floor, WidgetSizing.Floor), tile.Min);
            Assert.Equal(new SizeDip(TileRules.MaxSide, TileRules.MaxSide), tile.Max);
            Assert.True(TileRules.MaxSide >= 400);
        }

        // Where a new one lands, as the Medium tiles were: a toggle 116, a power button 100.
        Assert.Equal(new SizeDip(116, 116), TileRules.Sizing(TileLook.Tile).Natural);
        Assert.Equal(new SizeDip(100, 100), TileRules.Sizing(TileLook.Tile, stateLine: false).Natural);

        var row = TileRules.Sizing(TileLook.Switch);
        Assert.Empty(row.Problems());
        Assert.False(row.FixedWidth || row.FixedHeight);
        Assert.Equal(60, row.Min.Height);
        Assert.Equal(new SwitchParts(Glyph: true, Lines: false), TileRules.Switch(row.Min.Width - 24));
        Assert.Equal(new SwitchParts(Glyph: true, Lines: true), TileRules.Switch(row.Natural.Width - 24));
    }

    [Fact]
    public void S12806_under_the_glyphs_inset_the_inset_gives_way_and_the_glyph_shrinks_to_the_smallest()
    {
        // Owner, 2026-11-17: "they should not really have a smallest size at all".
        Assert.Equal(WidgetSizing.Floor, TileRules.MinSide);
        Assert.Equal(10, WidgetSizing.Floor);
        Assert.Equal((28.0, 6.0), (TileRules.Face(40, 40).MarkSize, TileRules.Face(40, 40).Inset));
        Assert.Equal((16.0, 4.0), (TileRules.Face(24, 24).MarkSize, TileRules.Face(24, 24).Inset));
        Assert.Equal((16.0, 6.0), (TileRules.Face(28, 60).MarkSize, TileRules.Face(28, 60).Inset));
        Assert.Equal((28.0, 4.0), (TileRules.Face(36, 36).MarkSize, TileRules.Face(36, 36).Inset));
        Assert.Equal((28.0, 10.0), (TileRules.Face(48, 48).MarkSize, TileRules.Face(48, 48).Inset));
        Assert.Equal((28.0, 12.0), (TileRules.Face(52, 52).MarkSize, TileRules.Face(52, 52).Inset));

        // Under the 16's room the glyph is two thirds of the box, the inset the rest.
        Assert.Equal((6.0, 2.0), (TileRules.Face(10, 10).MarkSize, TileRules.Face(10, 10).Inset));
        Assert.Equal((13.0, 3.0), (TileRules.Face(20, 20).MarkSize, TileRules.Face(20, 20).Inset));

        // Every box from the floor: the glyph alone fits its inset (4 from the 16's room on), and grows with the box.
        var last = 0.0;
        for (var side = TileRules.MinSide; side < 60; side++)
        {
            var face = TileRules.Face(side, side);
            Assert.False(face.Texted);
            Assert.True(face.Inset >= (side >= 24 ? TileRules.TightInset : 1) && face.MarkSize + (2 * face.Inset) <= side && face.MarkSize > 0, $"{side}: {face}");
            Assert.True(face.MarkSize >= last, $"{side}: shrank");
            last = face.MarkSize;
        }
    }

    [Fact]
    public void S11882_the_glyph_and_its_words_grow_with_the_box_and_always_fit_it()
    {
        // The smallest box is the glyph alone; the natural ones hold the glyph, two name lines and the state line.
        Assert.Equal(new TileFace(FamilyType.Big28, false, FamilyType.Body, FamilyType.Caption), TileRules.Face(52, 52));
        Assert.Equal(new TileFace(FamilyType.Big28, true, FamilyType.Body, FamilyType.Caption), TileRules.Face(116, 116));
        Assert.Equal(new TileFace(FamilyType.Big28, true, FamilyType.Body, FamilyType.Caption), TileRules.Face(100, 100, stateLine: false));

        // A bigger box takes a bigger step, and past 68 the words grow too.
        Assert.Equal(FamilyType.Big40, TileRules.Face(128, 128).Glyph);
        Assert.True(TileRules.Face(480, 480).Glyph.FontSize >= 96);
        Assert.Equal(FamilyType.Regular(FamilyType.Title), TileRules.Face(480, 480).Name);

        // Wide and short, or tall and narrow: the glyph alone, as large as the short side holds.
        Assert.False(TileRules.Face(400, 60).Texted);
        Assert.False(TileRules.Face(52, 400).Texted);
        Assert.Equal(FamilyType.Big28, TileRules.Face(52, 400).Glyph);

        // Never larger than the box: every box from the least to the most, on both axes.
        for (var w = 52.0; w <= TileRules.MaxSide; w += 12)
        {
            for (var h = 52.0; h <= TileRules.MaxSide; h += 20)
            {
                foreach (var stateLine in new[] { true, false })
                {
                    var face = TileRules.Face(w, h, stateLine);
                    var (iw, ih) = (w - 24, h - 24);
                    var below = face.Texted ? FamilySpace.S2 + (TileRules.NameLines * face.Name.LineHeight) + (stateLine ? face.State.LineHeight : 0) : 0;
                    Assert.True(face.Glyph.FontSize <= iw && face.Glyph.FontSize + below <= ih, $"{w} x {h}: {face}");
                    Assert.True(!face.Texted || TileRules.NameRoom(face.Name) <= iw, $"{w} x {h}: {face}");
                }
            }
        }

        // Growing never shrinks the glyph.
        var last = 0.0;
        for (var side = 52.0; side <= TileRules.MaxSide; side += 4)
        {
            var glyph = TileRules.Face(side, side).Glyph.FontSize;
            Assert.True(glyph >= last, $"{side}");
            last = glyph;
        }
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(0, 4)]
    [InlineData(9, 2)]
    public void S12807_a_rows_band_is_what_its_height_leaves_over_and_under_its_text(int lines, double band)
        => Assert.Equal(band, FamilyShape.RowBand(lines));

    [Theory]
    [InlineData(10, 28)]    // 12 + 28 = 40: four cells
    [InlineData(12, 24)]    // 12 + 24 = 36: three cells of 12
    [InlineData(9.6, 26.4)] // 12 + 26.4 = 38.4: four cells of 9.6
    public void S12834_a_tool_line_is_a_band_ending_on_a_grid_line_under_the_cards_inset(double cell, double band)
    {
        // WR-1 (owner, 2026-11-19): "there should not be a space at the top"; the rows under a line start right under it.
        var grid = new ItemGrid(cell);
        Assert.Equal(band, LineRules.Band(grid), 6);
        Assert.True(grid.IsWhole(FamilySpace.Inset + LineRules.Band(grid), 1e-6));
        Assert.True(LineRules.Band(grid) >= FamilySpace.HeaderLine + FamilySpace.HeaderGap);
        Assert.Equal(28, LineRules.NaturalBand, 6);
    }

    [Fact]
    public void S12834_a_line_on_the_cards_top_edge_and_a_field_over_rows_end_on_grid_lines()
    {
        var grid = ItemGrid.Default();

        // The compact state or Set up line on the card's top edge: a row of 28 in whole cells.
        Assert.Equal(30, LineRules.EdgeRow(grid), 6);

        // A widget's search field: the family's control of 28 from where it stands, grown to the next grid line.
        Assert.Equal(28, LineRules.Under(grid, FamilySpace.Inset, FamilyShape.Button), 6);
        Assert.Equal(30, LineRules.Under(grid, 40, FamilyShape.Button), 6);
        Assert.True(grid.IsWhole(40 + LineRules.Under(grid, 40, FamilyShape.Button), 1e-6));
    }
}
