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

    // ---- the looks -----------------------------------------------------------------------------

    [Fact]
    public void S3916_every_style_is_a_look_of_the_tile_block_planned_in_the_cards_inset()
    {
        // One inset for every style and size ([S-6703]): the tile less 16 on each side; the glyph's
        // step is the block's, so the round circle and the switch's row grow with it.
        foreach (var tier in WidgetTiers.All)
        {
            var (w, h) = (tier.Width - 32, tier.Height - 32);
            var step = Themia.Sdk.Kit.TileRules.GlyphStep(w, h).FontSize;
            Assert.True(step + Themia.Sdk.Kit.TileRules.Below(label: true, state: true) <= h, tier.Name);
            Assert.True(Themia.Sdk.Kit.TileRules.RoundGlyph(step) <= step / 2, tier.Name);
        }
    }
}
