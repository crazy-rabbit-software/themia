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

        // Every ready-made group this build places, once; the power buttons' aside ([S-16565]: they stand alone in Quick settings).
        var groups = categories.SelectMany(c => c.Pieces).Where(p => p.Group is not null).Select(p => p.Group!.Id).ToList();
        Assert.Equal(ReadyMadeGroups.Offered().Where(g => !WidgetGallery.InQuickSettings.Contains(g.Home)).Select(g => g.Id).Order(StringComparer.Ordinal), groups.Order(StringComparer.Ordinal));

        // Every widget that stands alone on the add page (a subject's own), once, in its subject; no part of a family.
        var sections = page.Rail.Where(r => r.Id != AddFolderId.Start).SelectMany(r => page.Sections(r.Id).Select(s => (Subject: r.Id, Section: s))).ToList();
        var own = sections.Where(s => s.Section.Kind == AddSectionKind.Widgets).SelectMany(s => s.Section.Tiles.OfType<AddWidgetTile>().Select(t => (s.Subject, t.Kind))).ToList();
        var families = sections.Where(s => s.Section.Kind == AddSectionKind.Family).ToList();
        var parts = families.Where(s => !WidgetGallery.InQuickSettings.Contains(s.Section.Family!.Id)).SelectMany(s => s.Section.Tiles.OfType<AddWidgetTile>().Select(t => t.Kind)).ToHashSet();
        var power = families.Where(s => WidgetGallery.InQuickSettings.Contains(s.Section.Family!.Id)).SelectMany(s => s.Section.Tiles.OfType<AddWidgetTile>().Select(t => (Subject: AddFolderId.QuickSettings, t.Kind))).Distinct().ToList();
        var kinds = categories.SelectMany(c => c.Pieces.Where(p => p.Kind is not null && !p.Least).Select(p => (Subject: c.Id, Kind: p.Kind!.Value))).ToList();
        Assert.Equal(own.Concat(power).OrderBy(o => o.Kind.Value, StringComparer.Ordinal), kinds.OrderBy(k => k.Kind.Value, StringComparer.Ordinal));
        Assert.NotEmpty(parts);
        Assert.DoesNotContain(kinds, k => parts.Contains(k.Kind));

        // The ready-made groups are pieces of their own.
        Assert.Contains("nowPlaying", groups);

        // Start is no category: it only shows again what is at home elsewhere.
        Assert.DoesNotContain(categories, c => c.Id == AddFolderId.Start);
        Assert.All(categories, c => Assert.NotEmpty(c.Pieces));
    }

    /// <summary>
    /// [S-16550]..[S-16553] The owner (2026-11-23): "start with a column layout, the columns dont have to be all the same
    /// width, then place all the widgets in them ... the widgets size should be defined by the columns". Every category of the
    /// build, and what searches find, laid as the gallery lays them on a 1920 by 1080 display and the small ones (1366 by 768,
    /// 1280 by 720, 1024 by 768): on whole cells inside the view, one cell between every two pieces, none under its least, a
    /// group only at a width its step lands on, every piece in a column as wide as it (or side by side at one height filling
    /// it), no hole in a column, and the columns' feet even (within a few rows; a category of groups, whose heights are kept,
    /// within a group's height).
    /// </summary>
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1366, 768)]
    [InlineData(1280, 720)]
    [InlineData(1024, 768)]
    public void S16550_every_category_and_search_of_the_build_is_a_column_bento_without_holes(int width, int height)
    {
        var (screen, view) = Display(width, height);
        var sizes = new GallerySizes(screen, 1, view);
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        var categories = WidgetGallery.Of(page);
        var shown = categories.Concat(new[] { "weather", "clock", "music", "cpu", "calendar", "a" }.Select(q => WidgetGallery.Find(page, categories, q)!)).ToList();
        var said = new List<string>();
        foreach (var category in shown)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var sheet = WidgetGallery.Lay(category, view, sizes.Of);
            var ms = clock.Elapsed.TotalMilliseconds;
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
                Assert.Contains(sheet.Columns, c => c.Holds(tile.Box));
            }

            // Every column's rows fill it across, one height a row; no hole in a column; the feet as even as the pieces let them.
            foreach (var col in sheet.Columns)
            {
                foreach (var row in sheet.Tiles.Where(t => col.Holds(t.Box)).GroupBy(t => t.Box.Y))
                {
                    Assert.Single(row.Select(t => t.Box.H).Distinct());
                    Assert.True(col.W == row.Sum(t => t.Box.W) + ((row.Count() - 1) * WidgetGallery.Gap), $"{category.Id}: a row of {string.Join(", ", row.Select(t => t.Piece.Key))} at {row.Key} does not fill its column {col}{Environment.NewLine}{Map(sheet, view)}");
                }
            }

            var (holes, ragged) = (WidgetGallery.Holes(sheet), WidgetGallery.Ragged(sheet));
            var tallestKept = sheet.Tiles.Where(t => !sizes.Of(t.Piece).Taller).Select(t => t.Box.H + WidgetGallery.Gap).DefaultIfEmpty(0).Max();
            said.Add($"{category.Id}/{category.Pieces.Count}: {sheet.Columns.Count} columns ({string.Join("+", sheet.Columns.Select(c => c.W))}), {sheet.Rows} rows, ragged {ragged}, {ms:0.0} ms");
            Assert.True(holes == 0, $"{category.Id}: {holes} cells of its columns empty{Environment.NewLine}{Map(sheet, view)}");
            Assert.True(ragged <= Math.Max(Uneven, tallestKept), $"{category.Id}: its columns' feet {ragged} rows apart{Environment.NewLine}{Map(sheet, view)}");
            // Across, the columns that start at the sheet's top (one may go on as two under its last card) fit the view.
            var top = sheet.Columns.Where(c => c.Y == 0).ToList();
            Assert.True(top.Sum(c => c.W) + ((top.Count - 1) * WidgetGallery.Gap) <= view, $"{category.Id}: columns {string.Join(" ", sheet.Columns)}");
            Assert.All(sheet.Columns.Where(c => c.Y > 0), c => Assert.Contains(top, t => t.X <= c.X && t.Right >= c.Right && t.Bottom + WidgetGallery.Gap == c.Y));
        }

        File.WriteAllLines(Path.Combine(Path.GetTempPath(), $"themia-gallery-columns-{width}x{height}.txt"), said);
    }

    /// <summary>[S-16552] How far apart the columns' feet may end where every piece stretches: a few rows (a piece's height steps).</summary>
    private const int Uneven = 8;

    /// <summary>A stand-in display of <paramref name="width"/> by <paramref name="height"/> DIPs, its taskbar 48 at the foot, and the gallery's view on it in cells.</summary>
    private static (ScreenMetrics Screen, int View) Display(int width, int height)
    {
        var screen = new ScreenMetrics(width, height) { Allowed = new FracRect(0, 0, 1, (height - 48) / (double)height) };
        var (cols, rows) = GridGeometry.GridDims(screen);
        var work = new DipRect(0, 0, width, height - 48);
        return (screen, WidgetGallery.Frame(work, 56, work, width / (double)cols, height / (double)rows).View.W);
    }

    /// <summary>[S-16561] A smaller display has a narrower view and fewer columns, never a piece cut.</summary>
    [Fact]
    public void S16561_a_small_display_lays_fewer_columns()
    {
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        var categories = WidgetGallery.Of(page);
        var (large, wide) = Display(1920, 1080);
        var (small, narrow) = Display(1024, 768);
        Assert.True(narrow < wide);
        var time = categories.First(c => c.Id == AddFolderId.Time);
        var big = WidgetGallery.Lay(time, wide, new GallerySizes(large, 1, wide).Of);
        var little = WidgetGallery.Lay(time, narrow, new GallerySizes(small, 1, narrow).Of);
        Assert.True(little.Columns.Count < big.Columns.Count, $"{little.Columns.Count} columns at 1024, {big.Columns.Count} at 1920");
        Assert.All(little.Tiles, t => Assert.True(t.Box.Right <= narrow));
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
    /// [S-16402] [S-16557] What a search finds is the add page's search kept to the gallery's whole pieces: best first, each
    /// once, no family's small part, the first <see cref="WidgetGallery.MostFound"/> at the most; nothing typed, nothing found.
    /// </summary>
    [Fact]
    public void S16557_a_search_finds_the_first_whole_pieces_as_the_add_page_ranks_them()
    {
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        var categories = WidgetGallery.Of(page);
        var whole = categories.SelectMany(c => c.Pieces).Where(p => !p.Least).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

        Assert.Null(WidgetGallery.Find(page, categories, null));
        Assert.Null(WidgetGallery.Find(page, categories, "   "));

        var weather = WidgetGallery.Find(page, categories, "weather")!;
        Assert.NotEmpty(weather.Pieces);
        Assert.All(weather.Pieces, p => Assert.Contains(p.Key, whole));
        Assert.Equal(weather.Pieces.Count, weather.Pieces.Select(p => p.Key).Distinct().Count());
        var ranked = page.Find("weather").SelectMany(s => s.Tiles).Select(t => t.Key).Where(whole.Contains).Distinct().ToList();
        Assert.Equal(ranked.Take(WidgetGallery.MostFound), weather.Pieces.Select(p => p.Key));

        // A word most things hold: the first eight, best first.
        var many = page.Find("e").SelectMany(s => s.Tiles).Select(t => t.Key).Where(whole.Contains).Distinct().ToList();
        Assert.True(many.Count > WidgetGallery.MostFound, $"\"e\" finds {many.Count}");
        Assert.Equal(many.Take(WidgetGallery.MostFound), WidgetGallery.Find(page, categories, "e")!.Pieces.Select(p => p.Key));
        Assert.Equal(3, WidgetGallery.Find(page, categories, "e", most: 3)!.Pieces.Count);

        Assert.Empty(WidgetGallery.Find(page, categories, "qqqqzzzz")!.Pieces);
    }

    /// <summary>
    /// [S-16562] The owner (2026-11-23): "the quick settings should show in two sizes, the small size that we have right now
    /// and then even a smaller size". Every toggle of the Quick settings tab is there at the size it has new and right after it
    /// at its least, both whole widgets that drag out at that size; no other tab doubles its widgets.
    /// </summary>
    [Fact]
    public void S16562_the_quick_settings_show_every_toggle_at_its_size_and_at_its_least()
    {
        var (screen, view) = Display(1920, 1080);
        var sizes = new GallerySizes(screen, 1, view);
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        var categories = WidgetGallery.Of(page);
        var quick = categories.Single(c => c.Id == AddFolderId.QuickSettings);

        var widgets = quick.Pieces.Where(p => p.Kind is not null && !p.Least).ToList();
        Assert.NotEmpty(widgets);
        foreach (var widget in widgets)
        {
            var at = quick.Pieces.ToList().IndexOf(widget);
            var least = quick.Pieces[at + 1];
            Assert.True(least.Least && least.Kind == widget.Kind && least.Key == "least:" + widget.Key);
            var (asked, small) = (sizes.Of(widget), sizes.Of(least));
            Assert.True(small.W <= asked.W && small.H <= asked.H && (small.W, small.H) != (asked.W, asked.H), $"{widget.Key}: {small.W}x{small.H} is not smaller than {asked.W}x{asked.H}");
            Assert.False(small.Wider || small.Taller);
            var limits = SizeFit.Limits(SizeFit.Of(widget.Kind!.Value, null), screen, 1)!;
            var (cols, rows) = GridGeometry.GridDims(screen);
            Assert.Equal(((int)Math.Ceiling((limits.Width.Min * cols) - 1e-6), (int)Math.Ceiling((limits.Height.Min * rows) - 1e-6)), (small.W, small.H));
        }

        Assert.DoesNotContain(categories.Where(c => c.Id != AddFolderId.QuickSettings).SelectMany(c => c.Pieces), p => p.Least);

        // Laid, each least one stands at its least.
        var sheet = WidgetGallery.Lay(quick, view, sizes.Of);
        Assert.All(sheet.Tiles.Where(t => t.Piece.Least), t => Assert.Equal((sizes.Of(t.Piece).W, sizes.Of(t.Piece).H), (t.Box.W, t.Box.H)));
    }

    /// <summary>
    /// [S-16565] The owner (2026-11-23): "the power widgets like restart shutdown etc, should be shown without the header bar and
    /// should be shown in two different sizes. the same sizes that the quick settings are shown"; "the power actions should be
    /// moved to the quick settings page". Every power button stands alone in Quick settings, at its size and at its least,
    /// nowhere else; the ready-made Power group (a Header over the buttons) is not in the gallery.
    /// </summary>
    [Fact]
    public void S16565_the_power_buttons_stand_alone_in_quick_settings_at_two_sizes()
    {
        var (screen, view) = Display(1920, 1080);
        var sizes = new GallerySizes(screen, 1, view);
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        var categories = WidgetGallery.Of(page);
        var family = page.Rail.SelectMany(r => page.Sections(r.Id)).First(s => s.Family?.Id == AddFolderId.PowerButtons).Tiles.OfType<AddWidgetTile>().Select(t => t.Kind).ToList();
        Assert.Contains(new WidgetId("shutdown"), family);
        Assert.Contains(new WidgetId("restart"), family);

        var quick = categories.Single(c => c.Id == AddFolderId.QuickSettings);
        foreach (var kind in family)
        {
            var two = quick.Pieces.Where(p => p.Kind == kind).ToList();
            Assert.Equal([false, true], two.Select(p => p.Least));
            Assert.All(two, p => Assert.Null(p.Group));
            var (natural, least) = (sizes.Of(two[0]), sizes.Of(two[1]));
            Assert.True(least.W <= natural.W && least.H <= natural.H && (least.W, least.H) != (natural.W, natural.H), $"{kind}: {least.W}x{least.H} beside {natural.W}x{natural.H}");
        }

        Assert.DoesNotContain(categories.Where(c => c.Id != AddFolderId.QuickSettings).SelectMany(c => c.Pieces), p => p.Kind is { } k && family.Contains(k));
        Assert.DoesNotContain(categories.SelectMany(c => c.Pieces), p => p.Group?.Home == AddFolderId.PowerButtons);
        Assert.Contains(categories, c => c.Id == AddFolderId.Devices);
        Assert.Equal(0, WidgetGallery.Holes(WidgetGallery.Lay(quick, view, sizes.Of)));
    }

    /// <summary>[S-16554] [S-16555] The clock of the build in focus on the 1920 by 1080 display: where it stood, its words and every other style round it, nothing else.</summary>
    [Fact]
    public void S16554_the_clock_in_focus_stays_where_it_stood_its_styles_round_it()
    {
        var (screen, view) = Display(1920, 1080);
        var sizes = new GallerySizes(screen, 1, view);
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        var time = WidgetGallery.Of(page).First(c => c.Pieces.Any(p => p.Kind?.Value == "clock"));
        var laid = WidgetGallery.Lay(time, view, sizes.Of);
        var at = laid.Tiles.First(t => t.Piece.Kind?.Value == "clock");
        var styles = WidgetStyles.For(at.Piece.Kind!.Value).Select(o => o.Value).Skip(1).ToList();

        var focus = WidgetGallery.Focus(time, at.Piece, styles);
        var sheet = WidgetGallery.LayFocus(focus, at.Box, view, laid.Rows, sizes.Of);

        Assert.Equal(at.Box, sheet.Tiles[0].Box);
        Assert.Equal(styles.Count, sheet.Tiles.Count(t => t.Piece.Role == GalleryRole.Style));
        Assert.Single(sheet.Tiles, t => t.Piece.Role == GalleryRole.About);
        Assert.Equal(styles.Count + 2, sheet.Tiles.Count);
        Assert.True(sheet.Rows >= laid.Rows);
        for (var i = 0; i < sheet.Tiles.Count; i++)
        {
            Assert.True(sheet.Tiles[i].Box.Right <= view, $"{sheet.Tiles[i].Piece.Key} at {sheet.Tiles[i].Box}");
            for (var j = i + 1; j < sheet.Tiles.Count; j++)
            {
                var (a, b) = (sheet.Tiles[i].Box, sheet.Tiles[j].Box);
                Assert.False(new CellBox(a.X - 1, a.Y - 1, a.W + 2, a.H + 2).Overlaps(b), $"{sheet.Tiles[i].Piece.Key} {a} and {sheet.Tiles[j].Piece.Key} {b}");
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
