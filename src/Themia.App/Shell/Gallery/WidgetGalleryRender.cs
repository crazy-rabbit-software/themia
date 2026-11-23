#if DEBUG
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-14917] Debug only: the widget gallery through the app's own wiring, a real <see cref="AppHost"/> over a detached store
/// and a real <see cref="DesktopWindow"/> on a stand-in 1920 × 1080 display (its taskbar 48 at the foot), off-screen, in edit
/// mode with the gallery opened from the bar's path.
/// <c>Themia.exe --render gallery &lt;outDir&gt; [--theme light|dark] [--category Id]</c> draws every category's bento (or the
/// one's; one taller than the well again at its foot), two searches, a widget with styles, one without and a ready-made group
/// in focus, several picked (the frame round them) and a drag under way (<c>gallery-&lt;theme&gt;-…png</c>).
/// <c>Themia.exe --check gallery &lt;out.txt&gt;</c> checks the window under the bar, every bento's cells and holes, a search, the
/// focus and its return, the keys, and drives the drag as its pointer does: one widget and then three picked, dragged and
/// dropped, a drag given up and a style shown in focus dragged; it says whether the ghost started on the cells they stood on
/// (no jump), moved by whole cells, landed where it was, selected, and whether the gallery closed for their settings page;
/// PASS or FAIL.
/// </summary>
internal static class WidgetGalleryRender
{
    private const string DisplayId = "gallery-render-display";

    public static RenderCommand Command { get; } = new(
        RenderVerb.Render,
        "gallery",
        "<outDir> [--theme light|dark] [--category Id] [--width w --height h] [--raster 100]",
        """
        the widget gallery through a real host and surface in edit mode: every tab (or --category's), searches, a widget with
        styles, one without and a group in focus, several picked, a drag under way (gallery-<theme>-<w>x<h>-*.png) [S-14917]
        [S-16410]; --width/--height the stand-in display's pixels (1920 × 1080), --raster 100 at 100 %
        """,
        run => DrawAsync(run, run.PathArg(0), run.App.RequestedTheme != ApplicationTheme.Light, Category(run), Display(run)));

    public static RenderCommand Check { get; } = new(
        RenderVerb.Check,
        "gallery",
        "<out.txt> [--width w --height h] [--raster 100]",
        """
        the widget gallery under the bar, its bentos (cells, holes), a search, the focus and its return, the keys, and its drag
        driven as its pointer does: one widget and three dragged (no jump, whole cells) and dropped (selected, their settings
        page open, the gallery closed), a drag given up, a style dragged from a focus; PASS or FAIL [S-14917] [S-16410]
        """,
        run => CheckAsync(run, run.PathArg(0), Display(run)));

    /// <summary>--category's screens only, or every one (a name it does not know is said and ignored).</summary>
    private static AddFolderId? Category(RenderRun run)
    {
        if (run.Option("--category") is not { } name)
        {
            return null;
        }

        if (Enum.TryParse<AddFolderId>(name, ignoreCase: true, out var id) && Enum.IsDefined(id))
        {
            return id;
        }

        Console.WriteLine($"gallery: no category '{name}' (has: {string.Join(", ", Enum.GetNames<AddFolderId>())})");
        return null;
    }

    /// <summary>The stand-in display's pixels: 1920 × 1080 unless --width/--height.</summary>
    private static (int Width, int Height) Display(RenderRun run) => (Pixels(run, "--width") ?? 1920, Pixels(run, "--height") ?? 1080);

    /// <summary>The gallery's and the surface's log lines, and every warning, said on stdout with the render's.</summary>
    private static void Listen() => Log.AddSink(static record =>
    {
        if (record.Level >= LogLevel.Warn || record.Scope is "widget-gallery" or "surface")
        {
            Console.WriteLine($"gallery: {record.Level} {record.Scope}: {record.Message} {record.Detail}");
        }
    });

    private static int? Pixels(RenderRun run, string name)
        => int.TryParse(run.Option(name), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) && value >= 640 ? value : null;

    /// <summary>
    /// The stand-in display (at the display's scale or 100 %, --raster 100), its host and surface in edit mode, the
    /// gallery open.
    /// </summary>
    private static async Task<(StandInDesktop Desk, DesktopWindow Surface, WidgetGalleryView Gallery)> StandAsync(RenderRun run, bool dark, string tag, (int Width, int Height) display)
    {
        var state = AppState.CreateDefault();
        var view = state.Views[0];
        view.Widgets.Clear();
        state.Design.Theme = dark ? AppTheme.Dark : AppTheme.Light;
        DesignApplier.Apply(state.Design);
        state.ShowViewOn(DisplayId, view.Id);

        var desk = StandInDesktop.Create(tag, state);
        var surface = RenderRaster.Made(run, () => desk.Add(StandInDesktop.Display(DisplayId, StandInDesktop.OffScreen(display.Width, display.Height))));
        desk.UseCheckButton(static () => false);
        desk.Show();

        // The surface laid out and its intro ended, edit mode drawn, the gallery open with its cards drawn: each settled.
        await desk.ReadyAsync($"{tag}: the surface").ConfigureAwait(true);
        desk.Host.SetEditMode(true);
        await desk.ReadyAsync($"{tag}: edit mode").ConfigureAwait(true);
        surface.OpenWidgetGallery();
        var gallery = surface.DebugGallery ?? throw new InvalidOperationException("the gallery did not open");
        await desk.ReadyAsync($"{tag}: the gallery open").ConfigureAwait(true);
        return (desk, surface, gallery);
    }

    private static async Task<int> DrawAsync(RenderRun run, string outDir, bool dark, AddFolderId? category, (int Width, int Height) display)
    {
        Listen();
        StandInDesktop? desk = null;
        var theme = dark ? "dark" : "light";
        try
        {
            Directory.CreateDirectory(outDir);
            (desk, var surface, var gallery) = await StandAsync(run, dark, "gallery-render", display).ConfigureAwait(true);
            StandInDesktop stand = desk;
            var host = desk.Host;
            async Task Shot(string name)
            {
                await stand.ReadyAsync($"gallery-{theme}-{display.Width}x{display.Height}-{name}.png").ConfigureAwait(true);
                await RenderHarness.SaveAsync(surface.DebugRoot, Path.Combine(outDir, $"gallery-{theme}-{display.Width}x{display.Height}-{name}.png"), "gallery").ConfigureAwait(true);
            }

            // Every category's bento from its top; one taller than the well again at its foot.
            for (var i = 0; i < gallery.Categories.Count; i++)
            {
                var shown = gallery.Categories[i];
                if (category is { } only && shown.Id != only)
                {
                    continue;
                }

                gallery.Show(i);
                await Shot($"{i:00}-{shown.Id}").ConfigureAwait(true);
                if (gallery.MostScroll > 0)
                {
                    gallery.ScrollTo(gallery.MostScroll);
                    await Shot($"{i:00}-{shown.Id}-end").ConfigureAwait(true);
                }
            }

            // [S-16402] A search: what it finds as a bento, no tab chosen.
            foreach (var query in new[] { "weather", "clock" })
            {
                gallery.Search(query);
                await Shot($"search-{query}").ConfigureAwait(true);
            }

            gallery.Search(string.Empty);

            // [S-16404] A widget with styles in focus: its words, its other styles and its neighbours round it.
            gallery.ShowCategory(category ?? AddFolderId.Time);
            var styled = gallery.Screen.Widgets.FirstOrDefault(w => WidgetStyles.Offers(w.Kind) && gallery.PieceOf(w.Id).Count == 1) ?? gallery.Screen.Widgets[0];
            gallery.FocusOn(styled.Id);
            await Shot("focus-styles").ConfigureAwait(true);

            // One with no style of its own: its sizes and its neighbours round it.
            for (var i = 0; i < gallery.Categories.Count; i++)
            {
                gallery.Show(i);
                if (gallery.Screen.Widgets.FirstOrDefault(w => !WidgetStyles.Offers(w.Kind) && gallery.PieceOf(w.Id).Count == 1) is { } plain)
                {
                    gallery.FocusOn(plain.Id);
                    await Shot("focus-plain").ConfigureAwait(true);
                    break;
                }
            }

            // A ready-made group in focus: its words and its neighbours.
            for (var i = 0; i < gallery.Categories.Count; i++)
            {
                if (gallery.Categories[i].Pieces.Any(p => p.Group is not null))
                {
                    gallery.Show(i);
                    var member = gallery.Screen.Widgets.First(w => gallery.PieceOf(w.Id).Count > 1);
                    gallery.FocusOn(member.Id);
                    await Shot("focus-group").ConfigureAwait(true);
                    break;
                }
            }

            // Several picked: the frame round them.
            gallery.ShowCategory(category ?? AddFolderId.Time);
            gallery.Pick([.. gallery.Screen.Widgets.Take(3).Select(w => w.Id)]);
            await Shot("select-several").ConfigureAwait(true);

            gallery.ShowCategory(category ?? AddFolderId.Time);
            gallery.Pick([gallery.Screen.Widgets[0].Id]);

            // A drag under way: the gallery stepped aside, the picked cards on the ghost, thirty cells on.
            var metrics = surface.Metrics;
            var held = Centre(gallery.Screen.Widgets[0].Rect, metrics);
            surface.BeginGalleryDragForCheck(gallery.Screen, gallery.Picked, held);
            var (cols, rows) = GridGeometry.GridDims(metrics);
            surface.MovePlacementForCheck(new Point(held.X + (30 * metrics.Width / cols), held.Y + (12 * metrics.Height / rows)));
            await Shot("drag").ConfigureAwait(true);
            host.CancelPlacement();
            return RenderCommandLine.Passed;
        }
        finally
        {
            desk?.Dispose();
        }
    }

    private static Point Centre(FracRect rect, ScreenMetrics metrics)
        => new((rect.X + (rect.W / 2)) * metrics.Width, (rect.Y + (rect.H * 0.4)) * metrics.Height);

    private static async Task<int> CheckAsync(RenderRun run, string outPath, (int Width, int Height) display)
    {
        Listen();
        var report = new CheckReport("gallery-check", outPath);
        void Say(string line) => report.Say(line);
        void Expect(bool ok, string what) => report.Expect(ok, what);
        StandInDesktop? desk = null;

        static bool Same(FracRect a, FracRect b) => Lattice.Same(a, b);

        try
        {
            (desk, var surface, var gallery) = await StandAsync(run, dark: true, "gallery-check", display).ConfigureAwait(true);
            StandInDesktop stand = desk;
            var host = desk.Host;
            var metrics = surface.Metrics;
            var (cols, rows) = GridGeometry.GridDims(metrics);
            var work = metrics.Allowed;
            Say($"gallery-check: {gallery.Categories.Count} categories over {metrics.Width:0}x{metrics.Height:0} DIPs, grid {cols}x{rows}");
            Say("     pieces per category: " + string.Join(", ", gallery.Categories.Select(c => $"{c.Id} {c.Pieces.Count} ({c.Pieces.Count(p => p.Group is not null)} groups)")));

            // [S-16400] Flush under the bar, as the add page hangs.
            var window = gallery.Frame!.Window;
            Expect(Math.Abs(window.Y - (surface.BarBottomForCheck + ShellMetrics.Flyout.Gap)) < 0.5,
                $"the gallery hangs flush under the edit bar (its top {window.Y:0.#}, the bar's foot {surface.BarBottomForCheck:0.#} + {ShellMetrics.Flyout.Gap:0})");
            Expect(window.X >= surface.WorkAreaForCheck.X && window.Y >= surface.WorkAreaForCheck.Y
                && window.X + window.Width <= surface.WorkAreaForCheck.X + surface.WorkAreaForCheck.Width + 1e-6
                && window.Y + window.Height <= surface.WorkAreaForCheck.Y + surface.WorkAreaForCheck.Height + 1e-6
                && window.Width < surface.WorkAreaForCheck.Width && window.Height < surface.WorkAreaForCheck.Height,
                $"the gallery is a window inside the work area ({window.Width:0}x{window.Height:0} of {surface.WorkAreaForCheck.Width:0}x{surface.WorkAreaForCheck.Height:0}), not all of it");

            // [S-14912] [S-16403] Every category, at its top and scrolled to its foot: whole cells of the display's grid, inside
            // the view, no two cards sharing a cell, every card where its widget is; the bento's holes; no family's small part.
            var offered = AddPage.Of([.. ModuleCatalog.Widgets.Select(d => d.Id)], ReadyMadeGroups.Offered());
            var parts = offered.Rail.SelectMany(r => offered.Sections(r.Id)).Where(s => s.Kind == AddSectionKind.Family)
                .SelectMany(s => s.Tiles.OfType<AddWidgetTile>()).Select(t => t.Kind).ToHashSet();
            var tidy = true;
            var whole = true;
            var reached = true;
            var holes = new List<string>();
            var holed = 0;
            for (var i = 0; i < gallery.Categories.Count; i++)
            {
                gallery.Show(i);
                var view = gallery.Frame!.View;
                var sheet = gallery.Sheet!;
                var h = WidgetGallery.Holes(sheet, view.W);
                var thick = WidgetGallery.Thickest(sheet, view.W);
                holes.Add($"{gallery.Categories[i].Id} {h} ({thick} thick)");
                holed = Math.Max(holed, h * 25 > (view.W + 1) * (sheet.Rows + 1) || WidgetGallery.Grows(sheet, view.W, gallery.SizeOf) is not null ? int.MaxValue : 0);
                foreach (var scrolled in new[] { 0, gallery.MostScroll })
                {
                    gallery.ScrollTo(scrolled);
                    tidy &= Tidy(gallery.Screen.Widgets.Select(w => w.Rect).ToList(), view);
                    tidy &= gallery.Screen.Widgets.TrueForAll(w => gallery.CardOf(w.Id) is { } card && Same(card.LiveRect, w.Rect));
                    if (scrolled == gallery.MostScroll && gallery.Screen.Widgets.Count > 0)
                    {
                        reached &= gallery.Screen.Widgets.Max(w => w.Rect.Bottom) * rows <= view.Bottom + 1e-6;
                    }
                }

                // At its top its first card stands on the view's first row.
                gallery.ScrollTo(0);
                reached &= gallery.Screen.Widgets.Count == 0 || Math.Abs((gallery.Screen.Widgets.Min(w => w.Rect.Y) * rows) - view.Y) < 1e-6;
                whole &= gallery.Categories[i].Pieces.All(p => p.Group is not null || (p.Kind is { } kind && !parts.Contains(kind)));
            }

            bool Tidy(List<FracRect> rects, CellBox view)
            {
                bool Whole(double v, int n) => Math.Abs((v * n) - Math.Round(v * n)) < 1e-6;
                var ok = rects.TrueForAll(r => Whole(r.X, cols) && Whole(r.Y, rows) && Whole(r.W, cols) && Whole(r.H, rows)
                    && r.X * cols >= view.X - 1e-6 && r.Right * cols <= view.Right + 1e-6 && r.X >= work.X && r.Right <= work.Right);
                for (var a = 0; a < rects.Count; a++)
                {
                    for (var b = a + 1; b < rects.Count; b++)
                    {
                        ok &= !(Math.Min(rects[a].Right, rects[b].Right) - Math.Max(rects[a].X, rects[b].X) > 1e-6 && Math.Min(rects[a].Bottom, rects[b].Bottom) - Math.Max(rects[a].Y, rects[b].Y) > 1e-6);
                    }
                }

                return ok;
            }

            Say("     holes per category (cells): " + string.Join(", ", holes));
            Expect(tidy && gallery.Categories.Count > 0, "every category's cards on whole cells of the display's grid, inside the view, apart, at its top and scrolled to its foot");
            Expect(reached, "every category starts on the view's top row and scrolls until its last card is inside the view");
            Expect(whole, "whole widgets and ready-made groups only: no family's small part");
            Expect(holed == 0, "[S-16403] every category a bento: every piece grown as far as it goes, at most one in twenty-five of its cells empty between pieces");

            // [S-14913] The wheel's step scrolls the sheet by whole cells and never changes the category.
            var tall = Enumerable.Range(0, gallery.Categories.Count).FirstOrDefault(i =>
            {
                gallery.Show(i);
                return gallery.MostScroll > 0;
            }, -1);
            if (tall >= 0)
            {
                gallery.Show(tall);
                var before = gallery.Screen.Widgets[0].Rect;
                gallery.ScrollTo(WidgetGallery.Step);
                var after = gallery.Screen.Widgets[0].Rect;
                Expect(gallery.Category.Id == gallery.Categories[tall].Id && gallery.Scrolled == Math.Min(WidgetGallery.Step, gallery.MostScroll)
                    && Math.Abs(((before.Y - after.Y) * rows) - gallery.Scrolled) < 1e-6,
                    $"{gallery.Category.Id}: a scroll step moves its cards {gallery.Scrolled} whole cells up and keeps the category");
            }
            else
            {
                Say("     no category is taller than the well at this size: scrolling not driven");
            }

            // [S-16402] A search: what it finds as a bento of whole pieces, the category back when it is emptied.
            gallery.ShowCategory(AddFolderId.Time);
            gallery.Search("weather");
            var found = gallery.Sheet!;
            var wholeKeys = gallery.Categories.SelectMany(c => c.Pieces).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
            Expect(gallery.Searching && found.Tiles.Count > 0 && found.Tiles.All(t => wholeKeys.Contains(t.Piece.Key)) && Tidy([.. gallery.Screen.Widgets.Select(w => w.Rect)], gallery.Frame!.View),
                $"a search for \"weather\" shows {found.Tiles.Count} whole pieces as a bento ({WidgetGallery.Holes(found, gallery.Frame!.View.W)} holes)");
            gallery.Search(string.Empty);
            Expect(!gallery.Searching && gallery.Category.Id == AddFolderId.Time && gallery.Sheet!.Tiles.Count == gallery.Category.Pieces.Count, "the search emptied: the category back");

            // [S-14914] A ready-made group is picked whole by any one of its widgets.
            var grouped = Enumerable.Range(0, gallery.Categories.Count).FirstOrDefault(i => gallery.Categories[i].Pieces.Any(p => p.Group is not null), -1);
            if (grouped >= 0)
            {
                gallery.Show(grouped);
                var member = gallery.Screen.Widgets.First(w => gallery.PieceOf(w.Id).Count > 1);
                gallery.Pick([member.Id]);
                Expect(gallery.Picked.Count == gallery.PieceOf(member.Id).Count && gallery.PieceOf(member.Id).All(gallery.Picked.Contains),
                    $"{gallery.Category.Id}: a group picked by one widget is picked whole ({gallery.Picked.Count} widgets)");
            }

            // [S-16404] [S-16406] A widget with styles in focus: the others gone, round it its words, its styles, its
            // neighbours; a click away brings the category back.
            gallery.ShowCategory(AddFolderId.Time);
            var count = gallery.Sheet!.Tiles.Count;
            var styledKind = gallery.Screen.Widgets.First(w => WidgetStyles.Offers(w.Kind) && gallery.PieceOf(w.Id).Count == 1);
            gallery.FocusOn(styledKind.Id);
            await stand.ReadyAsync("gallery check step 353").ConfigureAwait(true);
            var focus = gallery.Sheet!;
            var styles = focus.Tiles.Count(t => t.Piece.Role == GalleryRole.Style);
            var middle = focus.Tiles[0].Box;
            Expect(gallery.Focused && focus.Tiles[0].Piece.Role == GalleryRole.Picked && focus.Tiles[0].Piece.Kind == styledKind.Kind
                && gallery.Abouts == 1 && styles == WidgetStyles.For(styledKind.Kind).Count - 1
                && focus.Tiles.Count(t => t.Piece.Role == GalleryRole.Similar) >= WidgetGallery.FewestSimilar
                && middle.Y == 0 && Math.Abs((middle.X + (middle.W / 2.0)) - (gallery.Frame!.View.W / 2.0)) <= 1
                && Tidy([.. gallery.Screen.Widgets.Select(w => w.Rect)], gallery.Frame!.View),
                $"{styledKind.Kind} in focus: in the middle at the top, its words, {styles} other styles and its neighbours round it ({WidgetGallery.Holes(focus, gallery.Frame!.View.W)} holes)");
            Expect(gallery.Picked.Count == 1 && gallery.PieceAt(gallery.Picked[0])?.Role == GalleryRole.Picked, "the piece in focus is picked");

            // The arrows move the pick from piece to piece; Escape leaves the focus.
            Expect(gallery.HandleKey(Windows.System.VirtualKey.Right, false) && gallery.Picked.Count > 0 && gallery.PieceAt(gallery.Picked[0])?.Role != GalleryRole.Picked
                || gallery.HandleKey(Windows.System.VirtualKey.Down, false) && gallery.Picked.Count > 0 && gallery.PieceAt(gallery.Picked[0])?.Role != GalleryRole.Picked,
                "an arrow moves the pick to a card round the piece in focus");
            gallery.HandleKey(Windows.System.VirtualKey.Escape, false);
            await stand.ReadyAsync("gallery check step 370").ConfigureAwait(true);
            Expect(!gallery.Focused && gallery.Sheet!.Tiles.Count == count && gallery.Abouts == 0, "Escape leaves the focus: the category back whole");

            // One with no style of its own: its sizes round it.
            for (var i = 0; i < gallery.Categories.Count; i++)
            {
                gallery.Show(i);
                if (gallery.Screen.Widgets.FirstOrDefault(w => !WidgetStyles.Offers(w.Kind) && gallery.PieceOf(w.Id).Count == 1 && gallery.PieceAt(w.Id)!.Kind is { } k && SizeFit.Of(k).FixedWidth is false) is { } plain)
                {
                    gallery.FocusOn(plain.Id);
                    var sizes = gallery.Sheet!.Tiles.Count(t => t.Piece.Role == GalleryRole.Size);
                    Expect(gallery.Focused && gallery.Abouts == 1 && gallery.Sheet!.Tiles.All(t => t.Piece.Role != GalleryRole.Style),
                        $"{plain.Kind} (no styles) in focus: its words, {sizes} other sizes and its neighbours round it");
                    gallery.LeaveFocus();
                    break;
                }
            }

            // [S-16407] [S-16408] Enter places the pick: the placement session starts.
            gallery.ShowCategory(AddFolderId.Time);
            gallery.Pick([gallery.Screen.Widgets[0].Id]);
            gallery.HandleKey(Windows.System.VirtualKey.Enter, false);
            await stand.ReadyAsync("gallery check step 392").ConfigureAwait(true);
            Expect(host.Placing is not null && gallery.Following, "Enter places the pick: the placement session starts, the gallery steps aside");
            host.CancelPlacement();
            await stand.ReadyAsync("gallery check step 395").ConfigureAwait(true);

            WidgetGalleryView Open()
            {
                if (surface.DebugGallery is null)
                {
                    surface.OpenWidgetGallery();
                }

                return surface.DebugGallery ?? throw new InvalidOperationException("the gallery did not open again");
            }

            async Task<IReadOnlyList<WidgetInstance>> DragAndDrop(string what, IReadOnlyList<string> ids, double cellsX, double cellsY)
            {
                var screen = gallery.Screen;
                var picked = ids.Select(id => screen.Find(id)!).ToList();
                var held = Centre(picked[^1].Rect, metrics);

                // The move stays on the display (a ghost is held to it): the other way where it would leave it.
                var block = BlockGeometry.Bounds([.. picked.Select(w => w.Rect)]);
                cellsX = (block.Right + (cellsX / cols) > work.Right || block.X + (cellsX / cols) < work.X) ? -cellsX : cellsX;
                cellsY = (block.Bottom + (cellsY / rows) > work.Bottom || block.Y + (cellsY / rows) < work.Y) ? -cellsY : cellsY;
                var before = host.State.Views[0].Widgets.Select(w => w.Id).ToHashSet(StringComparer.Ordinal);
                gallery.Pick(ids);
                surface.BeginGalleryDragForCheck(screen, gallery.Picked, held);
                Expect(host.Placing is not null && gallery.Following, $"{what}: the drag starts the placement session, the gallery steps aside");

                // No jump: with the pointer where it held them the ghost stands on their cells, and the cards picked up with it.
                var start = surface.MovePlacementForCheck(held);
                var still = start.Count == picked.Count && picked.Select((w, i) => Same(start[i].Rect, w.Rect)).All(x => x);
                var drawn = picked.TrueForAll(w => gallery.CardOf(w.Id) is { } card
                    && Math.Abs(Microsoft.UI.Xaml.Controls.Canvas.GetLeft(card) - GridGeometry.PaintedCell(w.Rect, metrics, card.XamlRoot?.RasterizationScale ?? 1).X) < 0.01);
                if (!(still && drawn))
                {
                    Say($"     {what}: stood {string.Join(" ", picked.Select(w => w.Rect))}; ghost {string.Join(" ", start.Select(o => o.Rect))}; "
                        + $"cards at {string.Join(" ", picked.Select(w => gallery.CardOf(w.Id) is { } c ? $"{Microsoft.UI.Xaml.Controls.Canvas.GetLeft(c):0.#},{Microsoft.UI.Xaml.Controls.Canvas.GetTop(c):0.#}" : "none"))}");
                }

                Expect(still && drawn, $"{what}: held where it stood, the ghost is the cells they stood on and the cards stay put (no jump)");

                // Moved: whole cells, every one by the same, as the pointer went (rounded to the nearest line).
                var to = new Point(held.X + (cellsX * metrics.Width / cols), held.Y + (cellsY * metrics.Height / rows));
                var moved = surface.MovePlacementForCheck(to);
                var (dx, dy) = (Math.Round(cellsX) / cols, Math.Round(cellsY) / rows);
                Expect(moved.Count == picked.Count && picked.Select((w, i) => Same(moved[i].Rect, w.Rect with { X = w.Rect.X + dx, Y = w.Rect.Y + dy })).All(x => x),
                    $"{what}: moved {cellsX:0.#},{cellsY:0.#} cells, the ghost went {Math.Round(cellsX)},{Math.Round(cellsY)} whole cells, offsets kept");

                var landed = surface.ReleasePlacementForCheck(to);
                await stand.ReadyAsync($"{what}: dropped").ConfigureAwait(true);
                var added = host.State.Views[0].Widgets.Where(w => !before.Contains(w.Id)).ToList();
                var placedRight = landed && added.Count == picked.Count
                    && picked.Select((w, i) => added[i].Kind == w.Kind && Same(added[i].Rect, moved[i].Rect)).All(x => x);
                Expect(placedRight, $"{what}: dropped, {added.Count} new widget(s) where the ghost was");
                Expect(added.TrueForAll(w => host.Selection.Contains(w.Id)) && host.Selection.Count == added.Count, $"{what}: what landed is selected");

                // [S-16408] Landed: the gallery closed, the placed widgets' settings page open (for several, the page of several).
                Expect(surface.DebugGallery is null && host.Placing is null && surface.SelectionPageOpen,
                    $"{what}: the gallery closed and the settings page of what landed opened");
                return added;
            }

            // One widget, then three, from the Time tab; the gallery opened again for each.
            gallery.ShowCategory(AddFolderId.Time);
            await stand.ReadyAsync("the Time screen").ConfigureAwait(true);
            await DragAndDrop("one", [gallery.Screen.Widgets[0].Id], 37.4, 12.6).ConfigureAwait(true);
            await RenderHarness.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, ".dropped.png"), "gallery-check").ConfigureAwait(true);
            gallery = Open();
            await stand.ReadyAsync("gallery check step 462").ConfigureAwait(true);
            gallery.ShowCategory(AddFolderId.Time);
            await DragAndDrop("three", [.. gallery.Screen.Widgets.Take(3).Select(w => w.Id)], 3.3, 2.7).ConfigureAwait(true);

            // Given up: Escape's path, nothing placed, the gallery back.
            gallery = Open();
            await stand.ReadyAsync("gallery check step 468").ConfigureAwait(true);
            gallery.ShowCategory(AddFolderId.Time);
            var widgets = host.State.Views[0].Widgets.Count;
            gallery.Pick([gallery.Screen.Widgets[0].Id]);
            surface.BeginGalleryDragForCheck(gallery.Screen, gallery.Picked, Centre(gallery.Screen.Widgets[0].Rect, metrics));
            host.CancelPlacement();
            await stand.ReadyAsync("given up").ConfigureAwait(true);
            Expect(host.State.Views[0].Widgets.Count == widgets && !gallery.Following && ReferenceEquals(surface.DebugGallery, gallery) && !surface.SelectionPageOpen,
                "given up: nothing placed, the gallery back as it stood");

            // [S-16404] A style shown round a widget in focus lands in that style.
            gallery.FocusOn(gallery.Screen.Widgets.First(w => WidgetStyles.Offers(w.Kind) && gallery.PieceOf(w.Id).Count == 1).Id);
            await stand.ReadyAsync("gallery check step 480").ConfigureAwait(true);
            var styleCard = gallery.Screen.Widgets.First(w => gallery.PieceAt(w.Id)?.Role == GalleryRole.Style);
            var style = gallery.PieceAt(styleCard.Id)!.Style;
            var placed = await DragAndDrop($"{styleCard.Kind} in style {style}", [styleCard.Id], 5, -8).ConfigureAwait(true);
            Expect(placed.Count == 1 && WidgetStyles.Current(placed[0].Config) == style, $"the style shown ({style}) is the placed widget's");
            await RenderHarness.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, ".png"), "gallery-check").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            report.Crash(ex);
        }
        finally
        {
            desk?.Dispose();
        }

        return await report.FinishAsync().ConfigureAwait(true);
    }
}
#endif
