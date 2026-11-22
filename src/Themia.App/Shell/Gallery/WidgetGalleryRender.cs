#if DEBUG
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-14917] Debug only: the widget gallery through the app's own wiring, a real <see cref="AppHost"/> over a detached store
/// and a real <see cref="DesktopWindow"/> on a stand-in 1920 × 1080 display (its taskbar 48 at the foot), off-screen, in edit
/// mode with the gallery opened from the bar's path.
/// <c>Themia.exe --render gallery &lt;outDir&gt; [--theme light|dark] [--category Id]</c> draws every category's bento (or the
/// one's; one taller than the well again at its foot), one widget picked with its panel (the clock, its styles), several
/// picked (the frame round them), a ready-made group picked whole and a drag under way
/// (<c>gallery-&lt;theme&gt;-…png</c>). <c>Themia.exe --check gallery &lt;out.txt&gt;</c> drives the gallery as its pointer does:
/// one widget and then three picked, dragged and dropped, a drag given up and a style picked; it says whether the ghost
/// started on the cells they stood on (no jump), moved by whole cells, landed where it was, selected, and whether the
/// gallery came back with them still on it; PASS or FAIL.
/// </summary>
internal static class WidgetGalleryRender
{
    private const string DisplayId = "gallery-render-display";

    public static RenderCommand Command { get; } = new(
        RenderVerb.Render,
        "gallery",
        "<outDir> [--theme light|dark] [--category Id] [--width w --height h] [--raster 100]",
        """
        the widget gallery through a real host and surface in edit mode: every screen (or --category's), one widget picked (its
        panel, its styles), several, a drag under way (gallery-<theme>-<w>x<h>-*.png) [S-14917]; --width/--height the stand-in
        display's pixels (1920 × 1080), --raster 100 at 100 %
        """,
        run => DrawAsync(run, run.PathArg(0), run.App.RequestedTheme != ApplicationTheme.Light, Category(run), Display(run)));

    public static RenderCommand Check { get; } = new(
        RenderVerb.Check,
        "gallery",
        "<out.txt> [--width w --height h] [--raster 100]",
        """
        the widget gallery's drag driven as its pointer does: one widget and three picked, dragged (no jump, whole cells) and
        dropped (selected, the gallery back), a drag given up, a style picked; PASS or FAIL [S-14917]
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

            // One widget picked: its name, what it is and its styles in the panel.
            gallery.ShowCategory(category ?? AddFolderId.Time);
            var first = gallery.Screen.Widgets.FirstOrDefault(w => WidgetStyles.Offers(w.Kind) && gallery.PieceOf(w.Id).Count == 1) ?? gallery.Screen.Widgets[0];
            gallery.Pick([first.Id]);
            await Shot("select-one").ConfigureAwait(true);

            // Several: the frame round them, their names in the panel.
            gallery.Pick([.. gallery.Screen.Widgets.Take(3).Select(w => w.Id)]);
            await Shot("select-several").ConfigureAwait(true);

            // A ready-made group picked: all of it, by one of its widgets; what it is in the panel.
            for (var i = 0; i < gallery.Categories.Count; i++)
            {
                if (gallery.Categories[i].Pieces.Any(p => p.Group is not null))
                {
                    gallery.Show(i);
                    var member = gallery.Screen.Widgets.First(w => gallery.PieceOf(w.Id).Count > 1);
                    gallery.Pick([member.Id]);
                    await Shot("select-group").ConfigureAwait(true);
                    break;
                }
            }

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

            // [S-14912] Every category, at its top and scrolled to its foot: whole cells of the display's grid, inside the view
            // across, no two cards sharing a cell, every card where its widget is; no family's small part among them.
            var offered = AddPage.Of([.. ModuleCatalog.Widgets.Select(d => d.Id)], ReadyMadeGroups.Offered());
            var parts = offered.Rail.SelectMany(r => offered.Sections(r.Id)).Where(s => s.Kind == AddSectionKind.Family)
                .SelectMany(s => s.Tiles.OfType<AddWidgetTile>()).Select(t => t.Kind).ToHashSet();
            var tidy = true;
            var whole = true;
            var reached = true;
            for (var i = 0; i < gallery.Categories.Count; i++)
            {
                gallery.Show(i);
                var view = gallery.Frame!.View;
                foreach (var scrolled in new[] { 0, gallery.MostScroll })
                {
                    gallery.ScrollTo(scrolled);
                    var rects = gallery.Screen.Widgets.Select(w => w.Rect).ToList();
                    bool Whole(double v, int n) => Math.Abs((v * n) - Math.Round(v * n)) < 1e-6;
                    tidy &= rects.TrueForAll(r => Whole(r.X, cols) && Whole(r.Y, rows) && Whole(r.W, cols) && Whole(r.H, rows)
                        && r.X * cols >= view.X - 1e-6 && r.Right * cols <= view.Right + 1e-6 && r.X >= work.X && r.Right <= work.Right);
                    for (var a = 0; a < rects.Count; a++)
                    {
                        for (var b = a + 1; b < rects.Count; b++)
                        {
                            tidy &= !(Math.Min(rects[a].Right, rects[b].Right) - Math.Max(rects[a].X, rects[b].X) > 1e-6 && Math.Min(rects[a].Bottom, rects[b].Bottom) - Math.Max(rects[a].Y, rects[b].Y) > 1e-6);
                        }
                    }

                    tidy &= gallery.Screen.Widgets.TrueForAll(w => gallery.CardOf(w.Id) is { } card && Same(card.LiveRect, w.Rect));
                    if (scrolled == gallery.MostScroll && rects.Count > 0)
                    {
                        // Scrolled to its foot, its last card ends inside the view; at its top its first starts there.
                        reached &= rects.Max(r => r.Bottom) * rows <= view.Bottom + 1e-6;
                    }
                }

                // At its top its first card starts in the view's first tile (a piece that keeps its size stands in its tile's middle).
                gallery.ScrollTo(0);
                var first = gallery.Screen.Widgets.Count == 0 ? view.Y : gallery.Screen.Widgets.Min(w => w.Rect.Y) * rows;
                reached &= first >= view.Y - 1e-6 && first < view.Y + WidgetGallery.PitchTall;
                whole &= gallery.Categories[i].Pieces.All(p => p.Group is not null || (p.Kind is { } kind && !parts.Contains(kind)));
            }

            Expect(tidy && gallery.Categories.Count > 0, "every category's cards on whole cells of the display's grid, inside the view, apart, at its top and scrolled to its foot");
            Expect(reached, "every category starts at the view's top and scrolls until its last card is inside the view");
            Expect(whole, "whole widgets and ready-made groups only: no family's small part");
            var window = gallery.Frame!.Window;
            Expect(window.X >= surface.WorkAreaForCheck.X && window.Y >= surface.WorkAreaForCheck.Y
                && window.X + window.Width <= surface.WorkAreaForCheck.X + surface.WorkAreaForCheck.Width + 1e-6
                && window.Y + window.Height <= surface.WorkAreaForCheck.Y + surface.WorkAreaForCheck.Height + 1e-6
                && window.Width < surface.WorkAreaForCheck.Width && window.Height < surface.WorkAreaForCheck.Height,
                $"the gallery is a window inside the work area ({window.Width:0}x{window.Height:0} of {surface.WorkAreaForCheck.Width:0}x{surface.WorkAreaForCheck.Height:0}), not all of it");

            // [S-14913] The wheel's step scrolls the category by whole cells and never changes it.
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

            async Task<bool> DragAndDrop(string what, IReadOnlyList<string> ids, double cellsX, double cellsY)
            {
                var screen = gallery.Screen;
                var picked = ids.Select(id => screen.Find(id)!).ToList();
                var held = Centre(picked[^1].Rect, metrics);
                var before = host.State.Views[0].Widgets.Select(w => w.Id).ToHashSet(StringComparer.Ordinal);
                gallery.Pick(ids);
                surface.BeginGalleryDragForCheck(screen, gallery.Picked, held);
                Expect(host.Placing is not null && gallery.Following, $"{what}: the drag starts the placement session, the gallery steps aside");

                // No jump: with the pointer where it held them the ghost stands on their cells, and the cards picked up with it.
                var start = surface.MovePlacementForCheck(held);
                var still = start.Count == picked.Count && picked.Select((w, i) => Same(start[i].Rect, w.Rect)).All(x => x);
                var drawn = picked.TrueForAll(w => gallery.CardOf(w.Id) is { } card
                    && Math.Abs(Microsoft.UI.Xaml.Controls.Canvas.GetLeft(card) - GridGeometry.PaintedCell(w.Rect, metrics, card.XamlRoot?.RasterizationScale ?? 1).X) < 0.01);
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

                // Replenished: the gallery is back, every card on its cell, the kinds still there.
                var back = surface.DebugGallery is { } open && ReferenceEquals(open, gallery) && !gallery.Following && host.Placing is null
                    && picked.TrueForAll(w => gallery.Screen.Find(w.Id) is not null && gallery.CardOf(w.Id) is { Opacity: 1 } card
                        && Math.Abs(Microsoft.UI.Xaml.Controls.Canvas.GetTop(card) - GridGeometry.PaintedCell(w.Rect, metrics, card.XamlRoot?.RasterizationScale ?? 1).Y) < 0.01);
                Expect(back, $"{what}: the gallery is back, the widgets dragged still on it at their cells");
                return placedRight;
            }

            // One widget, then three, from the Time screen.
            gallery.ShowCategory(AddFolderId.Time);
            await stand.ReadyAsync("the Time screen").ConfigureAwait(true);
            await DragAndDrop("one", [gallery.Screen.Widgets[0].Id], 37.4, 12.6).ConfigureAwait(true);
            await RenderHarness.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, ".dropped.png"), "gallery-check").ConfigureAwait(true);
            await DragAndDrop("three", [.. gallery.Screen.Widgets.Take(3).Select(w => w.Id)], 3.3, 2.7).ConfigureAwait(true);

            // Given up: Escape's path, nothing placed, the gallery back.
            var count = host.State.Views[0].Widgets.Count;
            gallery.Pick([gallery.Screen.Widgets[0].Id]);
            surface.BeginGalleryDragForCheck(gallery.Screen, gallery.Picked, Centre(gallery.Screen.Widgets[0].Rect, metrics));
            host.CancelPlacement();
            await stand.ReadyAsync("given up").ConfigureAwait(true);
            Expect(host.State.Views[0].Widgets.Count == count && !gallery.Following && surface.DebugGallery is not null, "given up: nothing placed, the gallery back");

            // A style picked in the panel lands with the widget.
            var styled = gallery.Screen.Widgets.FirstOrDefault(w => WidgetStyles.Offers(w.Kind));
            if (styled is not null)
            {
                var option = WidgetStyles.For(styled.Kind)[^1];
                gallery.Pick([styled.Id]);
                gallery.ChooseStyle(styled.Kind, option.Value);
                var before = host.State.Views[0].Widgets.Select(w => w.Id).ToHashSet(StringComparer.Ordinal);
                await DragAndDrop($"styled {styled.Kind} ({option.Name})", [styled.Id], 5, -8).ConfigureAwait(true);
                var landed = host.State.Views[0].Widgets.FirstOrDefault(w => !before.Contains(w.Id));
                Expect(landed is not null && WidgetStyles.Current(landed.Config) == option.Value, $"the style picked ({option.Name}) is the placed widget's");
            }

            // The page of the selection does not open over the gallery.
            Expect(!surface.BarMenuOpenOtherThanGallery, "no other flyout opened over the gallery after the drops");
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
