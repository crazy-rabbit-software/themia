using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14912] [S-14916] The widget gallery over every widget of the build, and its drag as the one placement session. Here,
/// not in Core's tests, because the gallery is built from every widget and a new widget's box from its sizing.
/// </summary>
public sealed class WidgetGalleryPlacementTests
{
    private static readonly ScreenMetrics Screen = new(1920, 1080) { Allowed = new FracRect(0, 0, 1, 1032 / 1080.0) };

    private static readonly PlacementSizes Sizes = new(1);

    private static (int W, int H) Cells(WidgetId kind)
    {
        var natural = ModuleCatalog.Of(kind).Natural;
        var box = PlacementPayload.NewSize(kind, natural.Width, natural.Height, Screen, Sizes);
        var (cols, rows) = GridGeometry.GridDims(Screen);
        return ((int)Math.Round(box.W * cols), (int)Math.Round(box.H * rows));
    }

    private static FracRect Rect(CellBox box)
    {
        var (cols, rows) = GridGeometry.GridDims(Screen);
        return new FracRect(box.X / (double)cols, box.Y / (double)rows, box.W / (double)cols, box.H / (double)rows);
    }

    [Fact]
    public void S14912_every_widget_of_the_build_stands_once_in_the_gallery_in_its_subject_tidily()
    {
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        var categories = WidgetGallery.Of(page);
        var area = new CellBox(3, 13, 150, 87);

        var screens = WidgetGallery.Lay(categories, area, Cells);

        // Every kind the add page homes, once, in the subject the page homes it in.
        var laid = screens.SelectMany(s => s.Cards.Select(c => (s.Category, c.Kind))).ToList();
        Assert.Equal(page.Homes.Count, laid.Count);
        Assert.Equal(page.Homes.Select(h => h.Kind).Order(), laid.Select(l => l.Kind).Order());
        foreach (var (category, kind) in laid)
        {
            var home = page.Homes.Single(h => h.Kind == kind);
            Assert.Equal(home.Path[0].Id, category);
        }

        // On whole cells inside the area, no two sharing a cell, each at its natural cells (held to the area).
        foreach (var screen in screens)
        {
            Assert.All(screen.Cards, c => Assert.True(area.Holds(c.Box), $"{c.Kind} at {c.Box}"));
            for (var i = 0; i < screen.Cards.Count; i++)
            {
                for (var j = i + 1; j < screen.Cards.Count; j++)
                {
                    Assert.False(screen.Cards[i].Box.Overlaps(screen.Cards[j].Box));
                }
            }

            Assert.All(screen.Cards, c => Assert.Equal((Math.Min(Cells(c.Kind).W, area.W), Math.Min(Cells(c.Kind).H, area.H - WidgetGallery.TitleRows)), (c.Box.W, c.Box.H)));
        }

        // Start is no category: it only shows again what is at home elsewhere.
        Assert.DoesNotContain(categories, c => c.Id == AddFolderId.Start);
    }

    private static ViewState Gallery(params (string Kind, CellBox Box)[] cards)
    {
        var view = ViewState.Create("gallery");
        foreach (var (kind, box) in cards)
        {
            view.Widgets.Add(WidgetInstance.Create(new WidgetId(kind), Rect(box)));
        }

        return view;
    }

    [Fact]
    public void S14916_picked_up_off_the_gallery_the_block_starts_where_it_stood_and_moves_cell_by_cell()
    {
        var gallery = Gallery(("clock", new CellBox(20, 20, 26, 12)), ("date", new CellBox(50, 20, 20, 10)), ("cpu", new CellBox(20, 40, 14, 14)));
        var picked = gallery.Widgets.Take(2).Select(w => w.Id).ToList();

        // Held at a point inside the clock, off its centre.
        var (cols, rows) = GridGeometry.GridDims(Screen);
        var held = (X: 23.37 / cols, Y: 27.81 / rows);
        var payload = PlacementPayload.ForGallery(gallery, picked, held.X, held.Y)!;

        // No jump: with the pointer still where it held them, the ghost is exactly the cells they stood on.
        var start = payload.Outlines(held.X, held.Y, Screen, Sizes);
        Assert.Equal(gallery.Widgets.Take(2).Select(w => w.Rect), start.Select(o => o.Rect));

        // Moved by 7.4 cells across and 3.6 down: whole cells, every widget by the same, their offsets kept.
        var moved = payload.Outlines(held.X + (7.4 / cols), held.Y + (3.6 / rows), Screen, Sizes);
        for (var i = 0; i < 2; i++)
        {
            var from = gallery.Widgets[i].Rect;
            Assert.Equal(from.X + (7.0 / cols), moved[i].Rect.X, 9);
            Assert.Equal(from.Y + (4.0 / rows), moved[i].Rect.Y, 9);
            Assert.Equal((from.W, from.H), (moved[i].Rect.W, moved[i].Rect.H));
        }

        // The landing is where the ghost was.
        var plan = payload.Plan(held.X + (7.4 / cols), held.Y + (3.6 / rows), Screen, Sizes);
        Assert.Equal(moved.Select(o => o.Rect), plan.Added.Select(w => w.Rect));
    }

    [Fact]
    public void S14916_the_gallerys_widgets_land_new_and_the_gallery_keeps_them()
    {
        var gallery = Gallery(("clock", new CellBox(20, 20, 26, 12)), ("date", new CellBox(50, 20, 20, 10)));
        var ids = gallery.Widgets.Select(w => w.Id).ToList();
        var payload = PlacementPayload.ForGallery(gallery, ids, 0.2, 0.2)!;

        Assert.Equal(PlacementMode.Create, payload.Mode);
        Assert.Equal((2, 2), (payload.Count, payload.Creates));
        Assert.Equal(UndoAction.Add, UndoActions.ForPlacement(payload.Mode));
        Assert.Equal(Str.Shell_Place_PlaceMany.Plural(2), PlacementHint.Verb(payload));

        var one = payload.Plan(0.3, 0.3, Screen, Sizes);
        var two = payload.Plan(0.3, 0.3, Screen, Sizes);

        // New widgets every time (the gallery is never used up), nothing taken away, the kinds as they stood.
        Assert.Empty(one.RemoveIds);
        Assert.DoesNotContain(one.Added, w => ids.Contains(w.Id));
        Assert.Empty(one.Added.Select(w => w.Id).Intersect(two.Added.Select(w => w.Id)));
        Assert.Equal(one.Added.Select(w => w.Id), one.Roots);
        Assert.Equal(gallery.Widgets.Select(w => w.Kind), one.Added.Select(w => w.Kind));
        Assert.Equal(2, gallery.Widgets.Count);

        var single = PlacementPayload.ForGallery(gallery, [ids[0]], 0.2, 0.2)!;
        Assert.Equal(Str.Shell_Place_PlaceOne.Format(("name", ModuleCatalog.Of(new WidgetId("clock")).Name)), PlacementHint.Verb(single));
        Assert.Null(PlacementPayload.ForGallery(gallery, ["nope"], 0.2, 0.2));
    }

    [Fact]
    public void S14916_a_style_picked_in_the_gallery_lands_with_the_widget()
    {
        var gallery = Gallery(("clock", new CellBox(20, 20, 26, 12)));
        var clock = gallery.Widgets[0];
        var flip = WidgetStyles.Find(clock.Kind, "flip")!.Value;
        Assert.True(WidgetStyles.Set(clock.Config, flip.Value));

        var landed = PlacementPayload.ForGallery(gallery, [clock.Id], 0.2, 0.2)!.Plan(0.4, 0.4, Screen, Sizes).Added.Single();

        Assert.Equal(flip.Value, WidgetStyles.Current(landed.Config));
    }
}
