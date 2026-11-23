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

    /// <summary>
    /// [S-16403] The owner (2026-11-23): "make sure that in the widget gallery that all the widgets are properly placed on a
    /// bento grid and there are no major spaces between them. (except for the one block space between separate groups of
    /// widgets)". Every category of the build, and what searches find, laid as the gallery lays them on a 1920 by 1080
    /// display and a 1280 by 720 one: on whole cells inside the view, one cell between every two pieces, none under its
    /// least, a group only at a width its step lands on, every piece grown as far as it goes (<see cref="WidgetGallery.Grows"/>:
    /// what room is left no piece could take) and at most one in twenty-five of the sheet's cells empty between pieces.
    /// </summary>
    [Theory]
    [InlineData(1920, 1080, true)]
    [InlineData(1280, 720, true)]
    public void S16403_every_category_and_search_of_the_build_is_a_bento_without_holes(int width, int height, bool strict)
    {
        var (screen, view) = Display(width, height);
        var sizes = new GallerySizes(screen, 1, view);
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        var categories = WidgetGallery.Of(page);
        var shown = categories.Concat(new[] { "weather", "clock", "music", "cpu", "calendar" }.Select(q => WidgetGallery.Find(page, categories, q)!)).ToList();
        foreach (var category in shown)
        {
            var sheet = WidgetGallery.Lay(category, view, sizes.Of);
            Assert.Equal(category.Pieces.Count, sheet.Tiles.Count);
            var room = new CellBox(0, 0, view, sheet.Rows);
            for (var i = 0; i < sheet.Tiles.Count; i++)
            {
                var tile = sheet.Tiles[i];
                Assert.True(room.Holds(tile.Box), $"{category.Id}: {tile.Piece.Key} at {tile.Box}");
                for (var j = i + 1; j < sheet.Tiles.Count; j++)
                {
                    var other = sheet.Tiles[j].Box;
                    var apart = new CellBox(tile.Box.X - WidgetGallery.Gap, tile.Box.Y - WidgetGallery.Gap, tile.Box.W + (2 * WidgetGallery.Gap), tile.Box.H + (2 * WidgetGallery.Gap));
                    Assert.False(apart.Overlaps(other), $"{category.Id}: {tile.Piece.Key} and {sheet.Tiles[j].Piece.Key} share cells or touch");
                }

                var asked = sizes.Of(tile.Piece);
                Assert.True(tile.Piece.Group is null || tile.Box.H == asked.H, $"{category.Id}: {tile.Piece.Key} is {tile.Box.H} tall, laid {asked.H}");
                Assert.True(tile.Box.W >= Math.Min(asked.MinW, view) && tile.Box.H >= asked.MinH, $"{category.Id}: {tile.Piece.Key} {tile.Box} under its least {asked.MinW}x{asked.MinH}");
                Assert.True(asked.Widths is null || asked.Widths.Contains(tile.Box.W) || (asked.W > view && tile.Box.W == view),
                    $"{category.Id}: group {tile.Piece.Key} made {tile.Box.W} wide, not a width its step lands on");
            }

            // Every piece grown as far as it goes (what room is left no piece could take), and few cells between pieces empty.
            var (holes, thickest) = (WidgetGallery.Holes(sheet, view), WidgetGallery.Thickest(sheet, view));
            var grows = WidgetGallery.Grows(sheet, view, sizes.Of);
            // Ready-made groups keep their height and take only the widths their step lands on: a sheet of many leaves slivers.
            Assert.True(grows is null && (strict is false || holes * 25 <= (view + 1) * (sheet.Rows + 1)),
                $"{category.Id} ({category.Title.Text()}): {holes} cells between pieces empty, {thickest} thick; {grows?.Piece.Key} could grow{Environment.NewLine}{Map(sheet, view)}");
        }
    }

    /// <summary>A stand-in display of <paramref name="width"/> by <paramref name="height"/> DIPs, its taskbar 48 at the foot, and the gallery's view on it in cells.</summary>
    private static (ScreenMetrics Screen, int View) Display(int width, int height)
    {
        var screen = new ScreenMetrics(width, height) { Allowed = new FracRect(0, 0, 1, (height - 48) / (double)height) };
        var (cols, rows) = GridGeometry.GridDims(screen);
        var work = new DipRect(0, 0, width, height - 48);
        return (screen, WidgetGallery.Frame(work, 56, work, width / (double)cols, height / (double)rows).View.W);
    }

    /// <summary>A sheet drawn in letters, for a failure's message.</summary>
    private static string Map(GallerySheet sheet, int width)
    {
        var rows = Enumerable.Range(0, sheet.Rows + 1).Select(_ => Enumerable.Repeat('.', width + 1).ToArray()).ToList();
        for (var i = 0; i < sheet.Tiles.Count; i++)
        {
            var box = sheet.Tiles[i].Box;
            for (var y = box.Y; y < box.Bottom; y++)
            {
                for (var x = box.X; x < box.Right; x++)
                {
                    rows[y][x] = (char)('A' + (i % 26));
                }
            }
        }

        return string.Join(Environment.NewLine, rows.Select(r => new string(r)));
    }

    /// <summary>
    /// [S-16402] What a search finds is the add page's search kept to the gallery's whole pieces: best first, each once, no
    /// family's small part; nothing typed, nothing found.
    /// </summary>
    [Fact]
    public void S16402_a_search_finds_whole_pieces_as_the_add_page_ranks_them()
    {
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        var categories = WidgetGallery.Of(page);
        var whole = categories.SelectMany(c => c.Pieces).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

        Assert.Null(WidgetGallery.Find(page, categories, null));
        Assert.Null(WidgetGallery.Find(page, categories, "   "));

        var weather = WidgetGallery.Find(page, categories, "weather")!;
        Assert.NotEmpty(weather.Pieces);
        Assert.All(weather.Pieces, p => Assert.Contains(p.Key, whole));
        Assert.Equal(weather.Pieces.Count, weather.Pieces.Select(p => p.Key).Distinct().Count());
        var ranked = page.Find("weather").SelectMany(s => s.Tiles).Select(t => t.Key).Where(whole.Contains).Distinct().ToList();
        Assert.Equal(ranked, weather.Pieces.Select(p => p.Key));

        Assert.Empty(WidgetGallery.Find(page, categories, "qqqqzzzz")!.Pieces);
    }

    /// <summary>[S-16404] [S-16405] The clock of the build in focus on the 1920 by 1080 display: in the middle at the top, its styles round it, no hole.</summary>
    [Fact]
    public void S16405_the_clock_in_focus_stands_in_the_middle_its_styles_and_neighbours_round_it()
    {
        var (screen, view) = Display(1920, 1080);
        var sizes = new GallerySizes(screen, 1, view);
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        var time = WidgetGallery.Of(page).First(c => c.Pieces.Any(p => p.Kind?.Value == "clock"));
        var clock = time.Pieces.First(p => p.Kind?.Value == "clock");
        var styles = WidgetStyles.For(clock.Kind!.Value).Select(o => o.Value).Skip(1).ToList();

        var focus = WidgetGallery.Focus(time, clock, styles, sizes.Resizes(clock.Kind!.Value));
        var sheet = WidgetGallery.LayFocus(focus, view, sizes.Of);

        var middle = sheet.Tiles[0].Box;
        Assert.Equal(0, middle.Y);
        Assert.InRange(middle.X + (middle.W / 2.0), (view / 2.0) - 1, (view / 2.0) + 1);
        Assert.Equal(sizes.Cells(clock.Kind!.Value, null), (middle.W, middle.H));
        Assert.Equal(Math.Min(styles.Count, WidgetGallery.MostAround - WidgetGallery.FewestSimilar), sheet.Tiles.Count(t => t.Piece.Role == GalleryRole.Style));
        Assert.Null(WidgetGallery.Grows(sheet, view, sizes.Of));
        Assert.True(WidgetGallery.Holes(sheet, view) * 50 <= (view + 1) * (sheet.Rows + 1), Map(sheet, view));
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
