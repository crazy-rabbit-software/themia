using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The quick-setting toggles' and the power widgets' styles ([S-3916]..[S-3918], [S-1919]): one
/// shared style per family, each a look of the family's Tile block ([S-6715]).
/// </summary>
public sealed class ToggleStylesTests
{
    private static IEnumerable<WidgetId> PowerKinds => Enum.GetValues<PowerAction>().Select(PowerActionRules.KindOf);

    // ---- one style per family ------------------------------------------------------------------

    [Fact]
    public void S3916_all_thirteen_targets_share_one_list_of_styles_tile_first()
    {
        var first = WidgetStyles.For(new WidgetId("quickToggle"));
        Assert.Equal(["tile", "round", "switch"], first.Select(s => s.Name));

        foreach (var target in QuickToggleRules.All)
        {
            var config = new QuickToggleConfig { Target = target };
            Assert.Same(first, ((IStyled)config).Styles);
            Assert.Equal(QuickToggleStyle.Tile, config.Style);
        }
    }

    [Fact]
    public void S1919_all_seven_power_widgets_share_tile_and_round_named_as_the_toggles_are()
    {
        var power = WidgetStyles.For(new WidgetId("shutdown"));
        var toggles = WidgetStyles.For(new WidgetId("quickToggle"));
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
        var toggle = (QuickToggleConfig)ConfigJson.Read("QuickToggle", """{"kind":"quickToggle","target":"bluetooth","style":"switch"}""");
        Assert.Equal(QuickToggleStyle.Switch, toggle.Style);
        Assert.Equal(QuickToggleStyle.Switch, ((QuickToggleConfig)toggle.Clone()).Style);

        // "switch" is a toggle's look, not a power widget's: load validation sets the first.
        var shutdown = (ShutdownConfig)ConfigJson.Read("Shutdown", """{"kind":"shutdown","style":"switch"}""");
        Assert.Equal(PowerButtonStyle.Tile, shutdown.Style);
        Assert.False(WidgetStyles.Set(shutdown, (int)QuickToggleStyle.Switch));
    }

    // ---- the looks -----------------------------------------------------------------------------

    [Fact]
    public void S3916_every_style_is_a_look_of_the_tile_block_planned_in_the_cards_inset()
    {
        // One inset for every style and box ([S-6703]); the glyph's step is the box's ([S-11882]), so the
        // round circle grows with it and its glyph stays half the circle.
        foreach (var side in new[] { 52.0, 116, 200, 480 })
        {
            var face = Themia.Sdk.Kit.TileRules.Face(side, side);
            var h = side - (2 * Themia.Sdk.Kit.FamilySpace.Inset);
            var step = face.Glyph.FontSize;
            var below = face.Texted ? Themia.Sdk.Kit.FamilySpace.S2 + (2 * face.Name.LineHeight) + face.State.LineHeight : 0;
            Assert.True(step + below <= h, side.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.True(Themia.Sdk.Kit.TileRules.RoundGlyph(step) <= Math.Max(Themia.Sdk.Kit.FamilyShape.Icon, step / 2), side.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
