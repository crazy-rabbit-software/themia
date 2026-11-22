using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14912] [S-14916] The widget gallery over every whole widget and ready-made group of the build, and its drag as the
/// one placement session. Here, not in Core's tests, because the gallery is built from every widget and a new widget's
/// box from its sizing.
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

    /// <summary>What a piece asks of the bento on <see cref="Screen"/>, as the gallery's view asks it.</summary>
    private static PieceSize Size(GalleryPiece piece)
    {
        var (cols, rows) = GridGeometry.GridDims(Screen);
        if (piece.Group is { } group)
        {
            var laid = ReadyMadeGroups.Laid(group, Screen, Sizes);
            var (w, h) = ((int)Math.Round(laid.Max(r => r.Right) * cols), (int)Math.Round(laid.Max(r => r.Bottom) * rows));
            return new PieceSize(w, h, w, h, true, false);
        }

        var kind = piece.Kind!.Value;
        var (nw, nh) = Cells(kind);
        var limits = SizeFit.Limits(SizeFit.Of(kind), Screen, Sizes.Scale)!;
        return new PieceSize(nw, nh, (int)Math.Ceiling((limits.Width.Min * cols) - 1e-6), (int)Math.Ceiling((limits.Height.Min * rows) - 1e-6), !limits.Width.Fixed, !limits.Height.Fixed);
    }

    [Fact]
    public void S14912_every_whole_widget_and_ready_made_group_stands_once_in_its_subject_and_no_familys_part()
    {
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        var categories = WidgetGallery.Of(page);

        // Every ready-made group this build places, once.
        var groups = categories.SelectMany(c => c.Pieces).Where(p => p.Group is not null).Select(p => p.Group!.Id).ToList();
        Assert.Equal(ReadyMadeGroups.Offered().Select(g => g.Id).Order(StringComparer.Ordinal), groups.Order(StringComparer.Ordinal));

        // Every widget that stands alone on the add page (a subject's own), once, in its subject; no part of a family.
        var sections = page.Rail.Where(r => r.Id != AddFolderId.Start).SelectMany(r => page.Sections(r.Id).Select(s => (Subject: r.Id, Section: s))).ToList();
        var own = sections.Where(s => s.Section.Kind == AddSectionKind.Widgets).SelectMany(s => s.Section.Tiles.OfType<AddWidgetTile>().Select(t => (s.Subject, t.Kind))).ToList();
        var parts = sections.Where(s => s.Section.Kind == AddSectionKind.Family).SelectMany(s => s.Section.Tiles.OfType<AddWidgetTile>().Select(t => t.Kind)).ToHashSet();
        var kinds = categories.SelectMany(c => c.Pieces.Where(p => p.Kind is not null).Select(p => (Subject: c.Id, Kind: p.Kind!.Value))).ToList();
        Assert.Equal(own.OrderBy(o => o.Kind.Value, StringComparer.Ordinal), kinds.OrderBy(k => k.Kind.Value, StringComparer.Ordinal));
        Assert.NotEmpty(parts);
        Assert.DoesNotContain(kinds, k => parts.Contains(k.Kind));

        // The ready-made groups are pieces of their own.
        Assert.Contains("nowPlaying", groups);

        // Start is no category: it only shows again what is at home elsewhere.
        Assert.DoesNotContain(categories, c => c.Id == AddFolderId.Start);
        Assert.All(categories, c => Assert.NotEmpty(c.Pieces));
    }

    [Fact]
    public void S14912_every_category_of_the_build_is_a_tidy_bento_its_pieces_never_under_their_least()
    {
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        const int width = (12 * WidgetGallery.Pitch) - WidgetGallery.Gap;
        foreach (var category in WidgetGallery.Of(page))
        {
            var sheet = WidgetGallery.Lay(category, width, Size);
            Assert.Equal(category.Pieces.Count, sheet.Tiles.Count);
            var room = new CellBox(0, 0, width, sheet.Rows);
            for (var i = 0; i < sheet.Tiles.Count; i++)
            {
                var tile = sheet.Tiles[i];
                Assert.True(room.Holds(tile.Tile), $"{category.Id}: {tile.Piece.Key} at {tile.Tile}");
                Assert.True(tile.Tile.Holds(tile.Box), $"{category.Id}: {tile.Piece.Key}'s box {tile.Box} in {tile.Tile}");
                for (var j = i + 1; j < sheet.Tiles.Count; j++)
                {
                    var other = sheet.Tiles[j].Tile;
                    var apart = new CellBox(tile.Tile.X - WidgetGallery.Gap, tile.Tile.Y - WidgetGallery.Gap, tile.Tile.W + (2 * WidgetGallery.Gap), tile.Tile.H + (2 * WidgetGallery.Gap));
                    Assert.False(apart.Overlaps(other), $"{category.Id}: {tile.Piece.Key} and {sheet.Tiles[j].Piece.Key} share cells or touch");
                }

                // Never drawn under its least: a widget keeps what its sizing asks, a group the size it is laid at.
                var asked = Size(tile.Piece);
                Assert.True(tile.Piece.Group is null || tile.Box.H == asked.H, $"{category.Id}: {tile.Piece.Key} is {tile.Box.H} tall, laid {asked.H}");
                Assert.True(tile.Box.W >= Math.Min(asked.MinW, width) && tile.Box.H >= asked.MinH, $"{category.Id}: {tile.Piece.Key} {tile.Box} under its least {asked.MinW}x{asked.MinH}");
            }
        }
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
