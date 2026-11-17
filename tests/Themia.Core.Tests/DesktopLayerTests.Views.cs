using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5525]..[S-5528] Which desktop icons each view shows: All, Chosen or None.</summary>
public sealed partial class DesktopLayerTests
{
    private static AppState ThreeViews(out ViewState all, out ViewState chosen, out ViewState none)
    {
        all = ViewState.Create("All");
        chosen = ViewState.Create("Chosen");
        chosen.DesktopIcons = ViewDesktopIcons.Chosen;
        none = ViewState.Create("None");
        none.DesktopIcons = ViewDesktopIcons.None;
        return new AppState { Views = [all, chosen, none], ActiveViewId = all.Id };
    }

    private static string StateWith(string viewFields) => $$"""
        { "schemaVersion": 1, "views": [ { "id": "v1", "name": "Desktop", {{viewFields}} "widgets": [] } ], "activeViewId": "v1", "monitorViews": {} }
        """;

    [Fact]
    public void S5525_every_view_shows_all_desktop_icons_until_it_is_set_otherwise()
    {
        Assert.Equal(ViewDesktopIcons.All, ViewState.Create("New").DesktopIcons);

        var plain = AppStateStore.Parse(StateWith(string.Empty));
        Assert.Equal(ViewDesktopIcons.All, plain.State!.Views[0].DesktopIcons);
        Assert.Empty(plain.Repairs);

        var chosen = AppStateStore.Parse(StateWith("\"desktopIcons\": \"Chosen\","));
        Assert.Equal(ViewDesktopIcons.Chosen, chosen.State!.Views[0].DesktopIcons);
        Assert.Empty(chosen.Repairs);
    }

    [Fact]
    public void S5525_load_repairs_an_unknown_value_to_all_and_empties_a_none_view_loudly()
    {
        var unknown = AppStateStore.Parse(StateWith("\"desktopIcons\": 7,"));
        Assert.Equal(ViewDesktopIcons.All, unknown.State!.Views[0].DesktopIcons);
        Assert.Contains(unknown.Repairs, r => r.Contains("desktop icons", StringComparison.Ordinal));

        var view = ViewState.Create("None");
        view.DesktopIcons = ViewDesktopIcons.None;
        var own = OwnIcon(@"C:\Tools\app.exe", new FracRect(0.5, 0, 0.1, 0.1));
        view.Widgets.AddRange([Widget(Desk + "a.txt", new FracRect(0, 0, 0.1, 0.1)), own]);

        var repairs = DesktopViews.Repair(view);

        Assert.Equal([own], view.Widgets);
        Assert.Single(repairs);
    }

    [Fact]
    public void S5526_a_new_item_arrives_on_all_views_only_never_on_chosen_or_none()
    {
        var state = ThreeViews(out var all, out var chosen, out var none);
        var second = ViewState.Create("All too");
        state.Views.Add(second);

        var plan = DesktopReconcile.Plan(state, [File("a.txt")], []);
        DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create);

        Assert.Equal([Desk + "a.txt"], Keys(all));
        Assert.Equal([Desk + "a.txt"], Keys(second));
        Assert.Empty(chosen.Widgets);
        Assert.Empty(none.Widgets);
        Assert.Equal([Desk + "a.txt"], state.Desktop.Known);
    }

    [Fact]
    public void S5526_a_drop_on_a_chosen_view_picks_it_there_and_other_all_views_get_it_at_their_first_free_spot()
    {
        var state = ThreeViews(out var all, out var chosen, out var none);
        var screen = new ScreenMetrics(1000, 1000) { Allowed = new FracRect(0, 0, 1, 1) };
        var plan = DesktopReconcile.Plan(state, [File("photo.jpg")], []);

        DesktopReconcile.Apply(state, plan, screen, (100, 250), Create, [new DesktopLandingSpot(chosen.Id, 0.55, 0.3)], _ => screen);

        NearRect(new FracRect(0.5, 0.18, 0.1, 0.25), Assert.Single(chosen.Widgets).Rect);
        var first = Assert.Single(all.Widgets);
        Assert.Equal([Desk + "photo.jpg"], Keys(all));
        Assert.True(first.Rect.X < 0.2 && first.Rect.Y < 0.2, $"the all view's copy is at its first free spot, not at the drop: {first.Rect}");
        Assert.Empty(none.Widgets);
    }

    [Fact]
    public void S5526_a_drop_on_a_none_view_is_not_shown_there_but_arrives_on_the_all_views()
    {
        var state = ThreeViews(out var all, out var chosen, out var none);
        var plan = DesktopReconcile.Plan(state, [File("photo.jpg")], []);

        var unplaced = DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create, [new DesktopLandingSpot(none.Id, 0.5, 0.5)], _ => Screen);

        Assert.Empty(unplaced);
        Assert.Empty(none.Widgets);
        Assert.Empty(chosen.Widgets);
        Assert.Equal([Desk + "photo.jpg"], Keys(all));
        Assert.Equal([Desk + "photo.jpg"], state.Desktop.Known);
    }

    [Fact]
    public void S5526_a_deleted_file_leaves_every_view_and_a_rename_keeps_every_place()
    {
        var state = ThreeViews(out var all, out var chosen, out _);
        var here = new FracRect(0.3, 0.4, 0.09, 0.125);
        var there = new FracRect(0.6, 0.1, 0.09, 0.125);
        all.Widgets.AddRange([Widget(Desk + "old.txt", here), Widget(Desk + "gone.txt", there)]);
        chosen.Widgets.AddRange([Widget(Desk + "old.txt", there), Widget(Desk + "gone.txt", here)]);
        state.Desktop.Known = [Desk + "old.txt", Desk + "gone.txt"];

        var plan = DesktopReconcile.Plan(state, [File("new.txt")], [(Desk + "old.txt", Desk + "new.txt")]);
        DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create);

        Assert.Equal([Desk + "new.txt"], Keys(all));
        Assert.Equal(here, all.Widgets[0].Rect);
        Assert.Equal([Desk + "new.txt"], Keys(chosen));
        Assert.Equal(there, chosen.Widgets[0].Rect);
    }

    [Fact]
    public void S5526_a_view_that_already_shows_an_arrival_gets_no_second()
    {
        // An undo brought a widget back before its item counted as known.
        var state = ThreeViews(out var all, out _, out _);
        all.Widgets.Add(Widget(Desk + "a.txt", new FracRect(0.3, 0.4, 0.09, 0.125)));

        var plan = DesktopReconcile.Plan(state, [File("a.txt")], []);
        DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create);

        Assert.Single(all.Widgets);
    }

    [Fact]
    public void S5527_hiding_takes_only_that_items_icon_off_that_view_and_keeps_its_value()
    {
        var state = ThreeViews(out var all, out var chosen, out _);
        var own = OwnIcon(Desk + "a.txt", new FracRect(0.5, 0.5, 0.1, 0.1));
        all.Widgets.AddRange([Widget(Desk + "a.txt", new FracRect(0, 0, 0.1, 0.1)), Widget(Desk + "b.txt", new FracRect(0.2, 0, 0.1, 0.1)), own]);
        chosen.Widgets.Add(Widget(Desk + "A.TXT", new FracRect(0, 0, 0.1, 0.1)));

        Assert.Equal(1, DesktopViews.Hide(all, [Desk + "a.txt"]));

        Assert.Equal([Desk + "b.txt", string.Empty], Keys(all));
        Assert.Contains(own, all.Widgets);
        Assert.Equal(ViewDesktopIcons.All, all.DesktopIcons);
        Assert.Single(chosen.Widgets);

        // [S-5523] Off an All view it is offered again by Add widget; a None view is offered nothing.
        Assert.Equal([Desk + "a.txt"], DesktopReconcile.Unplaced(all, [File("a.txt"), File("b.txt")]).Select(i => i.Key));
        Assert.Empty(DesktopReconcile.Unplaced(state.Views[2], [File("a.txt")]));
    }

    [Fact]
    public void S5527_showing_picks_an_item_for_a_chosen_view_at_its_first_free_spot_and_never_twice()
    {
        var state = ThreeViews(out _, out var chosen, out var none);
        chosen.Widgets.Add(Widget(Desk + "a.txt", new FracRect(0.01, 0.0125, 0.09, 0.125)));

        var unplaced = DesktopViews.Fill(chosen, [File("a.txt"), File("b.txt")], Screen, (90, 100), Create);

        Assert.Empty(unplaced);
        Assert.Equal([Desk + "a.txt", Desk + "b.txt"], Keys(chosen));
        Assert.Equal(DesktopViews.Shown(chosen), new HashSet<string>([Desk + "a.txt", Desk + "b.txt"], DesktopItemKeys.Comparer));
        Assert.Equal(0.0125, chosen.Widgets[1].Rect.Y, 6);
        Assert.Equal(0.10, chosen.Widgets[1].Rect.X, 6);

        // A None view takes none.
        Assert.Empty(DesktopViews.Fill(none, [File("a.txt")], Screen, (90, 100), Create));
        Assert.Empty(none.Widgets);
        Assert.Empty(state.Views[0].Widgets);
    }

    [Fact]
    public void S5528_all_to_chosen_keeps_what_it_shows_as_its_chosen_set_and_nothing_moves()
    {
        var view = ViewState.Create("v");
        var a = Widget(Desk + "a.txt", new FracRect(0.3, 0.4, 0.09, 0.125));
        view.Widgets.Add(a);

        Assert.True(DesktopViews.Set(view, ViewDesktopIcons.Chosen));

        Assert.Equal(ViewDesktopIcons.Chosen, view.DesktopIcons);
        Assert.Equal([a], view.Widgets);
        Assert.Equal(new FracRect(0.3, 0.4, 0.09, 0.125), a.Rect);
        Assert.False(DesktopViews.Set(view, ViewDesktopIcons.Chosen));
    }

    [Fact]
    public void S5528_chosen_to_all_brings_the_missing_items_at_the_first_free_spots()
    {
        var view = ViewState.Create("v");
        view.DesktopIcons = ViewDesktopIcons.Chosen;
        var kept = new FracRect(0.5, 0.5, 0.09, 0.125);
        view.Widgets.Add(Widget(Desk + "b.txt", kept));

        Assert.True(DesktopViews.Set(view, ViewDesktopIcons.All));
        var unplaced = DesktopViews.Fill(view, [File("c.txt"), File("b.txt"), File("a.txt")], Screen, (90, 100), Create);

        Assert.Empty(unplaced);
        Assert.Equal([Desk + "b.txt", Desk + "a.txt", Desk + "c.txt"], Keys(view));
        Assert.Equal(kept, view.Widgets[0].Rect);
        Assert.Equal((0.01, 0.0125), (view.Widgets[1].Rect.X, view.Widgets[1].Rect.Y));
    }

    [Fact]
    public void S5528_none_takes_every_item_icon_off_keeps_own_icons_and_keeps_no_places()
    {
        var view = ViewState.Create("v");
        var own = OwnIcon(@"C:\Tools\app.exe", new FracRect(0.5, 0.5, 0.1, 0.1));
        view.Widgets.AddRange([Widget(Desk + "a.txt", new FracRect(0.3, 0.4, 0.09, 0.125)), own]);

        Assert.True(DesktopViews.Set(view, ViewDesktopIcons.None));

        Assert.Equal([own], view.Widgets);

        // Back to Chosen it starts empty; back to All every item comes in at the first free spots (the undo of the switch
        // is what brings the old places back).
        Assert.True(DesktopViews.Set(view, ViewDesktopIcons.All));
        DesktopViews.Fill(view, [File("a.txt")], Screen, (90, 100), Create);
        Assert.Equal((0.01, 0.0125), (view.Widgets[1].Rect.X, view.Widgets[1].Rect.Y));
    }

    [Fact]
    public void S5528_a_switch_is_part_of_the_undo_snapshot()
    {
        // The value alone (the widgets an undo brings back are any widgets': LayoutHistoryTests); a view with no icon
        // switched to None is still a change to undo.
        var state = ThreeViews(out var all, out _, out _);
        var before = LayoutShape.Capture(state);

        DesktopViews.Set(all, ViewDesktopIcons.None);
        Assert.False(before.SameLayout(LayoutShape.Capture(state)));

        before.RestoreInto(state);

        Assert.Equal(ViewDesktopIcons.All, state.Views[0].DesktopIcons);
        Assert.True(before.SameLayout(LayoutShape.Capture(state)));
    }

    [Fact]
    public void S5528_a_duplicate_keeps_the_views_value()
    {
        var state = ThreeViews(out _, out var chosen, out _);

        var copy = ViewOps.Duplicate(state, chosen.Id);

        Assert.Equal(ViewDesktopIcons.Chosen, copy!.DesktopIcons);
    }
}
