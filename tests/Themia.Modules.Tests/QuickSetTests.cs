using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The selected widgets' properties strip ([S-3701]..[S-3705], [S-3711]).</summary>
public sealed class QuickSetTests
{
    private static WidgetInstance Widget(WidgetId kind, string id)
    {
        var widget = WidgetInstance.Create(kind, new FracRect(0, 0, 0.1, 0.1));
        widget.Id = id;
        return widget;
    }

    private static (AppState State, ViewState View, ViewState Other) State(params WidgetInstance[] widgets)
    {
        var view = ViewState.Create("v");
        view.Widgets.AddRange(widgets);
        var other = ViewState.Create("o");
        other.Widgets.Add(Widget(new WidgetId("cpu"), "elsewhere"));
        return (new AppState { Views = [view, other] }, view, other);
    }

    private static WidgetSelection Selecting(ViewState view, params string[] ids)
    {
        var selection = new WidgetSelection();
        selection.Replace(view.Id, ids);
        return selection;
    }

    [Fact]
    public void S3701_nothing_selected_shows_no_group()
    {
        var (state, view, _) = State(Widget(new WidgetId("cpu"), "a"));

        Assert.False(QuickSet.For(state, new WidgetSelection(), view.Id).Shows);
    }

    [Fact]
    public void S3701_a_display_showing_another_view_shows_no_group()
    {
        var (state, view, other) = State(Widget(new WidgetId("cpu"), "a"));

        Assert.False(QuickSet.For(state, Selecting(view, "a"), other.Id).Shows);
        Assert.True(QuickSet.For(state, Selecting(view, "a"), view.Id).Shows);
    }

    [Fact]
    public void S3701_ids_no_longer_in_the_view_are_not_counted()
    {
        var (state, view, _) = State(Widget(new WidgetId("cpu"), "a"));

        var offer = QuickSet.For(state, Selecting(view, "a", "gone"), view.Id);

        Assert.Equal(1, offer.Count);
        Assert.Equal("a", offer.TitleWidgetId);
        Assert.False(QuickSet.For(state, Selecting(view, "gone"), view.Id).Shows);
    }

    [Fact]
    public void S3702_one_widget_offers_its_background_and_which_one_it_is()
    {
        var clock = Widget(new WidgetId("clock"), "a");
        clock.Background = FillChoice.Of("#202020", 0.6);
        var (state, view, _) = State(clock);

        var offer = QuickSet.For(state, Selecting(view, "a"), view.Id);

        Assert.True(offer.HasTitle);
        Assert.Equal(new WidgetId("clock"), offer.TitleKind);
        Assert.True(FillChoice.Of("#202020", 0.6).SameAs(offer.Background));
    }

    [Fact]
    public void S3702_several_widgets_offer_no_title()
    {
        var (state, view, _) = State(Widget(new WidgetId("cpu"), "a"), Widget(new WidgetId("memory"), "b"));

        var offer = QuickSet.For(state, Selecting(view, "a", "b"), view.Id);

        Assert.Equal(2, offer.Count);
        Assert.False(offer.HasTitle);
        Assert.Null(offer.TitleKind);
    }

    [Fact]
    public void S3703_a_mixed_selection_reads_as_no_value_and_a_shared_one_as_that_value()
    {
        var a = Widget(new WidgetId("cpu"), "a");
        var b = Widget(new WidgetId("memory"), "b");
        a.Background = FillChoice.Transparent();
        b.Background = FillChoice.Of("#202020", 1);
        var (state, view, _) = State(a, b);

        var offer = QuickSet.For(state, Selecting(view, "a", "b"), view.Id);

        Assert.Null(offer.Background);
        a.Scale = b.Scale = 1.5;
        Assert.Equal(1.5, QuickSet.For(state, Selecting(view, "a", "b"), view.Id).Scale);
    }

    [Fact]
    public void S3703_common_of_nothing_is_nothing()
    {
        Assert.Null(QuickSet.Common(Array.Empty<ContentAlign>()));
        Assert.Equal(ContentAlign.Right, QuickSet.Common([ContentAlign.Right, ContentAlign.Right]));
    }

    [Fact]
    public void S3703_a_choice_reaches_every_selected_widget_of_the_view_and_no_other()
    {
        var a = Widget(new WidgetId("cpu"), "a");
        var b = Widget(new WidgetId("memory"), "b");
        var c = Widget(new WidgetId("network"), "c");
        b.Background = FillChoice.Transparent();
        var (_, view, other) = State(a, b, c);

        Assert.Equal(1, QuickSet.SetBackground(view, ["a", "b", "elsewhere"], FillChoice.Transparent()));
        Assert.Equal(FillKind.Transparent, a.Background.Kind);
        Assert.Equal(FillKind.Default, c.Background.Kind);
        Assert.Equal(FillKind.Default, other.Widgets[0].Background.Kind);
    }

    [Fact]
    public void S6593_the_background_image_is_set_on_every_selected_widget_and_removed_with_empty()
    {
        var (a, b) = (Widget(new WidgetId("cpu"), "a"), Widget(new WidgetId("clock"), "b"));
        var (state, view, other) = State(a, b);
        const string lake = @"C:\Users\me\Pictures\lake.jpg";

        Assert.Equal(2, QuickSet.SetBackgroundImage(view, ["a", "b", "elsewhere"], lake));
        Assert.Equal(lake, a.BackgroundImage);
        Assert.Null(other.Widgets[0].BackgroundImage);
        Assert.Equal(lake, QuickSet.For(state, Selecting(view, "a", "b"), view.Id).BackgroundImage);

        Assert.Equal(0, QuickSet.SetBackgroundImage(view, ["a"], "notes.txt"));
        Assert.Equal(lake, a.BackgroundImage);

        Assert.Equal(1, QuickSet.SetBackgroundImage(view, ["a"], ""));
        Assert.Null(a.BackgroundImage);
        Assert.Null(QuickSet.For(state, Selecting(view, "a", "b"), view.Id).BackgroundImage);
        Assert.Equal(string.Empty, QuickSet.For(state, Selecting(view, "a"), view.Id).BackgroundImage);
    }

    [Fact]
    public void S6590_a_copy_and_an_undo_keep_the_background_image()
    {
        var a = Widget(new WidgetId("cpu"), "a");
        a.BackgroundImage = @"C:\walls\forest.png";

        Assert.Equal(a.BackgroundImage, a.Clone().BackgroundImage);

        var shape = WidgetShape.Of(a);
        Assert.Equal(a.BackgroundImage, shape.Look.BackgroundImage);
        a.BackgroundImage = null;
        Assert.False(shape.SameLayout(WidgetShape.Of(a)));
    }

    [Fact]
    public void S6591_a_stored_background_image_that_is_no_picture_path_loads_as_none()
    {
        var a = Widget(new WidgetId("cpu"), "a");
        a.BackgroundImage = "notes.txt";
        var (state, _, _) = State(a);

        var parsed = StateDocument.Parse(System.Text.Json.JsonSerializer.Serialize(state, StateJson.AppState));

        Assert.Null(parsed.State!.Views[0].Widgets[0].BackgroundImage);
        Assert.Contains(parsed.Repairs, r => r.Contains("background image", StringComparison.Ordinal));
    }
}
