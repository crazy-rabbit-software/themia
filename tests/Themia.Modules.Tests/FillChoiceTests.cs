using System.Text.Json;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// One fill choice for the background and the item background ([S-6560]..[S-6568]), and the item
/// layer it fills ([S-6510]..[S-6515], [S-6521]).
/// </summary>
public sealed class FillChoiceTests
{
    private static WidgetInstance Widget(string id, FillChoice? items = null, WidgetId? kind = null)
    {
        var widget = WidgetInstance.Create(kind ?? new WidgetId("shortcuts"), new FracRect(0, 0, 0.1, 0.1));
        widget.Id = id;
        widget.ItemBackground = items ?? new FillChoice();
        return widget;
    }

    // ---- [S-6560] the choice ------------------------------------------------------------------------

    [Fact]
    public void S6560_a_fresh_design_and_widget_are_default_for_both_fills()
    {
        var design = new DesignState();
        var widget = WidgetInstance.Create(new WidgetId("email"), new FracRect(0, 0, 0.1, 0.1));

        Assert.Equal(FillKind.Default, design.Background.Kind);
        Assert.Equal(FillKind.Default, design.ItemBackground.Kind);
        Assert.Equal(FillKind.Default, widget.Background.Kind);
        Assert.Equal(FillKind.Default, widget.ItemBackground.Kind);
    }

    [Fact]
    public void S6560_it_is_stored_as_kind_colour_and_opacity()
    {
        var state = AppState.CreateDefault();
        state.Design.Background = FillChoice.Of("#1b2030", 0.55);

        var json = JsonSerializer.Serialize(state, StateJson.AppState);

        Assert.Contains("\"background\": {", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"Colour\"", json, StringComparison.Ordinal);
        Assert.Contains("\"colour\": \"#1B2030\"", json, StringComparison.Ordinal);
        Assert.Contains("\"opacity\": 0.55", json, StringComparison.Ordinal);
    }

    [Fact]
    public void S6560_both_fills_survive_a_save_and_a_load_app_wide_and_per_widget()
    {
        var state = AppState.CreateDefault();
        state.Design.Background = FillChoice.Transparent();
        state.Design.ItemBackground = FillChoice.Of("#203040", 0.6);
        state.Views[0].Widgets[0].Background = FillChoice.Of("#405060", 0.3);
        state.Views[0].Widgets[0].ItemBackground = FillChoice.Transparent();

        var json = JsonSerializer.Serialize(state, StateJson.AppState);
        var back = JsonSerializer.Deserialize(json, StateJson.AppState)!;

        Assert.True(state.Design.Background.SameAs(back.Design.Background));
        Assert.True(state.Design.ItemBackground.SameAs(back.Design.ItemBackground));
        Assert.True(state.Views[0].Widgets[0].Background.SameAs(back.Views[0].Widgets[0].Background));
        Assert.True(state.Views[0].Widgets[0].ItemBackground.SameAs(back.Views[0].Widgets[0].ItemBackground));
        Assert.Equal(FillKind.Default, back.Views[1].Widgets[1].Background.Kind); // the seeded calendar, never touched
    }

    [Fact]
    public void S6560_kinds_are_read_case_insensitively()
    {
        var design = JsonSerializer.Deserialize("""{ "background": { "kind": "colour", "colour": "#102030", "opacity": 0.5 } }""", StateJson.DesignState)!;
        Assert.Equal(FillKind.Colour, design.Background.Kind);
    }

    [Fact]
    public void S6560_same_as_compares_what_is_drawn()
    {
        Assert.True(FillChoice.Of("#AABBCC", 0.5).SameAs(new FillChoice { Kind = FillKind.Colour, Colour = "#aabbcc", Opacity = 0.5004 }));
        Assert.False(FillChoice.Of("#AABBCC", 0.5).SameAs(FillChoice.Of("#AABBCC", 0.6)));
        Assert.False(FillChoice.Of("#AABBCC", 1).SameAs(FillChoice.Transparent()));

        // What a Default or Transparent choice remembers of an earlier colour does not count.
        Assert.True(new FillChoice { Kind = FillKind.Transparent, Colour = "#123456", Opacity = 0.2 }.SameAs(FillChoice.Transparent()));
        Assert.False(FillChoice.Default().SameAs(null));
    }

    // ---- [S-6561] load validation ---------------------------------------------------------------------

    [Fact]
    public void S6561_a_bad_choice_is_repaired_to_the_default_loudly()
    {
        var state = AppState.CreateDefault();
        state.Design.Background = new FillChoice { Kind = FillKind.Colour, Colour = "not a colour", Opacity = 0.5 };
        state.Design.ItemBackground = new FillChoice { Kind = (FillKind)9 };
        var widget = state.Views[0].Widgets[0];
        widget.Background = new FillChoice { Kind = FillKind.Colour, Colour = "#abcdef", Opacity = double.NaN };
        widget.ItemBackground = new FillChoice { Kind = FillKind.Colour, Colour = "#abcdef", Opacity = 7 };

        var repairs = AppStateStore.Validate(state);

        Assert.Equal(FillKind.Default, state.Design.Background.Kind);
        Assert.Equal(FillKind.Default, state.Design.ItemBackground.Kind);
        Assert.Equal("#ABCDEF", widget.Background.Colour);
        Assert.Equal(1, widget.Background.Opacity);
        Assert.Equal(1, widget.ItemBackground.Opacity);
        Assert.Contains(repairs, r => r.Contains("widget background colour", StringComparison.Ordinal));
        Assert.Contains(repairs, r => r.Contains("item background had an unknown kind", StringComparison.Ordinal));
        Assert.Contains(repairs, r => r.Contains(widget.Id, StringComparison.Ordinal) && r.Contains("opacity", StringComparison.Ordinal));
    }

    [Fact]
    public void S6561_a_good_choice_is_kept_and_a_missing_one_is_the_default_silently()
    {
        var state = AppState.CreateDefault();
        state.Design.Background = FillChoice.Of("#102030", 0.4);
        state.Design.ItemBackground = null!;
        state.Views[0].Widgets[0].Background = null!;

        var repairs = AppStateStore.Validate(state);

        Assert.True(FillChoice.Of("#102030", 0.4).SameAs(state.Design.Background));
        Assert.Equal(FillKind.Default, state.Design.ItemBackground.Kind);
        Assert.Equal(FillKind.Default, state.Views[0].Widgets[0].Background.Kind);
        Assert.DoesNotContain(repairs, r => r.Contains("background", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(-1, 0)]
    [InlineData(3, 1)]
    [InlineData(0.25, 0.25)]
    public void S6561_an_opacity_is_clamped_and_not_a_number_is_opaque(double value, double expected)
        => Assert.Equal(expected, FillChoice.ClampOpacity(value), 3);

    // ---- [S-6562] resolution ----------------------------------------------------------------------------

    [Fact]
    public void S6562_a_widget_default_follows_the_app_wide_choice_anything_else_wins()
    {
        var appWide = FillChoice.Of("#112233", 0.5);

        Assert.Same(appWide, FillChoice.Effective(appWide, FillChoice.Default()));
        Assert.Same(appWide, FillChoice.Effective(appWide, null));

        var own = FillChoice.Transparent();
        Assert.Same(own, FillChoice.Effective(appWide, own));
    }

    [Fact]
    public void S6562_the_item_background_resolves_the_same_way()
    {
        var design = new DesignState { ItemBackground = FillChoice.Of("#112233", 0.5) };
        var own = FillChoice.Of("#445566", 0.9);

        Assert.Same(design.ItemBackground, ItemLook.Effective(design, Widget("a")));
        Assert.Same(own, ItemLook.Effective(design, Widget("b", own)));
        Assert.Same(design.ItemBackground, ItemLook.Effective(design, null));
    }

    [Fact]
    public void S6563_the_card_paints_its_effective_choice()
    {
        var design = new DesignState { Background = FillChoice.Of("#202020", 0.5) };
        var follows = Widget("a");
        var own = Widget("b");
        own.Background = FillChoice.Transparent();

        var shared = DesignLook.Card(FillChoice.Effective(design.Background, follows.Background));
        Assert.Equal(new CardFill(FillKind.Colour, new RgbColor(0x20, 0x20, 0x20), 0.5), shared);

        var dropped = DesignLook.Card(FillChoice.Effective(design.Background, own.Background));
        Assert.False(dropped.Painted);
        Assert.False(DesignLook.Border(dropped, WidgetBorderStyle.Solid, 1, 1).Visible);
    }

    // ---- [S-6564] item inks ---------------------------------------------------------------------------

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
    public void S6514_default_is_the_fluent_fill_each_item_used_before(ItemRole role, ItemState state, string key)
    {
        var ink = ItemLook.Ink(role, state, FillChoice.Default(), dark: true);

        Assert.Null(ink.Colour);
        Assert.Equal(key, ink.ThemeKey);
        Assert.Equal(1, ink.Opacity);
    }

    [Fact]
    public void S6564_default_has_no_opacity_of_its_own()
    {
        // A remembered colour and opacity behind Default change nothing.
        var ink = ItemLook.Ink(ItemRole.Tile, ItemState.Hover, new FillChoice { Colour = "#336699", Opacity = 0.4 }, dark: false);

        Assert.Null(ink.Colour);
        Assert.Equal("ControlFillColorSecondaryBrush", ink.ThemeKey);
        Assert.Equal(1, ink.Opacity);
    }

    [Theory]
    [InlineData(ItemRole.Row, ItemState.Rest)]
    [InlineData(ItemRole.Tile, ItemState.Rest)]
    [InlineData(ItemRole.Chip, ItemState.Rest)]
    [InlineData(ItemRole.Tile, ItemState.Disabled)]
    [InlineData(ItemRole.Track, ItemState.Rest)]
    [InlineData(ItemRole.Track, ItemState.Hover)]
    public void S6564_transparent_draws_no_item_at_rest_disabled_or_as_a_track(ItemRole role, ItemState state)
        => Assert.Equal(0, ItemLook.Ink(role, state, FillChoice.Transparent(), dark: true).Opacity);

    [Theory]
    [InlineData(ItemRole.Tile, ItemState.Hover, "SubtleFillColorSecondaryBrush")]
    [InlineData(ItemRole.Chip, ItemState.Pressed, "SubtleFillColorTertiaryBrush")]
    [InlineData(ItemRole.Row, ItemState.Selected, "SubtleFillColorSecondaryBrush")]
    public void S6564_a_transparent_item_still_answers_the_pointer_and_shows_selection(ItemRole role, ItemState state, string key)
    {
        var ink = ItemLook.Ink(role, state, FillChoice.Transparent(), dark: false);

        Assert.Null(ink.Colour);
        Assert.Equal(key, ink.ThemeKey);
        Assert.Equal(1, ink.Opacity);
    }

    [Fact]
    public void S6513_with_a_colour_every_role_rests_on_it_at_its_opacity()
    {
        var layer = FillChoice.Of("#336699", 0.7);

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
        var layer = FillChoice.Of("#336699", 1);
        var rest = new RgbColor(0x33, 0x66, 0x99);

        RgbColor Dark(ItemState state) => ItemLook.Ink(ItemRole.Row, state, layer, dark: true).Colour!.Value;
        RgbColor Light(ItemState state) => ItemLook.Ink(ItemRole.Row, state, layer, dark: false).Colour!.Value;

        Assert.Equal(DesignLook.Over(FillChoice.Alpha(ItemLook.HoverShift), new RgbColor(255, 255, 255), rest), Dark(ItemState.Hover));
        Assert.Equal(DesignLook.Over(FillChoice.Alpha(ItemLook.HoverShift), new RgbColor(0, 0, 0), rest), Light(ItemState.Hover));

        // Lighter in dark theme, darker in light, and further the stronger the state.
        Assert.True(Dark(ItemState.Hover).R > rest.R && Dark(ItemState.Selected).R > Dark(ItemState.Hover).R);
        Assert.True(Dark(ItemState.SelectedHover).R > Dark(ItemState.Selected).R);
        Assert.True(Light(ItemState.Hover).B < rest.B && Light(ItemState.Pressed).B > Light(ItemState.Hover).B);
    }

    [Fact]
    public void S6513_a_track_stands_off_the_item_colour_and_disabled_is_half_as_opaque()
    {
        var layer = FillChoice.Of("#336699", 0.8);

        Assert.NotEqual(new RgbColor(0x33, 0x66, 0x99), ItemLook.Ink(ItemRole.Track, ItemState.Rest, layer, true).Colour);

        var disabled = ItemLook.Ink(ItemRole.Tile, ItemState.Disabled, layer, true);
        Assert.Equal(new RgbColor(0x33, 0x66, 0x99), disabled.Colour);
        Assert.Equal(0.4, disabled.Opacity, 3);
    }

    // ---- [S-6565] presets -------------------------------------------------------------------------------

    [Fact]
    public void S6565_presets_map_their_fills_onto_the_choice()
    {
        Assert.Equal(FillKind.Default, DesignPresets.Get(DesignPresetId.Default).Values.Background.Kind);

        // Glass was the translucent look: now a colour with an opacity, card and items alike.
        var glass = DesignPresets.Get(DesignPresetId.Glass).Values;
        Assert.True(FillChoice.Of("#1B2030", 0.55).SameAs(glass.Background));
        Assert.True(FillChoice.Of("#2A3248", 0.6).SameAs(glass.Items));

        Assert.True(FillChoice.Of("#0B0B0F", 0.96).SameAs(DesignPresets.Get(DesignPresetId.Midnight).Values.Background));
        Assert.True(FillChoice.Of("#FAF7F0", 1).SameAs(DesignPresets.Get(DesignPresetId.Paper).Values.Background));
    }

    [Fact]
    public void S6565_applying_a_preset_writes_copies_never_the_template()
    {
        var design = new DesignState();
        DesignPresets.Apply(design, DesignPresetId.Glass);

        design.Background.Opacity = 0.1;
        design.ItemBackground.Kind = FillKind.Transparent;

        Assert.Equal(0.55, DesignPresets.Get(DesignPresetId.Glass).Values.Background.Opacity, 3);
        Assert.Equal(FillKind.Colour, DesignPresets.Get(DesignPresetId.Glass).Values.Items!.Kind);
        Assert.Null(DesignPresets.Match(design));
    }

    [Fact]
    public void S6515_a_preset_owns_both_fills_and_the_default_preset_is_the_fluent_fills()
    {
        var design = new DesignState();

        DesignPresets.Apply(design, DesignPresetId.Glass);
        Assert.Equal(DesignPresetId.Glass, DesignPresets.Match(design));

        design.ItemBackground = FillChoice.Of("#2A3248", 0.2);
        Assert.Null(DesignPresets.Match(design));

        DesignPresets.Apply(design, DesignPresetId.Default);
        Assert.Equal(FillKind.Default, design.Background.Kind);
        Assert.Equal(FillKind.Default, design.ItemBackground.Kind);
        Assert.Equal(DesignPresetId.Default, DesignPresets.Match(design));
    }

    // ---- [S-6566] the properties panel ----------------------------------------------------------------

    [Fact]
    public void S6566_a_duplicated_widget_keeps_copies_of_its_fills()
    {
        var widget = Widget("a", FillChoice.Of("#010203", 0.5));
        widget.Background = FillChoice.Transparent();

        var copy = widget.Clone();

        Assert.NotSame(widget.ItemBackground, copy.ItemBackground);
        Assert.NotSame(widget.Background, copy.Background);
        Assert.True(widget.ItemBackground.SameAs(copy.ItemBackground));
        Assert.True(widget.Background.SameAs(copy.Background));
    }

    [Fact]
    public void S6566_the_offer_reads_a_shared_fill_or_null_when_mixed()
    {
        var own = FillChoice.Of("#112233", 0.5);

        Assert.Equal(FillKind.Default, QuickSet.CommonFill([FillChoice.Default(), FillChoice.Default()])!.Kind);
        Assert.True(own.SameAs(QuickSet.CommonFill([own, own.Clone()])));
        Assert.Null(QuickSet.CommonFill([own, FillChoice.Default()]));
        Assert.Null(QuickSet.CommonFill([own, FillChoice.Of("#112233", 0.6)]));
        Assert.Null(QuickSet.CommonFill([]));
    }

    [Fact]
    public void S6566_the_offer_carries_both_fills()
    {
        var view = ViewState.Create("v");
        var a = Widget("a", FillChoice.Of("#112233", 1));
        var b = Widget("b");
        a.Background = b.Background = FillChoice.Transparent();
        view.Widgets.AddRange([a, b]);
        var state = new AppState { Views = [view] };
        var selection = new WidgetSelection();

        selection.Replace(view.Id, ["a"]);
        var one = QuickSet.For(state, selection, view.Id);
        Assert.Equal("#112233", one.Items?.Colour);
        Assert.Equal(FillKind.Transparent, one.Background?.Kind);

        selection.Replace(view.Id, ["a", "b"]);
        var two = QuickSet.For(state, selection, view.Id);
        Assert.Null(two.Items);
        Assert.Equal(FillKind.Transparent, two.Background?.Kind);
    }

    [Fact]
    public void S6566_setting_a_fill_writes_a_copy_to_every_selected_widget_once()
    {
        var view = ViewState.Create("v");
        var a = Widget("a");
        var b = Widget("b");
        view.Widgets.AddRange([a, b]);
        var fill = FillChoice.Of("#FF8800", 0.5);

        Assert.Equal(2, QuickSet.SetBackground(view, ["a", "b", "elsewhere"], fill));
        Assert.True(fill.SameAs(a.Background));
        Assert.NotSame(fill, a.Background);
        Assert.NotSame(a.Background, b.Background);
        Assert.Equal(0, QuickSet.SetBackground(view, ["a", "b"], FillChoice.Of("#ff8800", 0.5)));

        Assert.Equal(2, QuickSet.SetItemBackground(view, ["a", "b"], FillChoice.Transparent()));
        Assert.Equal(FillKind.Transparent, b.ItemBackground.Kind);

        // Default hands both back to the app-wide fill.
        Assert.Equal(2, QuickSet.SetBackground(view, ["a", "b"], FillChoice.Default()));
        Assert.Equal(FillKind.Default, a.Background.Kind);
    }
}
