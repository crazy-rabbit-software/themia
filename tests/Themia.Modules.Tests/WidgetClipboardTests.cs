using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

public class WidgetClipboardTests
{
    private static readonly ScreenMetrics Screen = new(1920, 1080);

    private static (AppState State, ViewState View, WidgetInstance Widget) Fixture()
    {
        // One clock, not the first-run desktop ([S-893]): the counts below are about that one widget.
        var state = AppState.CreateDefault();
        var view = state.ActiveView();
        view.Widgets.RemoveRange(1, view.Widgets.Count - 1);
        return (state, view, view.Widgets[0]);
    }

    private static PastePlan Paste(WidgetClipboard clipboard, double x = 0.5, double y = 0.5, ScreenMetrics? screen = null, bool snap = false)
        => clipboard.Payload!.Plan(x, y, screen ?? Screen, snap);

    [Fact]
    public void An_empty_clipboard_has_nothing_to_place()
    {
        var clipboard = new WidgetClipboard();

        Assert.False(clipboard.HasContent);
        Assert.Null(clipboard.Payload);
    }

    [Fact]
    public void Copying_ids_the_view_does_not_have_leaves_the_clipboard_alone()
    {
        var (_, view, _) = Fixture();
        var clipboard = new WidgetClipboard();

        Assert.False(clipboard.Copy(view, ["nope"]));
        Assert.False(clipboard.HasContent);
    }

    [Fact]
    public void S72_a_pasted_copy_gets_a_new_id_so_it_can_coexist_with_the_original()
    {
        var (_, view, widget) = Fixture();
        var clipboard = new WidgetClipboard();
        clipboard.Copy(view, [widget.Id]);

        var pasted = Assert.Single(Paste(clipboard).Added);

        Assert.NotEqual(widget.Id, pasted.Id);
        Assert.Equal(widget.Kind, pasted.Kind);
    }

    [Fact]
    public void S72_a_copy_carries_the_config_not_a_shared_reference()
    {
        var (_, view, widget) = Fixture();
        ((ClockConfig)widget.Config).ShowSeconds = true;
        var clipboard = new WidgetClipboard();
        clipboard.Copy(view, [widget.Id]);

        // Mutating the original afterwards must not reach into the clipboard entry.
        ((ClockConfig)widget.Config).ShowSeconds = false;

        Assert.True(((ClockConfig)Paste(clipboard).Added[0].Config).ShowSeconds);
    }

    [Fact]
    public void S72_a_copy_survives_the_original_being_deleted()
    {
        var (_, view, widget) = Fixture();
        var clipboard = new WidgetClipboard();
        clipboard.Copy(view, [widget.Id]);
        view.Widgets.Clear();

        Assert.True(clipboard.HasContent);
        Assert.Single(Paste(clipboard).Added);
    }

    [Fact]
    public void A_copy_plan_removes_nothing()
    {
        var (_, view, widget) = Fixture();
        var clipboard = new WidgetClipboard();
        clipboard.Copy(view, [widget.Id]);

        Assert.Empty(Paste(clipboard).RemoveIds);
    }

    [Fact]
    public void S73_a_cut_plan_names_the_original_to_move_and_keeps_its_id()
    {
        var (_, view, widget) = Fixture();
        var clipboard = new WidgetClipboard();
        clipboard.Cut(view, [widget.Id]);

        var plan = Paste(clipboard);

        Assert.Equal([widget.Id], plan.RemoveIds);
        Assert.Equal(widget.Id, Assert.Single(plan.Added).Id);
    }

    [Fact]
    public void S73_a_cut_does_not_remove_anything_until_it_is_pasted()
    {
        var (_, view, widget) = Fixture();
        var clipboard = new WidgetClipboard();
        clipboard.Cut(view, [widget.Id]);

        // Marking only: changing your mind after a cut must cost nothing.
        Assert.Single(view.Widgets);
        Assert.Contains(view.Widgets, w => w.Id == widget.Id);
    }

    [Fact]
    public void S73_a_cut_is_consumed_by_the_placement_that_lands_it_but_a_copy_is_not()
    {
        var (_, view, widget) = Fixture();

        var cut = new WidgetClipboard();
        cut.Cut(view, [widget.Id]);
        cut.OnPlaced(cut.Payload!);
        Assert.False(cut.HasContent);

        var copy = new WidgetClipboard();
        copy.Copy(view, [widget.Id]);
        copy.OnPlaced(copy.Payload!);
        Assert.True(copy.HasContent);
    }

    [Fact]
    public void S1590_landing_some_other_placement_does_not_consume_the_cut()
    {
        var (_, view, widget) = Fixture();
        var clipboard = new WidgetClipboard();
        clipboard.Cut(view, [widget.Id]);

        clipboard.OnPlaced(PlacementPayload.ForNew(new WidgetId("clock"), 200, 200));

        Assert.True(clipboard.HasContent);
    }

    [Fact]
    public void S74_a_paste_is_centred_on_the_drop_point()
    {
        var (_, view, widget) = Fixture();
        widget.Rect = new FracRect(0.1, 0.1, 0.2, 0.1);
        var clipboard = new WidgetClipboard();
        clipboard.Copy(view, [widget.Id]);

        var rect = Paste(clipboard).Added[0].Rect;

        Assert.Equal(0.5, rect.X + (rect.W / 2), 6);
        Assert.Equal(0.5, rect.Y + (rect.H / 2), 6);
    }

    [Fact]
    public void S74_a_paste_keeps_the_widget_size()
    {
        var (_, view, widget) = Fixture();
        widget.Rect = new FracRect(0.1, 0.1, 0.25, 0.15);
        var clipboard = new WidgetClipboard();
        clipboard.Copy(view, [widget.Id]);

        var rect = Paste(clipboard).Added[0].Rect;

        Assert.Equal(0.25, rect.W, 6);
        Assert.Equal(0.15, rect.H, 6);
    }

    [Fact]
    public void S74_S6608_a_paste_beyond_a_screen_corner_keeps_the_visible_minimum_on_it()
    {
        var (_, view, widget) = Fixture();
        widget.Rect = new FracRect(0.1, 0.1, 0.3, 0.3);
        var clipboard = new WidgetClipboard();
        clipboard.Copy(view, [widget.Id]);

        var rect = Paste(clipboard, 1.5, 1.5, snap: true).Added[0].Rect;

        Assert.Equal(1 - (GridGeometry.MinVisiblePx / Screen.Width), rect.X, 9);
        Assert.Equal(1 - (GridGeometry.MinVisiblePx / Screen.Height), rect.Y, 9);
    }

    [Fact]
    public void S74_S6608_pasting_onto_a_differently_shaped_display_still_lands_in_reach()
    {
        var (_, view, widget) = Fixture();
        widget.Rect = new FracRect(0.4, 0.4, 0.5, 0.5);
        var clipboard = new WidgetClipboard();
        clipboard.Copy(view, [widget.Id]);

        var portrait = new ScreenMetrics(1080, 1920);
        var rect = Paste(clipboard, 1.4, 1.4, portrait).Added[0].Rect;

        Assert.True((1 - rect.X) * portrait.Width >= GridGeometry.MinVisiblePx - 1e-6);
        Assert.True((1 - rect.Y) * portrait.Height >= GridGeometry.MinVisiblePx - 1e-6);
    }

    [Fact]
    public void Clearing_raises_Changed_once_and_only_when_there_was_content()
    {
        var (_, view, widget) = Fixture();
        var clipboard = new WidgetClipboard();
        var changes = 0;
        clipboard.Changed += () => changes++;

        clipboard.Clear();          // nothing to clear
        Assert.Equal(0, changes);

        clipboard.Copy(view, [widget.Id]);
        Assert.Equal(1, changes);

        clipboard.Clear();
        Assert.Equal(2, changes);
    }
}
