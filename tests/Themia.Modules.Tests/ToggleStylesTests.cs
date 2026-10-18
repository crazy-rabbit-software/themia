using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The quick-setting toggles' and the power widgets' styles ([S-3916]..[S-3918], [S-1919]): one
/// shared style per family, the round button's ladder and the switch's ladder.
/// </summary>
public sealed class ToggleStylesTests
{
    private static IEnumerable<WidgetId> ToggleKinds => QuickToggleRules.All.Select(QuickToggleRules.KindOf);

    private static IEnumerable<WidgetId> PowerKinds => Enum.GetValues<PowerAction>().Select(PowerActionRules.KindOf);

    // ---- one style per family ------------------------------------------------------------------

    [Fact]
    public void S3916_all_thirteen_toggles_share_one_list_of_styles_tile_first()
    {
        var first = WidgetStyles.For(new WidgetId("wifiToggle"));
        Assert.Equal(["tile", "round", "switch"], first.Select(s => s.Name));

        foreach (var kind in ToggleKinds)
        {
            Assert.Same(first, WidgetStyles.For(kind));
            var config = Assert.IsAssignableFrom<QuickToggleConfig>(WidgetConfig.CreateDefault(kind));
            Assert.Equal(QuickToggleStyle.Tile, config.Style);
        }

        Assert.Equal(13, ToggleKinds.Count());
    }

    [Fact]
    public void S1919_all_seven_power_widgets_share_tile_and_round_named_as_the_toggles_are()
    {
        var power = WidgetStyles.For(new WidgetId("shutdown"));
        var toggles = WidgetStyles.For(new WidgetId("wifiToggle"));
        Assert.Equal(["tile", "round"], power.Select(s => s.Name));

        // The same look is the same name, label and icon in both families.
        Assert.Equal(toggles.Take(2), power);

        foreach (var kind in PowerKinds)
        {
            Assert.Same(power, WidgetStyles.For(kind));
            var config = Assert.IsAssignableFrom<PowerButtonConfig>(WidgetConfig.CreateDefault(kind));
            Assert.Equal(PowerButtonStyle.Tile, config.Style);
        }
    }

    [Fact]
    public void S3916_a_stored_toggle_style_reads_back_and_a_power_widget_refuses_switch()
    {
        var toggle = (WifiToggleConfig)ConfigJson.Read("WifiToggle", """{"kind":"wifiToggle","style":"switch"}""");
        Assert.Equal(QuickToggleStyle.Switch, toggle.Style);
        Assert.Equal(QuickToggleStyle.Switch, ((QuickToggleConfig)toggle.Clone()).Style);

        // "switch" is a toggle's look, not a power widget's: load validation sets the first.
        var shutdown = (ShutdownConfig)ConfigJson.Read("Shutdown", """{"kind":"shutdown","style":"switch"}""");
        Assert.Equal(PowerButtonStyle.Tile, shutdown.Style);
        Assert.False(WidgetStyles.Set(shutdown, (int)QuickToggleStyle.Switch));
    }

    // ---- the round button ----------------------------------------------------------------------

    private static RoundButtonPlan Round(double w, double h, int name = 13, int sentence = 0)
        => RoundButtonFace.Plan(new WidgetArea(w, h), w - 24, h - 24, name, sentence);

    [Fact]
    public void S3917_xs_is_the_circle_alone_and_from_s_the_name_sits_under_it()
    {
        var xs = Round(120, 120);
        Assert.Null(xs.Label);
        Assert.Equal(0, xs.LabelSlot);

        var s = Round(200, 150);
        Assert.NotNull(s.Label);
        Assert.True(s.Diameter + RoundButtonFace.LabelGap + s.LabelSlot <= 150 - 24);
    }

    [Fact]
    public void S3917_the_circle_is_even_fits_its_box_and_grows_with_the_tier()
    {
        var last = 0.0;
        foreach (var tier in WidgetTiers.All.OrderBy(t => Math.Min(t.Width, t.Height)))
        {
            var plan = Round(tier.Width, tier.Height);
            var room = tier.Height - 24 - (plan.Label is null ? 0 : plan.LabelSlot + RoundButtonFace.LabelGap);

            Assert.Equal(0, plan.Diameter % 2);
            Assert.Equal(0, plan.IconSize % 2);
            Assert.True(plan.Diameter <= Math.Min(tier.Width - 24, room), $"{tier.Name}: {plan.Diameter}");
            Assert.InRange(plan.Diameter, RoundButtonFace.MinDiameter, RoundButtonFace.MaxDiameter);
            Assert.True(plan.IconSize < plan.Diameter);
            Assert.True(plan.Diameter >= last, $"{tier.Name} shrank");
            last = plan.Diameter;
        }

        // The room L and XL have goes to the circle: XL's is bigger, not capped at L's.
        var l = WidgetTiers.All.Single(t => t.Name == "L");
        var xl = WidgetTiers.All.Single(t => t.Name == "XL");
        Assert.True(Round(xl.Width, xl.Height).Diameter > Round(l.Width, l.Height).Diameter);
    }

    [Fact]
    public void S1919_a_power_sentence_is_planned_into_the_label_slot_so_arming_moves_nothing()
    {
        var plain = Round(200, 150, name: 9);
        var arming = Round(200, 150, name: 9, sentence: "Click again to shut down".Length);

        Assert.True(arming.LabelSlot >= arming.Label!.Value.LineHeight);
        Assert.True(arming.LabelSlot >= arming.Sentence.LineHeight * arming.SentenceLines);
        Assert.True(arming.Diameter <= plain.Diameter);
    }

    [Fact]
    public void S3917_a_degenerate_box_still_plans_a_circle()
    {
        var plan = RoundButtonFace.Plan(new WidgetArea(double.NaN, 0), double.NaN, -5, 5);
        Assert.Null(plan.Label);
        Assert.Equal(RoundButtonFace.MinDiameter, plan.Diameter);
    }

    // ---- the switch ----------------------------------------------------------------------------

    /// <summary>The plan for a tile of w×h, inside the shared inset, as the view asks for it.</summary>
    private static SwitchFacePlan Switch(double w, double h, int name = 10, int state = 11)
    {
        var (iw, ih) = Inner(w, h);
        return SwitchFace.Plan(iw, ih, name, state);
    }

    private static (double W, double H) Inner(double w, double h)
    {
        var inset = ButtonInset.For(new WidgetArea(w, h));
        return (w - (2 * inset), h - (2 * inset));
    }

    /// <summary>The boxes drawn (empty ones are absent parts).</summary>
    private static List<PixelRect> Parts(SwitchFacePlan plan)
        => new[] { plan.Icon, plan.NameBox, plan.StateBox, plan.Track }.Where(r => r.Width > 0).ToList();

    public static TheoryData<double, double> Sizes => new()
    {
        { 120, 120 }, { 200, 150 }, { 320, 240 }, { 480, 160 }, { 200, 400 }, { 560, 420 }, { 800, 560 },
        { 100, 100 }, { 140, 140 }, { 160, 100 }, { 240, 120 }, { 320, 160 }, { 200, 300 },
    };

    [Fact]
    public void S3916_every_style_insets_by_the_cards_standard_inset_tighter_only_on_a_short_side_under_128()
    {
        Assert.Equal(16, ButtonInset.For(new WidgetArea(140, 140)));
        Assert.Equal(16, ButtonInset.For(new WidgetArea(160, 300)));
        Assert.Equal(16, ButtonInset.Standard);
        Assert.Equal(16, ButtonInset.For(new WidgetArea(200, 150)));
        Assert.Equal(16, ButtonInset.For(new WidgetArea(800, 560)));
        Assert.Equal(8, ButtonInset.For(new WidgetArea(120, 120)));
        Assert.Equal(8, ButtonInset.For(new WidgetArea(240, 120)));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void S3918_every_part_fits_inside_the_inset_box_and_no_planned_line_is_cut(double w, double h)
    {
        var (iw, ih) = Inner(w, h);
        var plan = Switch(w, h);

        foreach (var part in Parts(plan))
        {
            Assert.True(part.X >= 0 && part.Y >= 0 && part.X + part.Width <= iw + 0.01 && part.Y + part.Height <= ih + 0.01, $"{w}x{h}: {part}");
        }

        if (plan.Name is { } name)
        {
            Assert.True(FluentType.EstimateWidth(name, 10) <= plan.NameBox.Width, $"{w}x{h}: name");
        }

        if (plan.State is { } state)
        {
            Assert.True(FluentType.EstimateWidth(state, 11) <= plan.StateBox.Width, $"{w}x{h}: state");
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void S3918_a_column_is_centred_as_a_group_in_both_axes_and_a_row_on_its_centre_line(double w, double h)
    {
        var (iw, ih) = Inner(w, h);
        var plan = Switch(w, h);
        var parts = Parts(plan);

        if (plan.Layout == SwitchLayout.Column)
        {
            // Every part on the vertical centre line (the text lines span the width, centred in it).
            foreach (var part in parts)
            {
                Assert.InRange(part.X + (part.Width / 2), (iw / 2) - 0.5, (iw / 2) + 0.5);
            }

            var top = parts.Min(p => p.Y);
            var bottom = parts.Max(p => p.Y + p.Height);
            Assert.InRange(top - (ih - bottom), -1, 1);
            return;
        }

        // A Settings row: the icon at the left edge, the switch at the right, all on one centre line.
        Assert.Equal(iw, plan.Track.X + plan.Track.Width, 3);
        Assert.InRange(plan.Track.Y + (plan.Track.Height / 2), (ih / 2) - 0.5, (ih / 2) + 0.5);
        var words = plan.NameBox.Height + plan.StateBox.Height;
        Assert.InRange(plan.NameBox.Y + (words / 2), (ih / 2) - 0.5, (ih / 2) + 0.5);

        if (plan.IconSize > 0)
        {
            Assert.Equal(0, plan.Icon.X);
            Assert.InRange(plan.Icon.Y + (plan.IconSize / 2), (ih / 2) - 0.5, (ih / 2) + 0.5);
        }
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(120, 120)]
    [InlineData(140, 140)]
    [InlineData(160, 100)]
    public void S3918_a_small_tile_keeps_its_name_and_icon_while_they_fit(double w, double h)
    {
        // The old ladder dropped the name at any Compact width, though "Microphone" fits at 140.
        var plan = Switch(w, h);
        Assert.NotNull(plan.Name);
        Assert.True(plan.IconSize > 0);
        Assert.Equal(SwitchLayout.Column, plan.Layout);
    }

    [Fact]
    public void S3918_the_level_never_drops_as_the_tile_grows_and_s_is_above_the_smallest_steps()
    {
        // Swept where the inset holds (it widens once, at the 128 short side, [S-3916]).
        foreach (var h in new[] { 100.0, 140, 200, 300, 420 })
        {
            var last = -1;
            for (var w = 140.0; w <= 800; w += 10)
            {
                var level = Switch(w, h).Level;
                Assert.True(level >= last, $"{w}x{h} fell to {level} from {last}");
                last = level;
            }
        }

        // S (200×150) is above the Caption/Body step the old ladder put it on, with a switch above Windows' size.
        var s = Switch(200, 150);
        Assert.True(s.Level >= 2, $"S at {s.Level}");
        Assert.True(s.TrackHeight > SwitchFace.BaseTrackHeight);
        Assert.True(Switch(320, 240).Level > s.Level);
    }

    [Theory]
    [InlineData(480, 160)]
    [InlineData(320, 160)]
    [InlineData(240, 120)]
    public void S3918_a_wide_strip_is_a_settings_row(double w, double h)
    {
        var plan = Switch(w, h);
        Assert.Equal(SwitchLayout.Row, plan.Layout);
        Assert.NotNull(plan.Name);
        Assert.True(plan.NameBox.X + plan.NameBox.Width + (2 * plan.Gap) <= plan.Track.X + 0.01);
    }

    [Theory]
    [InlineData(200, 400)]
    [InlineData(200, 300)]
    [InlineData(320, 240)]
    [InlineData(120, 120)]
    public void S3918_a_square_or_tall_tile_is_a_column(double w, double h)
        => Assert.Equal(SwitchLayout.Column, Switch(w, h).Layout);

    [Fact]
    public void S3918_the_track_is_windows_two_to_one_never_below_40_by_20()
    {
        foreach (var (w, h) in new[] { (100.0, 100.0), (120, 120), (200, 150), (320, 240), (480, 160), (560, 420), (800, 560) })
        {
            var plan = Switch(w, h);
            Assert.Equal(plan.TrackWidth, plan.TrackHeight * 2);
            Assert.InRange(plan.TrackHeight, SwitchFace.BaseTrackHeight, SwitchFace.MaxTrackHeight);
            Assert.True(plan.Knob < plan.KnobHover && plan.KnobHover < plan.TrackHeight);
        }

        // Windows' own proportions at Body: a 40×20 track, a 12 knob growing to 14.
        Assert.Equal(20, SwitchFace.Level(1).TrackHeight);
        var windows = new SwitchFacePlan(SwitchLayout.Column, 1, 8, null, null, default, default, default, new PixelRect(0, 0, 40, 20));
        Assert.Equal(12, windows.Knob);
        Assert.Equal(14, windows.KnobHover);
    }

    [Fact]
    public void S3918_the_plan_is_made_for_the_longest_state_so_switching_moves_nothing()
    {
        var locale = Themia.Core.Localization.Locale.En;
        var longest = QuickToggleRules.LongestStateChars(locale);

        // Planned for "Unavailable", so "On" and "Off" sit in the same room and nothing shifts.
        Assert.True(longest >= QuickToggleRules.StateText(locale, ToggleLook.On).Length);
        Assert.True(longest >= QuickToggleRules.StateText(locale, ToggleLook.Off).Length);

        var plan = SwitchFace.Plan(288, 300, 5, longest);
        var state = Assert.NotNull(plan.State);
        Assert.True(FluentType.EstimateWidth(state, longest) <= plan.StateBox.Width);

        // A line too long for the room is dropped, never squeezed.
        Assert.Null(SwitchFace.Plan(288, 300, 5, 200).State);
    }

    [Fact]
    public void S3918_a_degenerate_box_still_plans_a_switch()
    {
        var plan = SwitchFace.Plan(double.NaN, -5, 5, 11);
        Assert.Null(plan.Name);
        Assert.Equal(SwitchFace.BaseTrackWidth, plan.TrackWidth);
    }
}
