using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The item background, a layer apart from the card's ([S-6510]..[S-6515], [S-6521]).</summary>
public sealed class ItemLayerTests
{
    private static WidgetInstance Widget(string id, ItemLayer? items = null)
    {
        var widget = WidgetInstance.Create(WidgetKind.Shortcuts, new FracRect(0, 0, 0.1, 0.1));
        widget.Id = id;
        widget.ItemBackground = items;
        return widget;
    }

    [Fact]
    public void S6510_a_fresh_design_keeps_the_items_on_their_fluent_fills()
    {
        var design = new DesignState();

        Assert.Equal(string.Empty, design.ItemBackground.Color);
        Assert.Equal(1, design.ItemBackground.Opacity);
        Assert.Null(WidgetInstance.Create(WidgetKind.Email, new FracRect(0, 0, 0.1, 0.1)).ItemBackground);
    }

    [Theory]
    [InlineData(ItemRole.Row, ItemState.Rest, "SubtleFillColorTransparentBrush")]
    [InlineData(ItemRole.Row, ItemState.Hover, "SubtleFillColorSecondaryBrush")]
    [InlineData(ItemRole.Row, ItemState.Pressed, "SubtleFillColorTertiaryBrush")]
    [InlineData(ItemRole.Row, ItemState.Selected, "SubtleFillColorSecondaryBrush")]
    [InlineData(ItemRole.Row, ItemState.SelectedHover, "SubtleFillColorTertiaryBrush")]
    [InlineData(ItemRole.Tile, ItemState.Rest, "ControlFillColorDefaultBrush")]
    [InlineData(ItemRole.Tile, ItemState.Hover, "ControlFillColorSecondaryBrush")]
    [InlineData(ItemRole.Tile, ItemState.Pressed, "ControlFillColorTertiaryBrush")]
    [InlineData(ItemRole.Tile, ItemState.Disabled, "ControlFillColorDisabledBrush")]
    [InlineData(ItemRole.Chip, ItemState.Rest, "SubtleFillColorSecondaryBrush")]
    [InlineData(ItemRole.Chip, ItemState.Selected, "SubtleFillColorTertiaryBrush")]
    [InlineData(ItemRole.Track, ItemState.Rest, "ControlStrongFillColorDefaultBrush")]
    [InlineData(ItemRole.Track, ItemState.Disabled, "ControlStrongFillColorDisabledBrush")]
    public void S6514_the_default_layer_is_the_fluent_fill_each_item_used_before(ItemRole role, ItemState state, string key)
    {
        var ink = ItemLook.Ink(role, state, new ItemLayer(), dark: true);

        Assert.Null(ink.Colour);
        Assert.Equal(key, ink.ThemeKey);
        Assert.Equal(1, ink.Opacity);
    }

    [Fact]
    public void S6513_without_a_colour_the_opacity_fades_the_fluent_fill()
    {
        var ink = ItemLook.Ink(ItemRole.Tile, ItemState.Hover, new ItemLayer { Opacity = 0.4 }, dark: false);

        Assert.Equal("ControlFillColorSecondaryBrush", ink.ThemeKey);
        Assert.Equal(0.4, ink.Opacity, 3);
    }

    [Fact]
    public void S6513_with_a_colour_every_role_rests_on_it_at_the_layer_opacity()
    {
        var layer = new ItemLayer { Color = "#336699", Opacity = 0.7 };

        foreach (var role in new[] { ItemRole.Row, ItemRole.Tile, ItemRole.Chip })
        {
            var ink = ItemLook.Ink(role, ItemState.Rest, layer, dark: true);
            Assert.Equal(new RgbColor(0x33, 0x66, 0x99), ink.Colour);
            Assert.Null(ink.ThemeKey);
            Assert.Equal(0.7, ink.Opacity, 3);
        }
    }

    [Fact]
    public void S6513_states_derive_from_the_item_colour_toward_the_themes_contrast()
    {
        var layer = new ItemLayer { Color = "#336699" };
        var rest = new RgbColor(0x33, 0x66, 0x99);

        RgbColor Dark(ItemState state) => ItemLook.Ink(ItemRole.Row, state, layer, dark: true).Colour!.Value;
        RgbColor Light(ItemState state) => ItemLook.Ink(ItemRole.Row, state, layer, dark: false).Colour!.Value;

        Assert.Equal(DesignLook.Over(DesignLook.Alpha(ItemLook.HoverShift), new RgbColor(255, 255, 255), rest), Dark(ItemState.Hover));
        Assert.Equal(DesignLook.Over(DesignLook.Alpha(ItemLook.HoverShift), new RgbColor(0, 0, 0), rest), Light(ItemState.Hover));

        // Lighter in dark theme, darker in light, and further the stronger the state.
        Assert.True(Dark(ItemState.Hover).R > rest.R && Dark(ItemState.Selected).R > Dark(ItemState.Hover).R);
        Assert.True(Dark(ItemState.SelectedHover).R > Dark(ItemState.Selected).R);
        Assert.True(Light(ItemState.Hover).B < rest.B && Light(ItemState.Pressed).B > Light(ItemState.Hover).B);
    }

    [Fact]
    public void S6513_a_track_stands_off_the_item_colour_and_disabled_is_half_as_opaque()
    {
        var layer = new ItemLayer { Color = "#336699", Opacity = 0.8 };

        Assert.NotEqual(new RgbColor(0x33, 0x66, 0x99), ItemLook.Ink(ItemRole.Track, ItemState.Rest, layer, true).Colour);

        var disabled = ItemLook.Ink(ItemRole.Tile, ItemState.Disabled, layer, true);
        Assert.Equal(new RgbColor(0x33, 0x66, 0x99), disabled.Colour);
        Assert.Equal(0.4, disabled.Opacity, 3);
    }

    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(-1, 0)]
    [InlineData(3, 1)]
    [InlineData(0.25, 0.25)]
    public void S6511_an_opacity_is_clamped_and_not_a_number_is_opaque(double value, double expected)
        => Assert.Equal(expected, ItemLook.Opacity(value), 3);

    [Fact]
    public void S6512_a_widget_with_its_own_layer_overrides_the_app_wide_one_whole()
    {
        var design = new DesignState { ItemBackground = new ItemLayer { Color = "#112233", Opacity = 0.5 } };
        var own = new ItemLayer { Color = "#445566", Opacity = 0.9 };

        Assert.Same(design.ItemBackground, ItemLook.Effective(design, Widget("a")));
        Assert.Same(own, ItemLook.Effective(design, Widget("b", own)));
        Assert.Same(design.ItemBackground, ItemLook.Effective(design, null));
    }

    [Fact]
    public void S6511_load_validation_repairs_a_bad_layer_loudly_and_keeps_a_good_one()
    {
        var state = AppState.CreateDefault();
        state.Design.ItemBackground = new ItemLayer { Color = "not a colour", Opacity = 7 };
        var widget = state.Views[0].Widgets[0];
        widget.ItemBackground = new ItemLayer { Color = "#abcdef", Opacity = double.NaN };

        var repairs = AppStateStore.Validate(state);

        Assert.Equal(string.Empty, state.Design.ItemBackground.Color);
        Assert.Equal(1, state.Design.ItemBackground.Opacity);
        Assert.Equal("#ABCDEF", widget.ItemBackground!.Color);
        Assert.Equal(1, widget.ItemBackground.Opacity);
        Assert.Contains(repairs, r => r.Contains("item background colour", StringComparison.Ordinal));
        Assert.Contains(repairs, r => r.Contains(widget.Id, StringComparison.Ordinal) && r.Contains("opacity", StringComparison.Ordinal));
    }

    [Fact]
    public void S6511_a_missing_app_wide_layer_loads_as_the_default()
    {
        var state = AppState.CreateDefault();
        state.Design.ItemBackground = null!;

        AppStateStore.Validate(state);

        Assert.True(new ItemLayer().SameAs(state.Design.ItemBackground));
    }

    [Fact]
    public void S6512_the_layer_survives_a_save_and_a_load()
    {
        var state = AppState.CreateDefault();
        state.Design.ItemBackground = new ItemLayer { Color = "#203040", Opacity = 0.6 };
        state.Views[0].Widgets[0].ItemBackground = new ItemLayer { Color = "#405060", Opacity = 0.3 };

        var json = System.Text.Json.JsonSerializer.Serialize(state, AppStateJsonContext.Default.AppState);
        var back = System.Text.Json.JsonSerializer.Deserialize(json, AppStateJsonContext.Default.AppState)!;

        Assert.True(state.Design.ItemBackground.SameAs(back.Design.ItemBackground));
        Assert.True(state.Views[0].Widgets[0].ItemBackground!.SameAs(back.Views[0].Widgets[0].ItemBackground));
        Assert.Null(back.Views[0].Widgets[1].ItemBackground);
    }

    [Fact]
    public void S6512_a_duplicated_widget_keeps_its_own_layer_as_a_copy()
    {
        var widget = Widget("a", new ItemLayer { Color = "#010203", Opacity = 0.5 });

        var copy = widget.Clone();

        Assert.NotSame(widget.ItemBackground, copy.ItemBackground);
        Assert.True(widget.ItemBackground!.SameAs(copy.ItemBackground));
    }

    [Fact]
    public void S6515_a_preset_owns_the_item_layer_and_the_default_preset_is_the_fluent_fills()
    {
        var design = new DesignState();

        DesignPresets.Apply(design, DesignPresetId.Glass);
        Assert.Equal("#2A3248", design.ItemBackground.Color);
        Assert.Equal(DesignPresetId.Glass, DesignPresets.Match(design));

        design.ItemBackground.Opacity = 0.2;
        Assert.Null(DesignPresets.Match(design));

        DesignPresets.Apply(design, DesignPresetId.Default);
        Assert.True(new ItemLayer().SameAs(design.ItemBackground));
        Assert.Equal(DesignPresetId.Default, DesignPresets.Match(design));
    }

    private static (ViewState View, WidgetInstance A, WidgetInstance B) Pair(ItemLayer? a, ItemLayer? b)
    {
        var view = ViewState.Create("v");
        var first = Widget("a", a);
        var second = Widget("b", b);
        view.Widgets.AddRange([first, second]);
        return (view, first, second);
    }

    [Fact]
    public void S6521_the_offer_reads_a_shared_layer_none_or_mixed()
    {
        var own = new ItemLayer { Color = "#112233", Opacity = 0.5 };

        Assert.Equal((null, false), QuickSet.CommonItems([Widget("a"), Widget("b")]));

        var (same, mixedSame) = QuickSet.CommonItems([Widget("a", own), Widget("b", own.Clone())]);
        Assert.False(mixedSame);
        Assert.True(own.SameAs(same));

        Assert.True(QuickSet.CommonItems([Widget("a", own), Widget("b")]).Mixed);
        Assert.True(QuickSet.CommonItems([Widget("a"), Widget("b", own)]).Mixed);
        Assert.True(QuickSet.CommonItems([Widget("a", own), Widget("b", new ItemLayer { Color = "#112233", Opacity = 0.6 })]).Mixed);
    }

    [Fact]
    public void S6521_the_offer_for_a_selection_carries_its_item_layer()
    {
        var (view, a, _) = Pair(new ItemLayer { Color = "#112233" }, null);
        var state = new AppState { Views = [view] };
        var selection = new WidgetSelection();

        selection.Replace(view.Id, ["a"]);
        Assert.Equal("#112233", QuickSet.For(state, selection, view.Id).Items?.Color);

        selection.Replace(view.Id, ["a", "b"]);
        Assert.True(QuickSet.For(state, selection, view.Id).ItemsMixed);
        Assert.NotNull(a.ItemBackground);
    }

    [Fact]
    public void S6521_a_colour_gives_each_widget_its_own_layer_keeping_its_opacity()
    {
        var appWide = new ItemLayer { Opacity = 0.6 };
        var (view, a, b) = Pair(new ItemLayer { Color = "#000000", Opacity = 0.3 }, null);

        Assert.Equal(2, QuickSet.SetItemColor(view, ["a", "b"], "#ff8800", appWide));
        Assert.Equal("#FF8800", a.ItemBackground!.Color);
        Assert.Equal(0.3, a.ItemBackground.Opacity, 3);
        Assert.Equal("#FF8800", b.ItemBackground!.Color);
        Assert.Equal(0.6, b.ItemBackground.Opacity, 3);

        Assert.Equal(0, QuickSet.SetItemColor(view, ["a", "b"], "#FF8800", appWide));
    }

    [Fact]
    public void S6521_default_takes_the_widgets_own_layer_away()
    {
        var (view, a, b) = Pair(new ItemLayer { Color = "#000000" }, null);

        Assert.Equal(1, QuickSet.SetItemColor(view, ["a", "b"], string.Empty, new ItemLayer()));
        Assert.Null(a.ItemBackground);
        Assert.Null(b.ItemBackground);
    }

    [Fact]
    public void S6521_the_opacity_reaches_only_widgets_with_a_layer_of_their_own()
    {
        var (view, a, b) = Pair(new ItemLayer { Color = "#000000", Opacity = 1 }, null);

        Assert.Equal(1, QuickSet.SetItemOpacity(view, ["a", "b"], 0.25));
        Assert.Equal(0.25, a.ItemBackground!.Opacity, 3);
        Assert.Null(b.ItemBackground);

        Assert.Equal(0, QuickSet.SetItemOpacity(view, ["a"], 0.25));
        Assert.Equal(1, QuickSet.SetItemOpacity(view, ["a"], 9));
        Assert.Equal(1, a.ItemBackground.Opacity, 3);
    }
}
