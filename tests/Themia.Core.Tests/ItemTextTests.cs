using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The item text colour, part of the item layer ([S-6540]..[S-6545]).</summary>
public sealed class ItemTextTests
{
    private static WidgetInstance Widget(string id, string text = "", FillChoice? items = null)
    {
        var widget = WidgetInstance.Create(ShortcutsConfig.Descriptor.Id, new FracRect(0, 0, 0.1, 0.1));
        widget.Id = id;
        widget.ItemTextColor = text;
        widget.ItemBackground = items ?? new FillChoice();
        return widget;
    }

    [Fact]
    public void S6540_a_fresh_design_and_widget_keep_the_theme_text()
    {
        Assert.Equal(string.Empty, new DesignState().ItemTextColor);
        Assert.Equal(string.Empty, WidgetInstance.Create(EmailConfig.Descriptor.Id, new FracRect(0, 0, 0.1, 0.1)).ItemTextColor);
        Assert.Equal(string.Empty, ItemLook.TextColour(new DesignState(), null));
    }

    [Fact]
    public void S6541_the_widgets_own_text_colour_wins_else_the_app_wide_one()
    {
        var design = new DesignState { ItemTextColor = "#ffffff" };

        Assert.Equal("#FFFFFF", ItemLook.TextColour(design, null));
        Assert.Equal("#FFFFFF", ItemLook.TextColour(design, Widget("a")));
        Assert.Equal("#102030", ItemLook.TextColour(design, Widget("b", "#102030")));
        Assert.Equal("#102030", ItemLook.TextColour(new DesignState(), Widget("c", "#102030")));
    }

    [Fact]
    public void S6541_the_text_colour_resolves_apart_from_the_item_background()
    {
        // A widget with a background of its own still takes the app-wide text colour, and one with a
        // text colour of its own still takes the app-wide background.
        var design = new DesignState { ItemTextColor = "#FFFFFF", ItemBackground = FillChoice.Of("#202020", 1) };
        var ownBackground = Widget("a", items: FillChoice.Of("#F0F0F0", 1));
        var ownText = Widget("b", "#000000");

        Assert.Equal("#FFFFFF", ItemLook.TextColour(design, ownBackground));
        Assert.Equal("#000000", ItemLook.TextColour(design, ownText));
        Assert.Same(design.ItemBackground, ItemLook.Effective(design, ownText));
    }

    [Fact]
    public void S6541_a_stored_text_colour_that_is_not_one_follows_on()
    {
        Assert.Equal("#FFFFFF", ItemLook.TextColour(new DesignState { ItemTextColor = "#FFFFFF" }, Widget("a", "nonsense")));
        Assert.Equal(string.Empty, ItemLook.TextColour(new DesignState { ItemTextColor = "nonsense" }, null));
    }

    [Theory]
    [InlineData(ItemTextTier.Primary, "TextFillColorPrimaryBrush")]
    [InlineData(ItemTextTier.Secondary, "TextFillColorSecondaryBrush")]
    [InlineData(ItemTextTier.Tertiary, "TextFillColorTertiaryBrush")]
    [InlineData(ItemTextTier.Disabled, "TextFillColorDisabledBrush")]
    public void S6542_without_a_colour_each_tier_is_the_theme_text(ItemTextTier tier, string key)
    {
        foreach (var dark in new[] { true, false })
        {
            var ink = ItemLook.TextInk(tier, string.Empty, dark);

            Assert.Null(ink.Colour);
            Assert.Equal(key, ink.ThemeKey);
            Assert.Equal(1, ink.Opacity);
        }
    }

    [Theory]
    [InlineData(true, ItemTextTier.Primary, 1.0)]
    [InlineData(true, ItemTextTier.Secondary, 0xC5 / 255.0)]
    [InlineData(true, ItemTextTier.Tertiary, 0x87 / 255.0)]
    [InlineData(true, ItemTextTier.Disabled, 0x5D / 255.0)]
    [InlineData(false, ItemTextTier.Primary, 1.0)]
    [InlineData(false, ItemTextTier.Secondary, 0x9E / (double)0xE4)]
    [InlineData(false, ItemTextTier.Tertiary, 0x72 / (double)0xE4)]
    [InlineData(false, ItemTextTier.Disabled, 0x5C / (double)0xE4)]
    public void S6542_a_colour_keeps_fluents_hierarchy_relative_to_the_primary(bool dark, ItemTextTier tier, double opacity)
    {
        var ink = ItemLook.TextInk(tier, "#ffffff", dark);

        Assert.Equal(new RgbColor(255, 255, 255), ink.Colour);
        Assert.Null(ink.ThemeKey);
        Assert.Equal(opacity, ink.Opacity, 4);
    }

    [Fact]
    public void S6542_the_tiers_step_down_in_both_themes()
    {
        foreach (var dark in new[] { true, false })
        {
            var opacities = Enum.GetValues<ItemTextTier>().Select(t => ItemLook.TextOpacity(t, dark)).ToList();
            Assert.Equal(opacities.OrderByDescending(o => o), opacities);
            Assert.Equal(1, opacities[0]);
        }
    }

    [Fact]
    public void S6542_the_text_is_never_faded_by_the_item_opacity()
    {
        // The ink has no layer at all: the item layer's opacity is the fill's alone.
        var ink = ItemLook.TextInk(ItemTextTier.Primary, "#000000", dark: false);

        Assert.Equal(1, ink.Opacity);
    }

    [Fact]
    public void S6543_load_validation_repairs_a_bad_text_colour_loudly_and_keeps_a_good_one()
    {
        var state = AppState.CreateDefault();
        state.Design.ItemTextColor = "white-ish";
        var first = state.Views[0].Widgets[0];
        var second = state.Views[0].Widgets[1];
        first.ItemTextColor = "#abcdef";
        second.ItemTextColor = "#12";

        var repairs = AppStateStore.Validate(state);

        Assert.Equal(string.Empty, state.Design.ItemTextColor);
        Assert.Equal("#ABCDEF", first.ItemTextColor);
        Assert.Equal(string.Empty, second.ItemTextColor);
        Assert.Contains(repairs, r => r.Contains("item text colour 'white-ish'", StringComparison.Ordinal));
        Assert.Contains(repairs, r => r.Contains(second.Id, StringComparison.Ordinal) && r.Contains("item text colour", StringComparison.Ordinal));
        Assert.DoesNotContain(repairs, r => r.Contains(first.Id, StringComparison.Ordinal) && r.Contains("item text", StringComparison.Ordinal));
    }

    [Fact]
    public void S6543_a_missing_text_colour_loads_as_the_default()
    {
        var state = AppState.CreateDefault();
        state.Design.ItemTextColor = null!;
        state.Views[0].Widgets[0].ItemTextColor = null!;

        var repairs = AppStateStore.Validate(state);

        Assert.Equal(string.Empty, state.Design.ItemTextColor);
        Assert.Equal(string.Empty, state.Views[0].Widgets[0].ItemTextColor);
        Assert.DoesNotContain(repairs, r => r.Contains("item text", StringComparison.Ordinal));
    }

    [Fact]
    public void S6540_the_text_colour_survives_a_save_a_load_and_a_duplicate()
    {
        var state = AppState.CreateDefault();
        state.Design.ItemTextColor = "#FFFFFF";
        state.Views[0].Widgets[0].ItemTextColor = "#101010";

        var json = System.Text.Json.JsonSerializer.Serialize(state, StateJson.AppState);
        var back = System.Text.Json.JsonSerializer.Deserialize(json, StateJson.AppState)!;

        Assert.Equal("#FFFFFF", back.Design.ItemTextColor);
        Assert.Equal("#101010", back.Views[0].Widgets[0].ItemTextColor);
        Assert.Equal(string.Empty, back.Views[0].Widgets[1].ItemTextColor);
        Assert.Equal("#101010", state.Views[0].Widgets[0].Clone().ItemTextColor);
        Assert.Equal("#FFFFFF", state.Design.Clone().ItemTextColor);
    }

    [Fact]
    public void S6545_presets_own_the_text_colour_and_none_sets_one()
    {
        foreach (var preset in DesignPresets.All)
        {
            Assert.Equal(string.Empty, preset.Values.ItemTextColor);
        }

        var design = new DesignState();
        DesignPresets.Apply(design, DesignPresetId.Glass);
        design.ItemTextColor = "#FFFFFF";
        Assert.Null(DesignPresets.Match(design));

        DesignPresets.Apply(design, DesignPresetId.Daylight);
        Assert.Equal(string.Empty, design.ItemTextColor);
        Assert.Equal(DesignPresetId.Daylight, DesignPresets.Match(design));
    }

    private static (ViewState View, WidgetInstance A, WidgetInstance B) Pair(string a, string b)
    {
        var view = ViewState.Create("v");
        var first = Widget("a", a);
        var second = Widget("b", b);
        view.Widgets.AddRange([first, second]);
        return (view, first, second);
    }

    [Fact]
    public void S6544_the_offer_reads_a_shared_text_colour_none_or_mixed()
    {
        Assert.Equal(string.Empty, QuickSet.CommonItemText([Widget("a"), Widget("b")]));
        Assert.Equal("#FFFFFF", QuickSet.CommonItemText([Widget("a", "#ffffff"), Widget("b", "#FFFFFF")]));
        Assert.Null(QuickSet.CommonItemText([Widget("a", "#FFFFFF"), Widget("b")]));
        Assert.Null(QuickSet.CommonItemText([Widget("a", "#FFFFFF"), Widget("b", "#000000")]));

        var (view, _, _) = Pair("#FFFFFF", string.Empty);
        var state = new AppState { Views = [view] };
        var selection = new WidgetSelection();
        selection.Replace(view.Id, ["a"]);
        Assert.Equal("#FFFFFF", QuickSet.For(state, selection, view.Id).ItemText);
        selection.Replace(view.Id, ["a", "b"]);
        Assert.Null(QuickSet.For(state, selection, view.Id).ItemText);
    }

    [Fact]
    public void S6544_a_colour_or_default_sets_the_text_and_leaves_the_background_alone()
    {
        var own = FillChoice.Of("#202020", 0.5);
        var (view, a, b) = Pair(string.Empty, "#000000");
        a.ItemBackground = own;

        Assert.Equal(2, QuickSet.SetItemTextColor(view, ["a", "b"], "#ffffff"));
        Assert.Equal("#FFFFFF", a.ItemTextColor);
        Assert.Equal("#FFFFFF", b.ItemTextColor);
        Assert.Same(own, a.ItemBackground);
        Assert.Equal(FillKind.Default, b.ItemBackground.Kind);

        Assert.Equal(0, QuickSet.SetItemTextColor(view, ["a", "b"], "#FFFFFF"));

        Assert.Equal(2, QuickSet.SetItemTextColor(view, ["a", "b"], string.Empty));
        Assert.Equal(string.Empty, a.ItemTextColor);
        Assert.Same(own, a.ItemBackground);
    }
}
