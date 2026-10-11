using Themia.Core.Geometry;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The edit bar's selection group ([S-3700]..[S-3705]).</summary>
public sealed class QuickSetTests
{
    private static WidgetInstance Widget(WidgetKind kind, string id)
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
        other.Widgets.Add(Widget(WidgetKind.Cpu, "elsewhere"));
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
        var (state, view, _) = State(Widget(WidgetKind.Cpu, "a"));

        Assert.False(QuickSet.For(state, new WidgetSelection(), view.Id).Shows);
    }

    [Fact]
    public void S3701_a_display_showing_another_view_shows_no_group()
    {
        var (state, view, other) = State(Widget(WidgetKind.Cpu, "a"));

        Assert.False(QuickSet.For(state, Selecting(view, "a"), other.Id).Shows);
        Assert.True(QuickSet.For(state, Selecting(view, "a"), view.Id).Shows);
    }

    [Fact]
    public void S3701_ids_no_longer_in_the_view_are_not_counted()
    {
        var (state, view, _) = State(Widget(WidgetKind.Cpu, "a"));

        var offer = QuickSet.For(state, Selecting(view, "a", "gone"), view.Id);

        Assert.Equal(1, offer.Count);
        Assert.Equal("a", offer.TitleWidgetId);
        Assert.False(QuickSet.For(state, Selecting(view, "gone"), view.Id).Shows);
    }

    [Fact]
    public void S3702_one_widget_offers_header_background_and_title()
    {
        var clock = Widget(WidgetKind.Clock, "a");
        clock.Header = HeaderPlacement.Bottom;
        clock.Surface = WidgetSurface.Translucent;
        clock.Label = "Zürich";
        var (state, view, _) = State(clock);

        var offer = QuickSet.For(state, Selecting(view, "a"), view.Id);

        Assert.True(offer.HasTitle);
        Assert.Equal(WidgetKind.Clock, offer.TitleKind);
        Assert.Equal("Zürich", offer.Label);
        Assert.Equal(HeaderPlacement.Bottom, offer.Header);
        Assert.Equal(WidgetSurface.Translucent, offer.Surface);
    }

    [Fact]
    public void S3702_several_widgets_offer_no_title()
    {
        var (state, view, _) = State(Widget(WidgetKind.Cpu, "a"), Widget(WidgetKind.Memory, "b"));

        var offer = QuickSet.For(state, Selecting(view, "a", "b"), view.Id);

        Assert.Equal(2, offer.Count);
        Assert.False(offer.HasTitle);
        Assert.Null(offer.TitleKind);
    }

    [Fact]
    public void S3703_a_mixed_selection_reads_as_no_value_and_a_shared_one_as_that_value()
    {
        var a = Widget(WidgetKind.Cpu, "a");
        var b = Widget(WidgetKind.Memory, "b");
        a.Header = HeaderPlacement.Top;
        b.Header = HeaderPlacement.Hidden;
        a.Surface = b.Surface = WidgetSurface.Transparent;
        var (state, view, _) = State(a, b);

        var offer = QuickSet.For(state, Selecting(view, "a", "b"), view.Id);

        Assert.Null(offer.Header);
        Assert.Equal(WidgetSurface.Transparent, offer.Surface);
    }

    [Fact]
    public void S3703_common_of_nothing_is_nothing()
    {
        Assert.Null(QuickSet.Common(Array.Empty<HeaderPlacement>()));
        Assert.Equal(HeaderPlacement.Bottom, QuickSet.Common([HeaderPlacement.Bottom, HeaderPlacement.Bottom]));
    }

    [Fact]
    public void S3703_a_choice_reaches_every_selected_widget_of_the_view_and_no_other()
    {
        var a = Widget(WidgetKind.Cpu, "a");
        var b = Widget(WidgetKind.Memory, "b");
        var c = Widget(WidgetKind.Disk, "c");
        b.Surface = WidgetSurface.Transparent;
        var (_, view, other) = State(a, b, c);

        Assert.Equal(1, QuickSet.SetSurface(view, ["a", "b", "elsewhere"], WidgetSurface.Transparent));
        Assert.Equal(WidgetSurface.Transparent, a.Surface);
        Assert.Equal(WidgetSurface.Card, c.Surface);
        Assert.Equal(WidgetSurface.Card, other.Widgets[0].Surface);

        Assert.Equal(2, QuickSet.SetHeader(view, ["a", "b"], HeaderPlacement.Hidden));
        Assert.Equal(0, QuickSet.SetHeader(view, ["a", "b"], HeaderPlacement.Hidden));
        Assert.Equal(HeaderPlacement.Top, c.Header);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("Clock", null)]
    [InlineData("  Clock ", null)]
    [InlineData("clock", "clock")]
    [InlineData("  Zürich  ", "Zürich")]
    public void S3704_a_blank_title_or_the_kinds_name_means_the_kinds_name(string? typed, string? stored)
    {
        Assert.Equal(stored, QuickSet.Title(typed, "Clock"));
    }

    [Fact]
    public void S3704_set_title_reports_a_change_only_when_the_stored_title_moves()
    {
        var clock = Widget(WidgetKind.Clock, "a");
        var (_, view, _) = State(clock);

        Assert.True(QuickSet.SetTitle(view, "a", " Home ", "Clock"));
        Assert.Equal("Home", clock.Label);
        Assert.False(QuickSet.SetTitle(view, "a", "Home", "Clock"));
        Assert.True(QuickSet.SetTitle(view, "a", "", "Clock"));
        Assert.Null(clock.Label);
        Assert.False(QuickSet.SetTitle(view, "gone", "x", "Clock"));
    }
}
