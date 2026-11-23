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

        // [S-16723] No ready-made group while the gallery shows none (the one switch); the power buttons' aside ([S-16565]).
        var groups = categories.SelectMany(c => c.Pieces).Where(p => p.Group is not null).Select(p => p.Group!.Id).ToList();
        var offered = WidgetGallery.ShowsGroups ? ReadyMadeGroups.Offered().Where(g => !WidgetGallery.InQuickSettings.Contains(g.Home)).Select(g => g.Id) : [];
        Assert.Equal(offered.Order(StringComparer.Ordinal), groups.Order(StringComparer.Ordinal));

        // Every widget that stands alone on the add page (a subject's own), once, in its subject; no part of a family.
        var sections = page.Rail.Where(r => r.Id != AddFolderId.Start).SelectMany(r => page.Sections(r.Id).Select(s => (Subject: r.Id, Section: s))).ToList();
        var own = sections.Where(s => s.Section.Kind == AddSectionKind.Widgets).SelectMany(s => s.Section.Tiles.OfType<AddWidgetTile>().Select(t => (s.Subject, t.Kind))).ToList();
        var families = sections.Where(s => s.Section.Kind == AddSectionKind.Family).ToList();
        var parts = families.Where(s => !WidgetGallery.InQuickSettings.Contains(s.Section.Family!.Id)).SelectMany(s => s.Section.Tiles.OfType<AddWidgetTile>().Select(t => t.Kind)).ToHashSet();
        var power = families.Where(s => WidgetGallery.InQuickSettings.Contains(s.Section.Family!.Id)).SelectMany(s => s.Section.Tiles.OfType<AddWidgetTile>().Select(t => (Subject: AddFolderId.QuickSettings, t.Kind))).Distinct().ToList();
        var kinds = categories.SelectMany(c => c.Pieces.Where(p => p.Kind is not null && !p.Least && p.Style is null).Select(p => (Subject: c.Id, Kind: p.Kind!.Value))).ToList();
        Assert.Equal(own.Concat(power).OrderBy(o => o.Kind.Value, StringComparer.Ordinal), kinds.OrderBy(k => k.Kind.Value, StringComparer.Ordinal));
        Assert.NotEmpty(parts);
        Assert.DoesNotContain(kinds, k => parts.Contains(k.Kind));


        // Start is no category: it only shows again what is at home elsewhere.
        Assert.DoesNotContain(categories, c => c.Id == AddFolderId.Start);
        Assert.All(categories, c => Assert.NotEmpty(c.Pieces));
    }

    /// <summary>
    /// [S-16550]..[S-16553] [S-16740] The automatic bento: what searches find, and a tab with no curated layout yet (every tab
    /// of the build has one: "Your layout for tabs, auto for search"; the curated tabs are judged by their own rules,
    /// <see cref="S16743_every_tab_ships_a_curated_layout_that_lays_by_its_rules"/>). The owner (2026-11-23): "start with a column layout, the columns dont have to be all the same
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
    public void S16550_every_search_and_uncurated_category_of_the_build_is_a_column_bento_without_holes(int width, int height)
    {
        var (screen, view) = Display(width, height);
        var sizes = new GallerySizes(screen, 1, view);
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        var categories = WidgetGallery.Of(page);
        var shown = categories.Where(c => c.Sections is null && CuratedLayouts.Load(c.Id, 0) is null).Concat(new[] { "weather", "clock", "music", "cpu", "calendar", "a" }.Select(q => WidgetGallery.Find(page, categories, q)!)).ToList();
        var said = new List<string>();
        foreach (var category in shown)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var sheet = WidgetGallery.Lay(category, view, sizes.Of, sizes.TitleRows);
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
                var card = sheet.CardOf(tile);
                Assert.Equal(sizes.TitleRows, card.Y - tile.Box.Y);
                Assert.True(tile.Piece.Group is null || card.H == asked.H, $"{category.Id}: {tile.Piece.Key} is {card.H} tall, laid {asked.H}");
                Assert.True(tile.Box.W >= Math.Min(asked.MinW, view) && card.H >= asked.MinH, $"{category.Id}: {tile.Piece.Key} {tile.Box} under its least {asked.MinW}x{asked.MinH}");
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
            Assert.Empty(sheet.Headings);
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

    /// <summary>
    /// [S-16740]..[S-16746] [S-17220]..[S-17224] Every tab of the build ships its curated layout, read clean (no widget the
    /// build lacks, nothing repaired: every widget has its place), and laid on a 1920 by 1080 display and the small ones by the
    /// curated rules: every widget of the file once, in its column (no wider), on whole cells inside the view, its card at its
    /// place (from its column's left and its section's room) under its title where the tab has titles, no card on another and
    /// no title over a card; at 1920 by 1080 every place and size the one the file holds; every column its own blocks wide, two
    /// blocks apart on one line, inside the view at 1920 by 1080; on a narrow view the same (the sheet wider, scrolled across).
    /// </summary>
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1366, 768)]
    [InlineData(1280, 720)]
    [InlineData(1024, 768)]
    public void S16743_every_tab_ships_a_curated_layout_that_lays_by_its_rules(int width, int height)
    {
        var (screen, view) = Display(width, height);
        var sizes = new GallerySizes(screen, 1, view);
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        var categories = WidgetGallery.Of(page);
        Assert.Equal(categories.Select(c => c.Id).Order(), CuratedLayouts.Shipped().Order());
        foreach (var category in categories)
        {
            var layout = CuratedLayouts.Load(category.Id, sizes.TitleRows)!;
            Assert.NotNull(layout);
            using (var stream = typeof(CuratedLayouts).Assembly.GetManifestResourceStream("gallery-layouts/" + CuratedLayouts.FileName(category.Id))!)
            using (var reader = new StreamReader(stream))
            {
                Assert.Equal(reader.ReadToEnd().ReplaceLineEndings("\n"), CuratedLayouts.Write(layout));
            }

            var widgets = layout.Columns.Sum(c => c.Sections.Sum(s => s.Widgets.Count));
            Assert.True(widgets > 0, $"{category.Id}: an empty tab");
            var sheet = CuratedBento.Lay(category.Id, category.Title, layout, sizes.Curated, sizes.TitleRows, sizes.HeadingRows);
            Assert.Equal(widgets, sheet.Tiles.Count);

            // [S-17160] [S-17161] Every column exactly its blocks (never stretched, never narrowed), on one line two blocks
            // from its neighbours, the sheet as wide as they are; at 1920 by 1080 every tab fits the view (no scrolling across).
            Assert.Equal(layout.Columns.Select(c => c.Width), sheet.Columns.Select(c => c.W));
            Assert.All(sheet.Columns, c => Assert.Equal(0, c.Y));
            Assert.All(sheet.Columns.Skip(1).Zip(sheet.Columns), p => Assert.Equal(p.Second.Right + CuratedBento.ColumnGap, p.First.X));
            Assert.Equal(sheet.Columns[^1].Right, sheet.Cols);
            Assert.True(width < 1920 || sheet.Cols <= view, $"{category.Id}: {sheet.Cols} cells wide at {width} (view {view})");
            Assert.Equal(widgets, sheet.Tiles.Select(t => t.Slot).Distinct().Count());
            foreach (var tile in sheet.Tiles)
            {
                var slot = tile.Slot!.Value;
                var column = sheet.Columns[slot.Column];
                Assert.True(column.Holds(tile.Box) && tile.Box.X >= 0 && tile.Box.Right <= sheet.Cols, $"{category.Id}: {tile.Piece.Key} at {tile.Box} outside its column {column} or the sheet {sheet.Cols}");
                Assert.True(tile.Box.Y >= sheet.Areas.Single(a => a.Slot == slot with { Widget = -1 }).Box.Y);
                Assert.Equal(layout.Titles ? sizes.TitleRows : 0, sheet.CardOf(tile).Y - tile.Box.Y);
                // [S-17220] Its card at its place in its section; at 1920 by 1080 its size the file's too.
                var widget = layout.Columns[slot.Column].Sections[slot.Section].Widgets[slot.Widget];
                var area = sheet.Areas.Single(a => a.Slot == slot with { Widget = -1 });
                var card = sheet.CardOf(tile);
                Assert.True(card.X == column.X + Math.Min(widget.X!.Value, column.W - card.W) && card.Y == area.Room + widget.Y, $"{category.Id}: {tile.Piece.Key} at {card}, its place {widget.X},{widget.Y} (room {area.Room})");
                Assert.True(width != 1920 || (card.W, card.H) == (widget.W, widget.H), $"{category.Id}: {tile.Piece.Key} drawn {card.W}x{card.H}, its file {widget.W}x{widget.H}");
            }

            // [S-17222] No card on another, no title over a card, in any section.
            foreach (var area in sheet.Areas)
            {
                var cards = sheet.Tiles.Where(t => t.Slot!.Value with { Widget = -1 } == area.Slot).Select(t => sheet.CardOf(t) with { Y = sheet.CardOf(t).Y - area.Room }).ToList();
                Assert.False(CuratedBento.Clashes(cards, sheet.Title), $"{category.Id}: section {area.Slot} has a card on another or a title over one{Environment.NewLine}{Map(sheet, Math.Max(view, sheet.Cols))}");
            }
        }
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
    /// [S-16562] [S-16724]..[S-16726] The owner (2026-11-23): "all the widgets in the quick settings should be shown squared
    /// ... the other styles should also be shown ... you should just add headers to the columns or individual sections there
    /// ... order them as well so that the actions dont interweave with the toggle buttons". Quick settings is sections under
    /// headings: the toggles in their first style each at the size it has new and right after it at its least, then each other
    /// style once, then the power buttons in their order, each at its two sizes; every tile and round one square on the
    /// display's cells (to a cell); laid with no title over a card and no piece of one section among another's.
    /// </summary>
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1366, 768)]
    public void S16724_quick_settings_is_sections_of_square_toggles_by_style_then_the_power_buttons(int width, int height)
    {
        var (screen, view) = Display(width, height);
        var sizes = new GallerySizes(screen, 1, view);
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());
        var categories = WidgetGallery.Of(page);
        var quick = categories.Single(c => c.Id == AddFolderId.QuickSettings);
        var sections = quick.Sections!;
        var toggles = page.Sections(AddFolderId.QuickSettings).Where(s => s.Kind == AddSectionKind.Widgets).SelectMany(s => s.Tiles.OfType<AddWidgetTile>()).Select(t => t.Pick).ToList();
        var styles = WidgetStyles.For(toggles[0].Kind);

        // [S-16685] Each toggle target is a piece of its own: the presets of the one Quick setting kind, every one listed.
        Assert.True(toggles.Count >= 13 && toggles.Distinct().Count() == toggles.Count, $"{toggles.Count} toggles: {string.Join(", ", toggles)}");
        Assert.True(styles.Count >= 2);
        Assert.Equal(styles.Count + 1, sections.Count);
        Assert.Equal([.. styles.Select(o => o.Label.Text()), Str.AddMenu_Family_PowerButtons.Text()], sections.Select(s => s.Heading.Text()));
        Assert.Equal(sections.SelectMany(s => s.Pieces), quick.Pieces);

        // The first style: each toggle at its size and right after it at its least, smaller.
        var first = sections[0].Pieces;
        Assert.Equal(toggles.SelectMany(k => new[] { k, k }), first.Select(p => p.Pick!.Value));
        for (var i = 0; i < first.Count; i += 2)
        {
            Assert.True(!first[i].Least && first[i + 1].Least && first[i + 1].Key == "least:" + first[i].Key);
            var (asked, small) = (sizes.Of(first[i]), sizes.Of(first[i + 1]));
            Assert.True(small.W <= asked.W && small.H <= asked.H && (small.W, small.H) != (asked.W, asked.H), $"{first[i].Key}: {small.W}x{small.H} beside {asked.W}x{asked.H}");
            Assert.False(asked.Wider || asked.Taller || small.Wider || small.Taller);
        }

        // Each other style once, in it.
        for (var s = 1; s < styles.Count; s++)
        {
            Assert.Equal(toggles, sections[s].Pieces.Select(p => p.Pick!.Value));
            Assert.All(sections[s].Pieces, p => Assert.Equal(styles[s].Value, p.Style));
        }

        // The power buttons last, lock first and the screen off last, each at its two sizes.
        var power = sections[^1].Pieces;
        var order = power.Where(p => !p.Least).Select(p => p.Kind!.Value.Value).ToList();
        Assert.Equal(WidgetGallery.PowerOrder.Where(order.Contains), order);
        Assert.Equal("lock", order[0]);
        Assert.Equal([false, true], power.Take(2).Select(p => p.Least));

        // Square on the display's cells: as many DIPs down as across, to a cell; a switch keeps its height.
        var (cols, rows) = GridGeometry.GridDims(screen);
        var (pw, ph) = (screen.Width / cols, screen.Height / rows);
        foreach (var piece in quick.Pieces.Where(p => p.Style is null || styles.First(o => o.Value == p.Style).Name != "switch"))
        {
            var asked = sizes.Of(piece);
            Assert.True(Math.Abs((asked.W * pw) - (asked.H * ph)) <= Math.Max(pw, ph) + 1e-9, $"{piece.Key}: {asked.W}x{asked.H} cells is {asked.W * pw}x{asked.H * ph} DIPs");
        }

        // A switch keeps its line, wider than tall (regression: squared, its line stood in the middle of a tall card).
        foreach (var piece in quick.Pieces.Where(p => p.Style is { } style && styles.First(o => o.Value == style).Name == "switch"))
        {
            var asked = sizes.Of(piece);
            Assert.True(asked.W * pw > 2 * asked.H * ph, $"{piece.Key}: {asked.W}x{asked.H} cells");
        }

        // Laid: no titles, the headings over their sections, no piece of one section among another's.
        var sheet = WidgetGallery.Lay(quick, view, sizes.Of, sizes.TitleRows, sizes.HeadingRows);
        Assert.Equal(0, sheet.Title);
        Assert.Equal(sections.Count, sheet.Headings.Count);
        var at = 0;
        for (var s = 0; s < sections.Count; s++)
        {
            var top = sheet.Headings[s].Box.Bottom;
            var next = s + 1 < sections.Count ? sheet.Headings[s + 1].Box.Y : int.MaxValue;
            foreach (var tile in sheet.Tiles.Skip(at).Take(sections[s].Pieces.Count))
            {
                Assert.True(tile.Box.Y >= top && tile.Box.Bottom < next && tile.Box.Right <= view, $"{tile.Piece.Key} at {tile.Box}");
                Assert.Equal((sizes.Of(tile.Piece).W, sizes.Of(tile.Piece).H), (tile.Box.W, tile.Box.H));
            }

            at += sections[s].Pieces.Count;
        }

        Assert.DoesNotContain(categories.Where(c => c.Id != AddFolderId.QuickSettings).SelectMany(c => c.Pieces), p => p.Least || p.Square);
        Assert.DoesNotContain(categories.SelectMany(c => c.Pieces), p => p.Group is not null);
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
