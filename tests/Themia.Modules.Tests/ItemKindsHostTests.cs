using System.Text.RegularExpressions;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Tests;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-6570] [S-6571] [S-6572] [S-6573] [S-6574] Which widgets have items, and the item background a selection is offered, asked of the catalogue with every module of the build installed. Moved from Core's ItemKindsTests, unchanged in what they assert.</summary>
public sealed class ItemKindsHostTests
{

    [Fact]
    public void S6570_lists_and_meters_have_items_and_a_plain_readout_does_not()
    {
        Assert.True(ItemKinds.Has(new WidgetId("tasks")));
        Assert.True(ItemKinds.Has(new WidgetId("cpu")));
        Assert.True(ItemKinds.Has(new WidgetId("clipboard")));
        Assert.False(ItemKinds.Has(new WidgetId("alarm")));
        Assert.False(ItemKinds.Has(new WidgetId("gallery")));
    }

    private static (AppState State, ViewState View, WidgetSelection Selection) Selected(params WidgetId[] kinds)
    {
        var view = ViewState.Create("v");
        for (var i = 0; i < kinds.Length; i++)
        {
            var widget = WidgetInstance.Create(kinds[i], new FracRect(0, 0, 0.1, 0.1));
            widget.Id = $"w{i}";
            view.Widgets.Add(widget);
        }

        var selection = new WidgetSelection();
        selection.Replace(view.Id, [.. view.Widgets.Select(w => w.Id)]);
        return (new AppState { Views = [view] }, view, selection);
    }

    [Fact]
    public void S6571_a_widget_without_items_offers_no_item_background()
    {
        var (state, view, selection) = Selected(new WidgetId("alarm"));

        Assert.False(QuickSet.For(state, selection, view.Id).HasItems);
        Assert.Equal(0, QuickSet.SetItemBackground(view, ["w0"], FillChoice.Transparent()));
        Assert.Equal(FillKind.Default, view.Widgets[0].ItemBackground.Kind);

        // The item text is the items' too: none to colour.
        Assert.Equal(0, QuickSet.SetItemTextColor(view, ["w0"], "#FFFFFF"));
        Assert.Equal(string.Empty, view.Widgets[0].ItemTextColor);
    }

    [Fact]
    public void S6572_a_mixed_selection_offers_it_when_any_has_items_and_sets_only_those()
    {
        var (state, view, selection) = Selected(new WidgetId("alarm"), new WidgetId("tasks"));
        view.Widgets[0].ItemBackground = FillChoice.Of("#123456", 1);

        var offer = QuickSet.For(state, selection, view.Id);
        Assert.True(offer.HasItems);

        // The value read is the one the widgets with items share; the one without has none to show.
        Assert.Equal(FillKind.Default, offer.Items?.Kind);

        Assert.Equal(1, QuickSet.SetItemBackground(view, ["w0", "w1"], FillChoice.Transparent()));
        Assert.Equal(FillKind.Colour, view.Widgets[0].ItemBackground.Kind);
        Assert.Equal(FillKind.Transparent, view.Widgets[1].ItemBackground.Kind);
    }

    private static WidgetInstance Styled(WidgetId kind, string style)
    {
        var widget = WidgetInstance.Create(kind, new FracRect(0, 0, 0.1, 0.1));
        Assert.True(WidgetStyles.Find(kind, style) is { } option && (WidgetStyles.Set(widget.Config, option.Value) || WidgetStyles.Current(widget.Config) == option.Value));
        return widget;
    }

    [Theory]
    [InlineData("clock", "digital", false)]
    [InlineData("clock", "analog", false)]
    [InlineData("clock", "word", false)]
    [InlineData("clock", "flip", true)]
    [InlineData("cpu", "graph", false)]
    [InlineData("cpu", "cores", true)]
    [InlineData("cpu", "coreGraphs", true)]
    [InlineData("memory", "graph", false)]
    [InlineData("memory", "gauge", true)]
    [InlineData("disk", "graph", false)]
    [InlineData("disk", "bars", true)]
    [InlineData("gpu", "graph", false)]
    [InlineData("gpu", "engines", true)]
    [InlineData("ping", "graph", false)]
    [InlineData("ping", "signal", true)]
    [InlineData("network", "graph", false)]
    [InlineData("network", "numbers", false)]
    [InlineData("battery", "graph", false)]
    [InlineData("battery", "battery", false)]
    [InlineData("battery", "ring", true)]
    [InlineData("pomodoro", "ring", true)]
    [InlineData("pomodoro", "bar", false)]
    [InlineData("pomodoro", "digits", false)]
    [InlineData("hibernate", "tile", true)]
    [InlineData("hibernate", "round", false)]
    public void S6573_a_style_that_paints_no_item_has_no_item_lines(string kind, string style, bool draws)
    {
        var widget = Styled(new WidgetId(kind), style);
        Assert.Equal(draws, ItemKinds.Draws(widget));

        var (state, view, selection) = Selected(new WidgetId(kind));
        view.Widgets[0].Config = widget.Config;
        Assert.Equal(draws, QuickSet.For(state, selection, view.Id).HasItems);
        Assert.Equal(draws ? 1 : 0, QuickSet.SetItemBackground(view, ["w0"], FillChoice.Transparent()));
    }

    [Fact]
    public void S6573_music_draws_its_seek_bar_only_while_it_shows_the_progress()
    {
        var (state, view, selection) = Selected(new WidgetId("music"));
        Assert.True(QuickSet.For(state, selection, view.Id).HasItems);

        ((MusicConfig)view.Widgets[0].Config).ShowProgress = false;
        Assert.False(QuickSet.For(state, selection, view.Id).HasItems);
    }

    [Theory]
    [InlineData("focusSession")]
    [InlineData("music")]
    [InlineData("cpu")]
    public void S6574_items_that_are_only_tracks_take_a_background_but_no_text_colour(string kind)
    {
        var (state, view, selection) = Selected(new WidgetId(kind));
        if (kind == "cpu")
        {
            WidgetStyles.Set(view.Widgets[0].Config, WidgetStyles.Find(new WidgetId(kind), "cores")!.Value.Value);
        }

        var offer = QuickSet.For(state, selection, view.Id);
        Assert.True(offer.HasItems);
        Assert.False(offer.HasItemText);
        Assert.Equal(0, QuickSet.SetItemTextColor(view, ["w0"], "#FFFFFF"));
        Assert.Equal(string.Empty, view.Widgets[0].ItemTextColor);
    }

    [Fact]
    public void S6574_a_mixed_selection_offers_the_text_colour_for_the_widgets_whose_items_carry_text()
    {
        var (state, view, selection) = Selected(new WidgetId("focusSession"), new WidgetId("tasks"));
        view.Widgets[1].ItemTextColor = "#123456";

        var offer = QuickSet.For(state, selection, view.Id);
        Assert.True(offer.HasItemText);
        Assert.Equal("#123456", offer.ItemText);

        Assert.Equal(1, QuickSet.SetItemTextColor(view, ["w0", "w1"], "#FFFFFF"));
        Assert.Equal(string.Empty, view.Widgets[0].ItemTextColor);
        Assert.Equal("#FFFFFF", view.Widgets[1].ItemTextColor);
    }
}
