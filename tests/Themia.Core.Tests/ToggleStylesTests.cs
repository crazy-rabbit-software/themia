using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The quick-setting toggles' and the power widgets' styles ([S-3916]..[S-3918], [S-1919]): one
/// shared style per family, the round button's ladder and the switch's ladder.
/// </summary>
public sealed class ToggleStylesTests
{
    private static IEnumerable<WidgetKind> ToggleKinds => QuickToggleRules.All.Select(QuickToggleRules.KindOf);

    private static IEnumerable<WidgetKind> PowerKinds => Enum.GetValues<PowerAction>().Select(PowerActionRules.KindOf);

    // ---- one style per family ------------------------------------------------------------------

    [Fact]
    public void S3916_all_thirteen_toggles_share_one_list_of_styles_tile_first()
    {
        var first = WidgetStyles.For(WidgetKind.WifiToggle);
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
        var power = WidgetStyles.For(WidgetKind.Shutdown);
        var toggles = WidgetStyles.For(WidgetKind.WifiToggle);
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

    private static SwitchFacePlan Switch(double w, double h, int name = 13)
        => SwitchFace.Plan(new WidgetArea(w, h), w - 24, h - 24, name, 11);

    [Fact]
    public void S3918_xs_is_the_icon_over_the_switch_without_a_name()
    {
        var plan = Switch(120, 120);
        Assert.Equal(SwitchLayout.IconOver, plan.Layout);
        Assert.Null(plan.Name);
        Assert.Null(plan.State);
        Assert.True(plan.IconSize >= 16);
        Assert.True(plan.IconSize + SwitchFace.Gap + plan.TrackHeight <= 120 - 24);
    }

    [Theory]
    [InlineData(200, 150)]
    [InlineData(320, 240)]
    [InlineData(200, 400)]
    [InlineData(560, 420)]
    [InlineData(800, 560)]
    public void S3918_a_tile_or_column_stacks_name_and_switch_and_fits_its_height(double w, double h)
    {
        var plan = Switch(w, h);
        Assert.Equal(SwitchLayout.Stacked, plan.Layout);
        var name = Assert.NotNull(plan.Name);

        var used = (plan.IconSize > 0 ? plan.IconSize + SwitchFace.Gap : 0) + name.LineHeight + SwitchFace.Gap + plan.TrackHeight;
        Assert.True(used <= h - 24, $"{w}x{h}: {used}");
        Assert.True(plan.TrackWidth <= (w - 24) / 2);
    }

    [Fact]
    public void S3918_a_wide_strip_is_a_settings_row_that_fits_its_width()
    {
        var plan = Switch(480, 160);
        Assert.Equal(SwitchLayout.Row, plan.Layout);
        var name = Assert.NotNull(plan.Name);

        var used = (plan.IconSize > 0 ? plan.IconSize + SwitchFace.Gap : 0) + FluentType.EstimateWidth(name, 13)
            + (2 * SwitchFace.Gap) + (plan.State is { } s ? FluentType.EstimateWidth(s, 11) + SwitchFace.Gap : 0) + plan.TrackWidth;
        Assert.True(used <= 480 - 24, $"{used}");
        Assert.True(plan.TrackHeight <= 160 - 24);
    }

    [Fact]
    public void S3918_the_track_is_windows_two_to_one_in_half_steps_and_grows_with_the_name()
    {
        var last = 0.0;
        foreach (var tier in WidgetTiers.All.Where(t => t.Name is "XS" or "S" or "M" or "L" or "XL"))
        {
            var plan = Switch(tier.Width, tier.Height);
            var scale = plan.TrackWidth / SwitchFace.BaseTrackWidth;

            Assert.Equal(plan.TrackWidth, plan.TrackHeight * 2);
            Assert.InRange(scale, 1, SwitchFace.MaxScale);
            Assert.Equal(0, scale * 2 % 1);
            Assert.True(plan.Knob < plan.KnobHover && plan.KnobHover < plan.TrackHeight);
            Assert.True(plan.TrackWidth >= last, $"{tier.Name} shrank");
            last = plan.TrackWidth;
        }
    }

    [Fact]
    public void S3918_the_plan_is_made_for_the_longest_state_so_switching_moves_nothing()
    {
        var area = new WidgetArea(320, 240);
        var locale = Themia.Core.Localization.Locale.En;
        var longest = QuickToggleRules.LongestStateChars(locale);

        // Planned for "Unavailable", so "On" and "Off" sit in the same room and nothing shifts.
        Assert.True(longest >= QuickToggleRules.StateText(locale, ToggleLook.On).Length);
        Assert.True(longest >= QuickToggleRules.StateText(locale, ToggleLook.Off).Length);

        var plan = SwitchFace.Plan(area, 296, 216, 5, longest);
        var state = Assert.NotNull(plan.State);
        Assert.True(plan.TrackWidth + SwitchFace.Gap + FluentType.EstimateWidth(state, longest) <= 296);

        // A line too long for the room is dropped, never squeezed.
        Assert.Null(SwitchFace.Plan(area, 296, 216, 5, 200).State);
    }
}
