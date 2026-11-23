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
using G = Themia.Sdk.Kit.ShellMetrics.WidgetGallery;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-14917] [S-16564] Debug only: the widget gallery through the app's own wiring, a real <see cref="AppHost"/> over a
/// detached store and a real <see cref="DesktopWindow"/> on a stand-in display (1920 × 1080 pixels unless sized, its taskbar
/// 48 at the foot), off-screen, in edit mode with the gallery opened from the bar's path.
/// <c>Themia.exe --render gallery &lt;outDir&gt; [--theme light|dark] [--category Id]</c> draws every category's bento (or the
/// one's; one taller than the well again at its foot), two searches, a widget with styles in focus, one without and a
/// ready-made group in focus, several picked and a drag under way (<c>gallery-&lt;theme&gt;-…png</c>); each of
/// <c>--displays WxH,..</c> after it (small monitors, [S-16561]) its Time, System and Quick settings tabs, a search and a focus.
/// <c>--wallpaper &lt;picture&gt;</c> stands the display over that wallpaper ([S-16563]).
/// <c>Themia.exe --check gallery &lt;out.txt&gt;</c> checks the window under the bar and its beak, the head (tabs, hint, search
/// always open), every bento's columns (no hole, how even their feet), a search (its first finds, a keystroke's time), the
/// focus (the piece never moving, its words and styles alone, clicks on them picking, never navigating) and its return, the
/// keys, the quick settings' two sizes, and drives the drag as its pointer does: one widget and then three picked, dragged and
/// dropped, a drag given up and a style shown in focus dragged; on each of <c>--displays</c> the window, head and bentos
/// again; PASS or FAIL.
/// </summary>
internal static class WidgetGalleryRender
{
    private const string DisplayId = "gallery-render-display";

    public static RenderCommand Command { get; } = new(
        RenderVerb.Render,
        "gallery",
        "<outDir> [--theme light|dark] [--category Id] [--width w --height h] [--displays WxH,..] [--wallpaper <picture>] [--raster 100]",
        """
        the widget gallery through a real host and surface in edit mode: every tab (or --category's), searches, a widget with
        styles, one without and a group in focus, several picked, a drag under way (gallery-<theme>-<w>x<h>-*.png) [S-14917]
        [S-16410]; --width/--height the stand-in display's pixels (1920 × 1080), --displays more displays after it (their Time,
        System and Quick settings tabs, a search, a focus) [S-16561], --wallpaper its wallpaper [S-16563], --raster 100 at 100 %
        """,
        run => DrawAsync(run, run.PathArg(0), run.App.RequestedTheme != ApplicationTheme.Light, Category(run), Displays(run)));

    public static RenderCommand Check { get; } = new(
        RenderVerb.Check,
        "gallery",
        "<out.txt> [--width w --height h] [--displays WxH,..] [--wallpaper <picture>] [--raster 100]",
        """
        the widget gallery under the bar (its beak), its head (tabs, hint, search), its column bentos (no hole, even feet), a
        search (its first finds, a keystroke's time), the focus (the piece never moving, its styles alone, clicks picking, never
        navigating) and its return, the keys, the quick settings' two sizes, and its drag driven as its pointer does: one widget
        and three dragged (no jump, whole cells) and dropped (selected, their settings page open, the gallery closed), a drag
        given up, a style dragged from a focus; --displays the window and bentos on more displays; PASS or FAIL [S-14917]
        [S-16410] [S-16564]
        """,
        run => CheckAsync(run, run.PathArg(0), Displays(run)));

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

    /// <summary>The stand-in displays' pixels: 1920 × 1080 unless --width/--height, then each of --displays.</summary>
    private static List<(int Width, int Height)> Displays(RenderRun run)
    {
        List<(int Width, int Height)> displays = [(Pixels(run.Option("--width")) ?? 1920, Pixels(run.Option("--height")) ?? 1080)];
        foreach (var size in (run.Option("--displays") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = size.Split('x', 'X');
            if (parts.Length == 2 && Pixels(parts[0]) is { } w && Pixels(parts[1]) is { } h)
            {
                displays.Add((w, h));
            }
            else
            {
                throw run.Usage($"--displays takes WxH,.. of 640 or more each, not '{size}'");
            }
        }

        return displays;
    }

    /// <summary>The gallery's and the surface's log lines, and every warning, said on stdout with the render's.</summary>
    private static void Listen() => Log.AddSink(static record =>
    {
        if (record.Level >= LogLevel.Warn || record.Scope is "widget-gallery" or "surface")
        {
            Console.WriteLine($"gallery: {record.Level} {record.Scope}: {record.Message} {record.Detail}");
        }
    });

    private static int? Pixels(string? value)
        => int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var pixels) && pixels >= 640 ? pixels : null;

    /// <summary>[S-16563] --wallpaper: the stand-in displays' wallpaper, until disposed; null without one.</summary>
    private static IDisposable? Wallpaper(RenderRun run, (int Width, int Height) display)
        => run.Option("--wallpaper") is { } picture ? RenderWallpaper.Use(picture, new ScreenMetrics(display.Width, display.Height)) : null;

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

    private static async Task<int> DrawAsync(RenderRun run, string outDir, bool dark, AddFolderId? category, List<(int Width, int Height)> displays)
    {
        Listen();
        Directory.CreateDirectory(outDir);
        for (var d = 0; d < displays.Count; d++)
        {
            await DrawOneAsync(run, outDir, dark, category, displays[d], brief: d > 0).ConfigureAwait(true);
        }

        return RenderCommandLine.Passed;
    }

    /// <summary>One display's pictures: all of them, or (<paramref name="brief"/>, a small display) three tabs, a search and a focus.</summary>
    private static async Task DrawOneAsync(RenderRun run, string outDir, bool dark, AddFolderId? category, (int Width, int Height) display, bool brief)
    {
        StandInDesktop? desk = null;
        var theme = dark ? "dark" : "light";
        using var wallpaper = Wallpaper(run, display);
        try
        {
            (desk, var surface, var gallery) = await StandAsync(run, dark, "gallery-render", display).ConfigureAwait(true);
            StandInDesktop stand = desk;
            var host = desk.Host;
            var size = $"{surface.Metrics.Width:0}x{surface.Metrics.Height:0}";
            async Task Shot(string name)
            {
                var file = $"gallery-{theme}-{size}-{name}.png";
                await stand.ReadyAsync(file).ConfigureAwait(true);
                await RenderHarness.SaveAsync(surface.DebugRoot, Path.Combine(outDir, file), "gallery").ConfigureAwait(true);
            }

            // Every category's bento from its top; one taller than the well again at its foot. A small display: three.
            AddFolderId[] few = [AddFolderId.Time, AddFolderId.System, AddFolderId.QuickSettings];
            for (var i = 0; i < gallery.Categories.Count; i++)
            {
                var shown = gallery.Categories[i];
                if ((category is { } only && shown.Id != only) || (brief && !few.Contains(shown.Id)))
                {
                    continue;
                }

                gallery.Show(i);
                await Shot($"{i:00}-{shown.Id}").ConfigureAwait(true);
                if (gallery.MostScroll > 0 && !brief)
                {
                    gallery.ScrollTo(gallery.MostScroll);
                    await Shot($"{i:00}-{shown.Id}-end").ConfigureAwait(true);
                }
            }

            // [S-16402] [S-16557] A search: its first finds as a bento, no tab chosen.
            foreach (var query in brief ? new[] { "weather" } : new[] { "weather", "clock", "e" })
            {
                gallery.Search(query);
                await Shot($"search-{query}").ConfigureAwait(true);
            }

            gallery.Search(string.Empty);

            // [S-16554] [S-16555] A widget with styles in focus: where it stood, its words and its other styles round it.
            gallery.ShowCategory(category ?? AddFolderId.Time);
            var styled = gallery.Screen.Widgets.FirstOrDefault(w => WidgetStyles.Offers(w.Kind) && gallery.PieceOf(w.Id).Count == 1) ?? gallery.Screen.Widgets[0];
            await Shot("focus-before").ConfigureAwait(true);
            gallery.FocusOn(styled.Id);
            await Shot("focus-styles").ConfigureAwait(true);
            if (brief)
            {
                return;
            }

            // A style picked in focus: picked, nothing else moves.
            if (gallery.Screen.Widgets.FirstOrDefault(w => gallery.PieceAt(w.Id)?.Role == GalleryRole.Style) is { } style)
            {
                gallery.ClickForCheck(style.Id);
                await Shot("focus-style-picked").ConfigureAwait(true);
            }

            // One with no style of its own: its words alone round it.
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

            // A ready-made group in focus: its words.
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
        }
        finally
        {
            desk?.Dispose();
        }
    }

    private static Point Centre(FracRect rect, ScreenMetrics metrics)
        => new((rect.X + (rect.W / 2)) * metrics.Width, (rect.Y + (rect.H * 0.4)) * metrics.Height);

    private static Point Corner(UIElement card) => new(Microsoft.UI.Xaml.Controls.Canvas.GetLeft(card), Microsoft.UI.Xaml.Controls.Canvas.GetTop(card));

    private static async Task<int> CheckAsync(RenderRun run, string outPath, List<(int Width, int Height)> displays)
    {
        Listen();
        var report = new CheckReport("gallery-check", outPath);
        for (var d = 0; d < displays.Count; d++)
        {
            await CheckOneAsync(run, report, outPath, displays[d], brief: d > 0).ConfigureAwait(true);
        }

        return await report.FinishAsync().ConfigureAwait(true);
    }

    /// <summary>One display's checks: all of them, or (<paramref name="brief"/>, a small display) its window, head and bentos.</summary>
    private static async Task CheckOneAsync(RenderRun run, CheckReport report, string outPath, (int Width, int Height) display, bool brief)
    {
        void Say(string line) => report.Say(line);
        void Expect(bool ok, string what) => report.Expect(ok, what);
        StandInDesktop? desk = null;
        using var wallpaper = Wallpaper(run, display);

        static bool Same(FracRect a, FracRect b) => Lattice.Same(a, b);

        try
        {
            (desk, var surface, var gallery) = await StandAsync(run, dark: true, "gallery-check", display).ConfigureAwait(true);
            StandInDesktop stand = desk;
            var host = desk.Host;
            var metrics = surface.Metrics;
            var (cols, rows) = GridGeometry.GridDims(metrics);
            var work = metrics.Allowed;
            var at = $"{metrics.Width:0}x{metrics.Height:0}";
            Say($"gallery-check: {gallery.Categories.Count} categories over {at} DIPs at {(surface.DebugRoot.XamlRoot?.RasterizationScale ?? 1) * 100:0} %, grid {cols}x{rows}, view {gallery.Frame!.View.W} cells wide");
            Say("     pieces per category: " + string.Join(", ", gallery.Categories.Select(c => $"{c.Id} {c.Pieces.Count} ({c.Pieces.Count(p => p.Group is not null)} groups)")));

            // [S-16400] Flush under the bar, as the add page hangs; [S-16558] its beak pointing at the gallery's button.
            var window = gallery.Frame!.Window;
            var area = surface.WorkAreaForCheck;
            Expect(Math.Abs(window.Y - (surface.BarBottomForCheck + ShellMetrics.Flyout.Gap)) < 0.5,
                $"{at}: the gallery hangs flush under the edit bar (its top {window.Y:0.#}, the bar's foot {surface.BarBottomForCheck:0.#} + {ShellMetrics.Flyout.Gap:0})");
            Expect(window.X >= area.X && window.Y >= area.Y && window.X + window.Width <= area.X + area.Width + 1e-6 && window.Y + window.Height <= area.Y + area.Height + 1e-6
                && window.Width < area.Width && window.Height < area.Height,
                $"{at}: the gallery is a window inside the work area ({window.Width:0}x{window.Height:0} of {area.Width:0}x{area.Height:0}), not all of it");
            if (surface.GalleryButtonForCheck is { } button)
            {
                var middle = button.X + (button.Width / 2);
                Expect(Math.Abs(gallery.Tip - middle) < 1 || (middle < window.X + ShellMetrics.OverlayRadius + ShellMetrics.Flyout.BeakWidth) || (middle > window.X + window.Width - ShellMetrics.OverlayRadius - ShellMetrics.Flyout.BeakWidth),
                    $"{at}: [S-16558] the window's beak points at the gallery's button (its tip {gallery.Tip:0.#}, the button's middle {middle:0.#})");
            }
            else
            {
                Report(false, $"{at}: [S-16558] the gallery's button is laid out on the bar");
            }

            // [S-16559] [S-16560] [S-16557] [S-16561] The head: the tabs, the hint and the search box always open, all inside the window, none over another.
            var head = gallery.Frame!.Head;
            var (search, hint, tabs) = (gallery.SearchBoxShown, gallery.HintBox, gallery.TabsShown);
            Say($"     head {head.Width:0}x{head.Height:0} at {head.Y - window.Y:0} under the window's top, the well {gallery.Frame!.Well.Y - (head.Y + head.Height):0} under it; tabs {tabs?.Width:0}, hint {hint?.Width:0}, search {search?.Width:0} wide");
            Expect(Math.Abs(head.Height - ShellMetrics.Page.Tabs) < 0.5 && Math.Abs(head.Y - window.Y - G.HeadInset) < 0.5,
                $"{at}: [S-16559] the tabs' line is a tab tall, {G.HeadInset:0} under the window's top and {G.HeadGap:0} over the well");
            bool Inside(DipRect? box) => box is { } b && b.X >= window.X - 0.5 && b.X + b.Width <= window.X + window.Width + 0.5 && b.Y >= window.Y - 0.5 && b.Y + b.Height <= window.Y + window.Height + 0.5;
            Expect(Inside(search) && search!.Value.Width >= 120, $"{at}: [S-16557] the search box is open at the line's end, inside the window");
            Say($"     the hint: {gallery.HintShown}; every tab in sight: {gallery.AllTabsShown}");
            Expect(gallery.HintShown == GalleryHint.None ? hint is null : Inside(hint) && hint!.Value.X + hint.Value.Width <= search!.Value.X + 0.5,
                $"{at}: [S-16560] the hint ({gallery.HintShown}) stands before the search box, inside the window, or is not shown");
            Expect(Inside(tabs) && (hint is not { } shownHint || tabs!.Value.X + tabs.Value.Width <= shownHint.X + 0.5), $"{at}: [S-16561] the tabs keep to their room, clear of the hint");
            Expect(gallery.AllTabsShown || gallery.HintShown == GalleryHint.None, $"{at}: [S-16566] the hint never takes the tabs' room: shown only while every tab is in sight");
            if (metrics.Width >= 1920 - 0.5)
            {
                Expect(gallery.AllTabsShown, $"{at}: [S-16566] every tab in sight on a full-size display");
            }
            if (run.Has("--wallpaper"))
            {
                Expect(gallery.WallpaperShown, $"{at}: [S-16563] the well shows the desktop's own wallpaper");
            }

            // [S-16550]..[S-16553] Every category, at its top and scrolled to its foot: whole cells of the display's grid, inside
            // the view, no two cards sharing a cell, every card where its widget is; its columns: no hole, how even the feet.
            var offered = AddPage.Of([.. ModuleCatalog.Widgets.Select(d => d.Id)], ReadyMadeGroups.Offered());
            var parts = offered.Rail.SelectMany(r => offered.Sections(r.Id)).Where(s => s.Kind == AddSectionKind.Family && !WidgetGallery.InQuickSettings.Contains(s.Family!.Id))
                .SelectMany(s => s.Tiles.OfType<AddWidgetTile>()).Select(t => t.Kind).ToHashSet();
            var tidy = true;
            var whole = true;
            var reached = true;
            var columns = new List<string>();
            var holed = 0;
            for (var i = 0; i < gallery.Categories.Count; i++)
            {
                gallery.Show(i);
                var view = gallery.Frame!.View;
                var sheet = gallery.Sheet!;
                var h = WidgetGallery.Holes(sheet);
                columns.Add($"{gallery.Categories[i].Id} {sheet.Columns.Count} ({string.Join("+", sheet.Columns.Select(c => c.W))}) ragged {WidgetGallery.Ragged(sheet)}");
                holed += h;
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

            Say("     columns per category: " + string.Join(", ", columns));
            Expect(tidy && gallery.Categories.Count > 0, $"{at}: every category's cards on whole cells of the display's grid, inside the view, apart, at its top and scrolled to its foot");
            Expect(reached, $"{at}: every category starts on the view's top row and scrolls until its last card is inside the view");
            Expect(whole, $"{at}: whole widgets and ready-made groups only: no family's small part");
            Expect(holed == 0, $"{at}: [S-16552] every category a column bento: no cell of a column empty between its cards ({holed})");

            // [S-16562] Quick settings: every toggle at its size and right after it at its least, both cards.
            var quick = gallery.Categories.ToList().FindIndex(c => c.Id == AddFolderId.QuickSettings);
            if (quick >= 0)
            {
                gallery.Show(quick);
                var least = gallery.Screen.Widgets.Where(w => gallery.PieceAt(w.Id)?.Least == true).ToList();
                var full = gallery.Screen.Widgets.Where(w => gallery.PieceAt(w.Id) is { Least: false, Kind: not null, Group: null }).ToList();
                Expect(least.Count > 0 && least.Count == full.Count && least.TrueForAll(w => full.Exists(f => gallery.PieceAt(f.Id)?.Pick == gallery.PieceAt(w.Id)?.Pick && f.Rect.W >= w.Rect.W - 1e-9 && f.Rect.H >= w.Rect.H - 1e-9 && f.Rect.W * f.Rect.H > (w.Rect.W * w.Rect.H) + 1e-9)),
                    $"{at}: [S-16562] [S-16565] the quick settings show {full.Count} toggles and power buttons at their size and {least.Count} at their least, smaller");
            }

            if (brief)
            {
                // A small display: a search and a focus besides.
                gallery.Search("weather");
                Expect(gallery.Sheet!.Tiles.Count is > 0 and <= WidgetGallery.MostFound && WidgetGallery.Holes(gallery.Sheet!) == 0 && Tidy([.. gallery.Screen.Widgets.Select(w => w.Rect)], gallery.Frame!.View),
                    $"{at}: a search shows its first {gallery.Sheet!.Tiles.Count} finds as a column bento");
                gallery.Search(string.Empty);
                await FocusAsync().ConfigureAwait(true);
                return;
            }

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

            // [S-16402] [S-16557] A search: its first finds as a bento of whole pieces, the category back when it is emptied.
            gallery.ShowCategory(AddFolderId.Time);
            gallery.Search("e");
            var found = gallery.Sheet!;
            var wholeKeys = gallery.Categories.SelectMany(c => c.Pieces).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
            Expect(gallery.Searching && found.Tiles.Count == WidgetGallery.MostFound && found.Tiles.All(t => wholeKeys.Contains(t.Piece.Key)) && Tidy([.. gallery.Screen.Widgets.Select(w => w.Rect)], gallery.Frame!.View) && WidgetGallery.Holes(found) == 0,
                $"a search for \"e\" shows its first {found.Tiles.Count} of more finds, whole pieces, as a column bento");
            gallery.Search(string.Empty);
            Expect(!gallery.Searching && gallery.Category.Id == AddFolderId.Time && gallery.Sheet!.Tiles.Count == gallery.Category.Pieces.Count, "the search emptied: the category back");

            // [S-16557] What a keystroke costs: "weather" typed a letter at a time, each search's own work on the UI thread
            // (finding, laying, building its cards), the cards' samples drawn after it.
            var typed = new List<(double All, double Lay, int Found)>();
            foreach (var prefix in Enumerable.Range(1, "weather".Length).Select(n => "weather"[..n]).Concat(Enumerable.Range(1, "weather".Length).Select(n => "weather"[..n])))
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                gallery.Search(prefix);
                typed.Add((clock.Elapsed.TotalMilliseconds, gallery.LastLay.TotalMilliseconds, gallery.Sheet!.Tiles.Count));
                await stand.ReadyAsync($"searched \"{prefix}\"").ConfigureAwait(true);
            }

            static string Ms(double ms) => ms.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            Say($"     a keystroke's search (\"weather\" letter by letter, twice: ms in all, ms laying, pieces): {string.Join("; ", typed.Select(t => $"{Ms(t.All)} {Ms(t.Lay)} {t.Found}"))}; the second time's middle {Ms(typed.Skip(7).Select(t => t.All).Order().ElementAt(3))} ms, its most {Ms(typed.Skip(7).Max(t => t.All))} ms");
            gallery.Search(string.Empty);

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

            var count = await FocusAsync().ConfigureAwait(true);

            // The arrows move the pick between the cards in focus; Escape leaves the focus.
            gallery.ShowCategory(AddFolderId.Time);
            gallery.ClickForCheck(gallery.Screen.Widgets.First(w => WidgetStyles.Offers(w.Kind) && gallery.PieceOf(w.Id).Count == 1).Id);
            await stand.ReadyAsync("focused for the keys").ConfigureAwait(true);
            var focusKey = gallery.FocusKey;
            var pickedBefore = gallery.Picked.ToList();
            var moved = false;
            foreach (var arrow in new[] { Windows.System.VirtualKey.Down, Windows.System.VirtualKey.Up, Windows.System.VirtualKey.Right, Windows.System.VirtualKey.Left })
            {
                if (!moved && gallery.HandleKey(arrow, false) && !gallery.Picked.SequenceEqual(pickedBefore))
                {
                    moved = true;
                }
            }

            Expect(moved && gallery.Picked.Count > 0 && gallery.FocusKey == focusKey,
                $"an arrow moves the pick in focus; the focus stays (the search box {(gallery.SearchHasKeys ? "has" : "has not")} the keys)");
            gallery.HandleKey(Windows.System.VirtualKey.Escape, false);
            await stand.ReadyAsync("escaped the focus").ConfigureAwait(true);
            Expect(!gallery.Focused && gallery.Sheet!.Tiles.Count == count && gallery.Abouts == 0, "Escape leaves the focus: the category back whole");

            // [S-16407] [S-16408] Enter places the pick: the placement session starts.
            gallery.ShowCategory(AddFolderId.Time);
            gallery.Pick([gallery.Screen.Widgets[0].Id]);
            gallery.HandleKey(Windows.System.VirtualKey.Enter, false);
            await stand.ReadyAsync("gallery check: Enter").ConfigureAwait(true);
            Expect(host.Placing is not null && gallery.Following, "Enter places the pick: the placement session starts, the gallery steps aside");
            host.CancelPlacement();
            await stand.ReadyAsync("gallery check: Enter given up").ConfigureAwait(true);

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
                        + $"cards at {string.Join(" ", picked.Select(w => gallery.CardOf(w.Id) is { } c ? $"{Corner(c).X:0.#},{Corner(c).Y:0.#}" : "none"))}");
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
            await stand.ReadyAsync("gallery check: open again").ConfigureAwait(true);
            gallery.ShowCategory(AddFolderId.Time);
            await DragAndDrop("three", [.. gallery.Screen.Widgets.Take(3).Select(w => w.Id)], 3.3, 2.7).ConfigureAwait(true);

            // Given up: Escape's path, nothing placed, the gallery back.
            gallery = Open();
            await stand.ReadyAsync("gallery check: open for giving up").ConfigureAwait(true);
            gallery.ShowCategory(AddFolderId.Time);
            var widgets = host.State.Views[0].Widgets.Count;
            gallery.Pick([gallery.Screen.Widgets[0].Id]);
            surface.BeginGalleryDragForCheck(gallery.Screen, gallery.Picked, Centre(gallery.Screen.Widgets[0].Rect, metrics));
            host.CancelPlacement();
            await stand.ReadyAsync("given up").ConfigureAwait(true);
            Expect(host.State.Views[0].Widgets.Count == widgets && !gallery.Following && ReferenceEquals(surface.DebugGallery, gallery) && !surface.SelectionPageOpen,
                "given up: nothing placed, the gallery back as it stood");

            // [S-16404] A style shown round a widget in focus, clicked (picked) and dragged out, lands in that style.
            gallery.FocusOn(gallery.Screen.Widgets.First(w => WidgetStyles.Offers(w.Kind) && gallery.PieceOf(w.Id).Count == 1).Id);
            await stand.ReadyAsync("gallery check: a style in focus").ConfigureAwait(true);
            // One standing whole in the view (a card under the well's foot is cut by it; dragged, the ghost is held to the display).
            var styleCard = gallery.Screen.Widgets.Where(w => gallery.PieceAt(w.Id)?.Role == GalleryRole.Style)
                .OrderBy(w => w.Rect.Bottom * rows > gallery.Frame!.View.Bottom + 1e-6 || w.Rect.Bottom > work.Bottom + 1e-9 ? 1 : 0).First();
            var style = gallery.PieceAt(styleCard.Id)!.Style;
            gallery.ClickForCheck(styleCard.Id);
            var placed = await DragAndDrop($"{styleCard.Kind} in style {style}", [styleCard.Id], 5, -8).ConfigureAwait(true);
            Expect(placed.Count == 1 && WidgetStyles.Current(placed[0].Config) == style, $"the style shown ({style}) is the placed widget's");
            await RenderHarness.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, ".png"), "gallery-check").ConfigureAwait(true);

            // [S-16554]..[S-16556] A widget with styles put in focus where it stands: the same card on the same cells before, in
            // focus and after; its words and its other styles alone round it; a click on a style picks it and navigates nowhere;
            // a click on empty room leaves the focus. One of one style: its words alone. Returns the Time tab's piece count.
            async Task<int> FocusAsync()
            {
                gallery.ShowCategory(AddFolderId.Time);
                var pieces = gallery.Sheet!.Tiles.Count;
                gallery.ScrollTo(0);
                var styled = gallery.Screen.Widgets.First(w => WidgetStyles.Offers(w.Kind) && gallery.PieceOf(w.Id).Count == 1);
                var card = gallery.CardOf(styled.Id)!;
                var (corner, rect) = (Corner(card), card.LiveRect);
                gallery.ClickForCheck(styled.Id);
                await stand.ReadyAsync($"{at}: {styled.Kind} put in focus").ConfigureAwait(true);
                var focus = gallery.Sheet!;
                var styles = focus.Tiles.Count(t => t.Piece.Role == GalleryRole.Style);
                Expect(gallery.Focused && ReferenceEquals(gallery.CardOf(styled.Id), card) && Corner(card) == corner && Same(card.LiveRect, rect),
                    $"{at}: [S-16554] {styled.Kind} clicked: in focus, the same card on the same cells ({corner.X:0.#},{corner.Y:0.#})");
                Expect(gallery.Abouts == 1 && styles == WidgetStyles.For(styled.Kind).Count - 1 && focus.Tiles.All(t => t.Piece.Role is GalleryRole.Picked or GalleryRole.About or GalleryRole.Style)
                    && Tidy([.. gallery.Screen.Widgets.Select(w => w.Rect)], gallery.Frame!.View),
                    $"{at}: [S-16555] round it its words and its {styles} other styles, nothing else");

                // [S-16556] A style clicked: picked, the focus stays, nothing moves or navigates; clicked again, the same.
                var key = gallery.FocusKey;
                var other = gallery.Screen.Widgets.First(w => gallery.PieceAt(w.Id)?.Role == GalleryRole.Style);
                var otherCorner = Corner(gallery.CardOf(other.Id)!);
                gallery.ClickForCheck(other.Id);
                gallery.ClickForCheck(other.Id);
                await stand.ReadyAsync($"{at}: a style clicked in focus").ConfigureAwait(true);
                Expect(gallery.Focused && gallery.FocusKey == key && gallery.Picked.SequenceEqual([other.Id]) && Corner(card) == corner && Corner(gallery.CardOf(other.Id)!) == otherCorner
                    && gallery.Sheet!.Tiles.Count == focus.Tiles.Count,
                    $"{at}: [S-16556] a style clicked in focus is picked; the focus, the cards and the sheet stay");
                gallery.ClickForCheck(styled.Id);
                Expect(gallery.Focused && gallery.FocusKey == key && gallery.Picked.SequenceEqual([styled.Id]), $"{at}: [S-16556] the piece itself clicked again: picked, still in focus");

                // Left by a click on empty room: the sheet back, the card where it stood.
                gallery.ClickEmptyForCheck();
                await stand.ReadyAsync($"{at}: the focus left").ConfigureAwait(true);
                Expect(!gallery.Focused && ReferenceEquals(gallery.CardOf(styled.Id), card) && Corner(card) == corner && gallery.Sheet!.Tiles.Count == pieces,
                    $"{at}: [S-16554] a click on empty room leaves the focus; the card stays where it stood");

                // One of one style: its words alone.
                for (var i = 0; i < gallery.Categories.Count; i++)
                {
                    gallery.Show(i);
                    if (gallery.Screen.Widgets.FirstOrDefault(w => !WidgetStyles.Offers(w.Kind) && gallery.PieceOf(w.Id).Count == 1) is { } plain)
                    {
                        gallery.FocusOn(plain.Id);
                        Expect(gallery.Focused && gallery.Abouts == 1 && gallery.Sheet!.Tiles.Count == 2, $"{at}: [S-16555] {plain.Kind} (one style) in focus: its words alone round it");
                        gallery.LeaveFocus();
                        break;
                    }
                }

                await stand.ReadyAsync($"{at}: focus checked").ConfigureAwait(true);
                return pieces;
            }
        }
        catch (Exception ex)
        {
            report.Crash(ex);
        }
        finally
        {
            desk?.Dispose();
        }

        void Report(bool ok, string what) => report.Expect(ok, what);
    }
}
#endif
