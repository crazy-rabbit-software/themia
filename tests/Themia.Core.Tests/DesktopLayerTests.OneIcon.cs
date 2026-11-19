using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-13015] One icon per desktop item on a view, whatever put it there.</summary>
public sealed partial class DesktopLayerTests
{
    [Fact]
    public void S13015_load_keeps_the_first_icon_of_an_item_shown_twice_and_says_so()
    {
        // The owner's Today view (2026-11-19): each Public Desktop shortcut twice, the copies moving with the originals.
        var view = ViewState.Create("Today");
        var first = Widget(Desk + "a.lnk", new FracRect(0.5, 0.1, 0.03, 0.07));
        var other = Widget(Desk + "b.lnk", new FracRect(0.2, 0.1, 0.03, 0.07));
        var copy = Widget(Desk.ToUpperInvariant() + "A.LNK", new FracRect(0.5, 0.5, 0.03, 0.07));
        var own = OwnIcon(@"C:\Tools\app.exe", new FracRect(0.7, 0, 0.03, 0.07));
        var ownTwin = OwnIcon(@"C:\Tools\app.exe", new FracRect(0.8, 0, 0.03, 0.07));
        view.Widgets.AddRange([first, other, copy, own, ownTwin]);

        var repairs = DesktopViews.Repair(view);

        Assert.Equal([first, other, own, ownTwin], view.Widgets);
        Assert.Contains(repairs, r => r.Contains("second time", StringComparison.Ordinal));
        Assert.Empty(DesktopViews.Repair(view));
    }

    [Fact]
    public void S13015_a_copy_of_an_icon_pasted_on_its_own_view_moves_the_icon_there_and_adds_no_second()
    {
        var state = TwoViews(out var home, out var work);
        var icon = Widget(Desk + "a.lnk", new FracRect(0, 0, 0.03, 0.07));
        var card = new WidgetInstance { Id = "card", Config = new OtherConfig(), Rect = new FracRect(0.3, 0.3, 0.1, 0.1) };
        home.Widgets.AddRange([icon, card]);

        var pasted = Widget(Desk + "a.lnk", new FracRect(0.4, 0.5, 0.03, 0.07));
        var cardCopy = new WidgetInstance { Id = "card2", Config = new OtherConfig(), Rect = new FracRect(0.6, 0.6, 0.1, 0.1) };
        state.ApplyPlacement(new PastePlan([pasted, cardCopy], [pasted.Id, cardCopy.Id], []), home.Id);

        Assert.Equal([Desk + "a.lnk"], Keys(home));
        Assert.Equal(new FracRect(0.4, 0.5, 0.03, 0.07), icon.Rect);
        Assert.Contains(cardCopy, home.Widgets);

        // On another view the copy is that view's one icon of it.
        state.ApplyPlacement(new PastePlan([Widget(Desk + "a.lnk", new FracRect(0.1, 0.1, 0.03, 0.07))], [], []), work.Id);
        Assert.Equal([Desk + "a.lnk"], Keys(work));
    }

    [Fact]
    public void S13015_an_icon_moved_onto_a_view_that_shows_its_item_leaves_that_view_one_icon()
    {
        var state = TwoViews(out var home, out var work);
        var there = Widget(Desk + "a.lnk", new FracRect(0, 0, 0.03, 0.07));
        var moving = Widget(Desk + "a.lnk", new FracRect(0.5, 0.5, 0.03, 0.07));
        work.Widgets.Add(there);
        home.Widgets.Add(moving);

        var snapshot = new WidgetInstance { Id = moving.Id, Config = moving.Config.Clone(), Rect = new FracRect(0.2, 0.2, 0.03, 0.07) };
        state.ApplyPlacement(new PastePlan([snapshot], [moving.Id], [moving.Id]), work.Id);

        Assert.Empty(home.Widgets);
        Assert.Equal([there], work.Widgets);
        Assert.Equal(new FracRect(0.2, 0.2, 0.03, 0.07), there.Rect);
    }

    [Fact]
    public void S13015_a_pasted_icon_of_an_item_in_a_fence_stays_in_the_fence()
    {
        var state = TwoViews(out var home, out _);
        home.Widgets.Add(new WidgetInstance { Id = "fence", Config = new FenceFake { Items = [Desk + "a.lnk"] }, Rect = new FracRect(0.5, 0.1, 0.3, 0.3) });

        state.ApplyPlacement(new PastePlan([Widget(Desk + "a.lnk", new FracRect(0, 0, 0.03, 0.07))], [], []), home.Id);

        Assert.Empty(Keys(home));
    }

    [Fact]
    public void S13015_items_laid_free_or_stood_in_for_never_give_a_view_a_second_icon()
    {
        var view = ViewState.Create("Home");
        view.Widgets.Add(Widget(Desk + "a.lnk", new FracRect(0, 0, 0.03, 0.07)));

        var unplaced = DesktopFences.LayFree(view, [File("a.lnk"), File("b.lnk")], null, null, Screen, (90, 100), Create);

        Assert.Empty(unplaced);
        Assert.Equal([Desk + "a.lnk", Desk + "b.lnk"], Keys(view));
    }

    [Fact]
    public void S13015_a_rename_onto_a_key_the_view_shows_leaves_one_icon()
    {
        var state = TwoViews(out var home, out _);
        home.Widgets.AddRange([Widget(Desk + "a.lnk", new FracRect(0, 0, 0.03, 0.07)), Widget(Desk + "b.lnk", new FracRect(0, 0.1, 0.03, 0.07))]);
        state.Desktop.Known = [Desk + "a.lnk", Desk + "b.lnk"];

        var plan = DesktopReconcile.Plan(state, [File("b.lnk")], [(Desk + "a.lnk", Desk + "b.lnk")]);
        DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create);

        Assert.Equal([Desk + "b.lnk"], Keys(home));
    }
}
