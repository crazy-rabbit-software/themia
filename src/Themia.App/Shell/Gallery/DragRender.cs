#if DEBUG
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-2008] [S-11874]..[S-11879] Debug only: the locked desktop's moves, off-screen, to PNGs.
/// <c>Themia.exe --drag-png &lt;outDir&gt;</c> writes, for both themes over a dark and a light stand-in
/// wallpaper: <c>drag-move-…</c> a clock moved on the locked desktop, the local grid round where it lands;
/// <c>drag-icons-&lt;size&gt;-…</c> desktop icons at each icon size on the widget grid, beside a clock, packed by
/// Auto arrange's rule, with the edit-mode grid drawn faintly to judge the cells; <c>drag-lasso-…</c> a mixed
/// lasso selection (icons in their selection look with their corners, [S-11889], cards with their corners) under the band;
/// [S-14310] <c>drag-group-…</c> several cards selected beside selected icons (the selection's frame's four corners round the
/// cards, none of their own) and <c>drag-groupmove-…</c> two of them moved together (the frame's corners with the block).
/// [S-13970] <c>--icon-layout below|beside|icononly</c> draws instead, on a larger screen, the icons and the fences in that
/// icon layout at medium and large icons (<c>drag-icons-&lt;layout&gt;-&lt;size&gt;-…</c>, <c>drag-fences-&lt;layout&gt;-&lt;size&gt;-…</c>),
/// dark over the dark wallpaper and light over the light one, an icon pointed at and one focused among them (icon only's
/// names over their neighbours, [S-13974]).
/// </summary>
internal static class DragRender
{
    public const string Flag = "--drag-png";

    private const string Scope = "drag-render";
    private static double ScreenWidth = 960;
    private static double ScreenHeight = 540;

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, $"usage: {Flag} <outDir>");
            app.Exit();
            return true;
        }

        var outDir = System.IO.Path.GetFullPath(args[at + 1]);

        // [S-13970] One icon layout's icons and fences, on a screen with room for text beside's wide boxes.
        var layoutAt = Array.IndexOf(args, "--icon-layout");
        DesktopIconLayout? layout = null;
        if (layoutAt >= 0)
        {
            if (layoutAt + 1 >= args.Length || !Enum.TryParse<DesktopIconLayout>(args[layoutAt + 1], ignoreCase: true, out var named) || !Enum.IsDefined(named))
            {
                Log.Error(Scope, "usage: --icon-layout below|beside|icononly");
                app.Exit();
                return true;
            }

            layout = named;
            (ScreenWidth, ScreenHeight) = (1440, 900);
        }

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;
            Ticker? ticker = null;
            try
            {
                System.IO.Directory.CreateDirectory(outDir);
                var screen = new Grid { Width = ScreenWidth, Height = ScreenHeight, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                window = new Window { Content = new Grid { Children = { screen } }, Title = "Themia drag render" };
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((ScreenWidth + 40) * scale), (int)((ScreenHeight + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);

                ticker = new Ticker(dispatcher);
                var metrics = new ScreenMetrics(ScreenWidth, ScreenHeight);
                if (layout is { } only)
                {
                    await LayoutAsync(screen, ticker, metrics, scale, only, outDir).ConfigureAwait(true);
                    return;
                }

                foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
                {
                    DesignApplier.Apply(new DesignState { Theme = theme });
                    screen.RequestedTheme = DesignApplier.Theme;
                    var tag = theme == AppTheme.Light ? "light" : "dark";
                    foreach (var (wallName, wall) in Wallpapers())
                    {
                        screen.Background = wall;
                        await MoveAsync(screen, ticker, metrics, scale, System.IO.Path.Combine(outDir, $"drag-move-{tag}-{wallName}.png"), several: false).ConfigureAwait(true);
                        await MoveAsync(screen, ticker, metrics, scale, System.IO.Path.Combine(outDir, $"drag-groupmove-{tag}-{wallName}.png"), several: true).ConfigureAwait(true);
                        await LassoAsync(screen, ticker, metrics, scale, System.IO.Path.Combine(outDir, $"drag-lasso-{tag}-{wallName}.png")).ConfigureAwait(true);
                        await GroupAsync(screen, ticker, metrics, scale, System.IO.Path.Combine(outDir, $"drag-group-{tag}-{wallName}.png")).ConfigureAwait(true);
                        await FencesAsync(screen, ticker, metrics, scale, System.IO.Path.Combine(outDir, $"drag-fences-{tag}-{wallName}.png")).ConfigureAwait(true);
                        foreach (var size in new[] { DesktopIconSize.Small, DesktopIconSize.Medium, DesktopIconSize.Large })
                        {
                            await IconsAsync(screen, ticker, metrics, scale, size, System.IO.Path.Combine(outDir, $"drag-icons-{size.ToString().ToLowerInvariant()}-{tag}-{wallName}.png")).ConfigureAwait(true);
                        }
                    }
                }

                // [S-13000] The fences again in Liquid glass, which frosts over the wallpaper.
                foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
                {
                    var glass = new DesignState();
                    DesignPresets.Apply(glass, DesignPresetId.LiquidGlass);
                    glass.Theme = theme;
                    DesignApplier.Apply(glass);
                    screen.RequestedTheme = DesignApplier.Theme;
                    var tag = theme == AppTheme.Light ? "light" : "dark";
                    foreach (var (wallName, wall) in Wallpapers())
                    {
                        screen.Background = wall;
                        await FencesAsync(screen, ticker, metrics, scale, System.IO.Path.Combine(outDir, $"drag-fences-glass-{tag}-{wallName}.png")).ConfigureAwait(true);
                    }
                }

                DesktopCell.Apply(DesktopIconSize.Medium);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the drag failed", ex);
            }
            finally
            {
                ticker?.Dispose();
                window?.Close();
                app.Exit();
            }
        });

        return true;
    }

    /// <summary>
    /// [S-13970] [S-13976] The icons and the fences in <paramref name="layout"/> at medium and large icons: dark over the dark
    /// wallpaper, light over the light one.
    /// </summary>
    private static async Task LayoutAsync(Grid screen, Ticker ticker, ScreenMetrics metrics, double scale, DesktopIconLayout layout, string outDir)
    {
        var name = layout.ToString().ToLowerInvariant();
        DesktopCell.Apply(layout);
        try
        {
            var walls = Wallpapers().ToList();
            foreach (var (theme, (wallName, wall)) in new[] { (AppTheme.Dark, walls[0]), (AppTheme.Light, walls[1]) })
            {
                DesignApplier.Apply(new DesignState { Theme = theme });
                screen.RequestedTheme = DesignApplier.Theme;
                screen.Background = wall;
                var tag = theme == AppTheme.Light ? "light" : "dark";
                foreach (var size in new[] { DesktopIconSize.Medium, DesktopIconSize.Large })
                {
                    var at = $"{name}-{size.ToString().ToLowerInvariant()}-{tag}-{wallName}.png";
                    await IconsAsync(screen, ticker, metrics, scale, size, System.IO.Path.Combine(outDir, $"drag-icons-{at}")).ConfigureAwait(true);
                    await FencesAsync(screen, ticker, metrics, scale, System.IO.Path.Combine(outDir, $"drag-fences-{at}"), size).ConfigureAwait(true);
                }
            }
        }
        finally
        {
            DesktopCell.Apply(DesktopIconSize.Medium);
            DesktopCell.Apply(DesktopIconLayout.Below);
        }
    }

    /// <summary>[S-13975] What the icons draw past their cards (icon only's names), over every card on <paramref name="layer"/>, as the surface's plate.</summary>
    private static void Hang(Canvas layer)
    {
        var hangs = new Canvas { Width = ScreenWidth, Height = ScreenHeight };
        foreach (var card in layer.Children.OfType<WidgetCard>().ToList())
        {
            hangs.Children.Add(card.OverhangPlate);
        }

        layer.Children.Add(hangs);
    }

    /// <summary>
    /// [S-11874] [S-11881] [S-11886] A clock mid-move on the locked desktop: the card where it lands, the round
    /// local grid round it, its corner marks and the marks of the cards near it fading with the grid (one
    /// touching it, one a little apart, one past the fade without any), a desktop icon beside. [S-14310]
    /// <paramref name="several"/>: a date beside it, both selected, move with it: the selection's frame's four corners go
    /// round the two where they land, neither shows its own; the neighbours' marks fade as before.
    /// </summary>
    private static async Task MoveAsync(Grid screen, Ticker ticker, ScreenMetrics metrics, double scale, string path, bool several)
    {
        screen.Children.Clear();
        DesktopCell.Apply(DesktopIconSize.Medium);
        var selected = new HashSet<string>(StringComparer.Ordinal);
        var host = new NoticeRender.RenderCardHost(ticker) { Selected = selected };
        var local = new LocalGridOverlay();
        screen.Children.Add(local);
        var layer = new Canvas { Width = ScreenWidth, Height = ScreenHeight };
        screen.Children.Add(layer);

        var clock = GridGeometry.Settle(new FracRect(0.42, 0.33, 0.3, 0.24), metrics);
        var moving = new List<WidgetCard> { new(host, WidgetInstance.Create(new WidgetId("clock"), clock)) };
        Place(layer, moving[0], clock, metrics, scale);
        if (several)
        {
            var date = GridGeometry.Settle(new FracRect(clock.Right, clock.Y, 0.14, clock.H), metrics);
            moving.Add(new WidgetCard(host, WidgetInstance.Create(new WidgetId("date"), date)));
            Place(layer, moving[1], date, metrics, scale);
            selected.UnionWith(moving.Select(c => c.Model.Id));
        }

        var target = BlockGeometry.Bounds([.. moving.Select(c => c.LiveRect)]);
        var standing = new List<WidgetCard>();
        foreach (var (kind, at) in new[] { ("countdown", new FracRect(0.17, 0.33, 0.25, 0.24)), ("wifiToggle", new FracRect(0.5, 0.66, 0.13, 0.22)), ("clock", new FracRect(0.76, 0.02, 0.22, 0.2)) })
        {
            var rect = GridGeometry.Settle(at, metrics);
            var card = new WidgetCard(host, WidgetInstance.Create(new WidgetId(kind), rect));
            Place(layer, card, rect, metrics, scale);
            standing.Add(card);
        }

        await IconAsync(layer, host, metrics, scale, new FracRect(0.02, 0.04, 0, 0), "app").ConfigureAwait(true);

        // [S-11889] An icon just below where the clock lands: its corners fade in with the grid like a card's.
        standing.Add(await IconAsync(layer, host, metrics, scale, new FracRect(0.36, 0.6, 0, 0), "app").ConfigureAwait(true));

        // The marks on their plates over every card, at the strengths the locked move gives them.
        var markers = new EditMarkerLayer();
        var borders = LocalGrid.Borders(target, moving.Select(c => c.Model.Id), standing.Select(c => (c.Model.Id, c.LiveRect)), metrics);
        var cards = layer.Children.OfType<WidgetCard>().ToList();
        foreach (var card in cards)
        {
            markers.Add(card);
            card.ShowMoveMarks(borders.TryGetValue(card.Model.Id, out var alpha) ? alpha : null);
        }

        // [S-14310] Several moving: the frame's corners round them, none of their own.
        RenderFrame.Round(markers, [.. cards.Select(c => (c, GridGeometry.ToPixels(c.LiveRect, metrics, scale)))], selected, editing: false, scale, [host]);
        screen.Children.Add(markers.Halos);
        screen.Children.Add(markers.Inks);

        local.Show(target, metrics, scale);
        await Task.Delay(700).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"drag: wrote {path}");
    }

    /// <summary>[S-11877] A mixed selection under the band: two icons selected (their look), a clock selected (its corners), one icon and one card not.</summary>
    private static async Task LassoAsync(Grid screen, Ticker ticker, ScreenMetrics metrics, double scale, string path)
    {
        screen.Children.Clear();
        DesktopCell.Apply(DesktopIconSize.Medium);
        var clock = WidgetInstance.Create(new WidgetId("clock"), GridGeometry.Settle(new FracRect(0.2, 0.08, 0.3, 0.24), metrics));
        var other = WidgetInstance.Create(new WidgetId("clock"), GridGeometry.Settle(new FracRect(0.62, 0.55, 0.3, 0.24), metrics));
        var selected = new HashSet<string>(StringComparer.Ordinal) { clock.Id };
        var host = new NoticeRender.RenderCardHost(ticker) { Selected = selected };
        var layer = new Canvas { Width = ScreenWidth, Height = ScreenHeight };
        screen.Children.Add(layer);

        Place(layer, new WidgetCard(host, clock), clock.Rect, metrics, scale);
        Place(layer, new WidgetCard(host, other), other.Rect, metrics, scale);
        // [S-11889] The selected icons in the desktop's one selection: their corners over their highlight.
        selected.Add((await IconAsync(layer, host, metrics, scale, new FracRect(0.02, 0.04, 0, 0), "selected").ConfigureAwait(true)).Model.Id);
        selected.Add((await IconAsync(layer, host, metrics, scale, new FracRect(0.02, 0.26, 0, 0), "ownSelected").ConfigureAwait(true)).Model.Id);
        await IconAsync(layer, host, metrics, scale, new FracRect(0.02, 0.6, 0, 0), "app").ConfigureAwait(true);

        // The band from empty desktop over both icons into the clock.
        var band = new Border { Width = 0.3 * ScreenWidth, Height = 0.4 * ScreenHeight, IsHitTestVisible = false };
        SelectionChrome.PaintBand(band);
        Canvas.SetLeft(band, 0.01 * ScreenWidth);
        Canvas.SetTop(band, 0.02 * ScreenHeight);
        layer.Children.Add(band);

        // The cards' marks on their plates, over every card, as the surface draws them.
        var markers = new EditMarkerLayer();
        foreach (var card in layer.Children.OfType<WidgetCard>().ToList())
        {
            markers.Add(card);
            card.ApplySelection();
        }

        screen.Children.Add(markers.Halos);
        screen.Children.Add(markers.Inks);

        await Task.Delay(900).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"drag: wrote {path}");
    }

    /// <summary>
    /// [S-14310] Several cards selected on the locked desktop beside selected icons: a clock, a date touching it and a
    /// countdown under them in the desktop's one selection draw no corners of their own; the selection's frame's four corners
    /// (a locked selected card's look) go round the three; the two selected icons keep their highlight alone ([S-11889]) and
    /// are not in the frame; one icon and one card are not selected.
    /// </summary>
    private static async Task GroupAsync(Grid screen, Ticker ticker, ScreenMetrics metrics, double scale, string path)
    {
        screen.Children.Clear();
        DesktopCell.Apply(DesktopIconSize.Medium);
        var clock = WidgetInstance.Create(new WidgetId("clock"), GridGeometry.Settle(new FracRect(0.2, 0.08, 0.3, 0.24), metrics));
        var date = WidgetInstance.Create(new WidgetId("date"), GridGeometry.Settle(new FracRect(clock.Rect.Right, clock.Rect.Y, 0.14, clock.Rect.H), metrics));
        var countdown = WidgetInstance.Create(new WidgetId("countdown"), GridGeometry.Settle(new FracRect(clock.Rect.X, clock.Rect.Bottom, 0.25, 0.24), metrics));
        var other = WidgetInstance.Create(new WidgetId("clock"), GridGeometry.Settle(new FracRect(0.68, 0.6, 0.28, 0.24), metrics));
        var selected = new HashSet<string>(StringComparer.Ordinal) { clock.Id, date.Id, countdown.Id };
        var host = new NoticeRender.RenderCardHost(ticker) { Selected = selected };
        var layer = new Canvas { Width = ScreenWidth, Height = ScreenHeight };
        screen.Children.Add(layer);

        foreach (var widget in new[] { clock, date, countdown, other })
        {
            Place(layer, new WidgetCard(host, widget), widget.Rect, metrics, scale);
        }

        selected.Add((await IconAsync(layer, host, metrics, scale, new FracRect(0.02, 0.04, 0, 0), "selected").ConfigureAwait(true)).Model.Id);
        selected.Add((await IconAsync(layer, host, metrics, scale, new FracRect(0.02, 0.26, 0, 0), "ownSelected").ConfigureAwait(true)).Model.Id);
        await IconAsync(layer, host, metrics, scale, new FracRect(0.02, 0.6, 0, 0), "app").ConfigureAwait(true);

        // The cards' marks on their plates, over every card, and the frame's corners with them, as the surface draws them.
        var markers = new EditMarkerLayer();
        var cards = layer.Children.OfType<WidgetCard>().ToList();
        foreach (var card in cards)
        {
            markers.Add(card);
        }

        RenderFrame.Round(markers, [.. cards.Select(c => (c, GridGeometry.ToPixels(c.LiveRect, metrics, scale)))], selected, editing: false, scale, [host]);
        screen.Children.Add(markers.Halos);
        screen.Children.Add(markers.Inks);

        await Task.Delay(900).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"drag: wrote {path}");
    }

    /// <summary>
    /// [S-13000] [S-13007] [S-13012] [S-13040] [S-13042] [S-13043] Fences on the locked desktop, over the widget grid drawn
    /// faintly to judge the cells: each a region of the desktop icon grid whose items are the desktop's own icon cards, laid
    /// by Core's one layout (<see cref="DesktopFences.Lay"/>) beside free icons on the same grid: one with two items selected,
    /// one rolled up to its title bar, one with more items than it shows scrolled a row (its thumb), free icons on the same
    /// rows and columns, and the rectangle a right-drag draws to make one, in the band's look.
    /// </summary>
    private static async Task FencesAsync(Grid screen, Ticker ticker, ScreenMetrics metrics, double scale, string path, DesktopIconSize size = DesktopIconSize.Medium)
    {
        screen.Children.Clear();
        DesktopCell.Apply(size);
        var host = new NoticeRender.RenderCardHost(ticker);
        var grid = new SnapGridOverlay();
        grid.Draw(metrics, scale);
        screen.Children.Add(grid);
        var layer = new Canvas { Width = ScreenWidth, Height = ScreenHeight };
        screen.Children.Add(layer);

        if (ModuleCatalog.Widgets.FirstOrDefault(d => typeof(IHoldsDesktopItems).IsAssignableFrom(d.ConfigType)) is not { } fenceKind
            || ModuleCatalog.Widgets.FirstOrDefault(d => typeof(IDesktopItemConfig).IsAssignableFrom(d.ConfigType)) is not { } iconKind)
        {
            return;
        }

        var box = DesktopGrid.IconSize(DesktopCell.Grid(DesktopCell.Current), metrics);
        var (cols, rows) = GridGeometry.GridDims(metrics);
        FracRect Cells(int c, int r, double w, double h) => new(c / (double)cols, r / (double)rows, w, h);

        // Free icons in a column at the work area's edge, and one on the row of the fence's first cells, past it.
        var view = ViewState.Create("render");
        var states = new Dictionary<string, string>(StringComparer.Ordinal);
        WidgetInstance Icon(FracRect at, string state)
        {
            var icon = WidgetInstance.Create(iconKind.Id, at with { W = box.W, H = box.H });
            ((IDesktopItemConfig)icon.Config).Item = $@"C:\Users\Public\Desktop\render {view.Widgets.Count}.txt";
            view.Widgets.Add(icon);
            states[icon.Id] = state;
            return icon;
        }

        Icon(Cells(1, 1, 0, 0), "thisPc");
        Icon(Cells(1, 1 + (int)Math.Round(box.H * rows), 0, 0), "selected");
        Icon(Cells(1, 1 + (2 * (int)Math.Round(box.H * rows)), 0, 0), "recycleBin");

        WidgetInstance Fence(FracRect at, int count, string[] looks, Action<IHoldsDesktopItems> set)
        {
            var fence = WidgetInstance.Create(fenceKind.Id, at);
            var held = (IHoldsDesktopItems)fence.Config;
            held.Name = "Work";
            view.Widgets.Add(fence);
            for (var i = 0; i < count; i++)
            {
                var icon = Icon(at, looks[i % looks.Length]);
                held.Items.Add(((IDesktopItemConfig)icon.Config).Item);
            }

            set(held);
            return fence;
        }

        var across = (int)Math.Round(box.W * cols);
        var down = (int)Math.Round(box.H * rows);
        var title = FenceGrid.TitleCells;

        // [S-13976] A fence of text beside's wide boxes is a list: one box across.
        var wide = FenceGrid.MinAcross(DesktopCell.Grid(size)) == 1 ? 1 : 3;
        Fence(Cells(1 + across, 1, wide * box.W, (title / (double)rows) + (FenceGrid.RowsOf(6, wide) * box.H)), 6, ["document", "selected", "hover", "twoWords", "link", "selected"], _ => { });
        Icon(Cells(1 + ((wide + 1) * across), 1 + title, 0, 0), "document");
        Fence(Cells(2 + ((wide + 2) * across), 1, wide * box.W, 0), 6, ["document"], f => f.RolledUp = true);
        Fence(Cells(2 + ((wide + 2) * across), 1 + (2 * title), wide * box.W, (title / (double)rows) + (2 * box.H)), 20, ["document", "folder", "link", "twoWords", "longName"], f => f.Scroll = 1);
        foreach (var on in DesktopFences.On(view))
        {
            if (!on.Fence.RolledUp && on.Widget.H <= 0)
            {
                on.Widget.H = (title / (double)rows) + (2 * box.H);
            }
        }

        DesktopFences.Lay(view, metrics, box);
        var hidden = DesktopFences.Hidden(view, metrics, box);
        foreach (var widget in DesktopFences.PaintOrder(view).Where(w => !hidden.Contains(w.Id)))
        {
            var card = new WidgetCard(host, widget);
            Place(layer, card, widget.Rect, metrics, scale);
            if (states.TryGetValue(widget.Id, out var state) && card.View is IWidgetPreviewState preview)
            {
                await preview.ShowPreviewStateAsync(state).ConfigureAwait(true);
            }
        }

        Hang(layer);

        // The rectangle a right-drag on empty desktop draws before "Create fence here".
        var band = new Border { Width = 0.3 * ScreenWidth, Height = 0.25 * ScreenHeight, IsHitTestVisible = false };
        SelectionChrome.PaintBand(band);
        Canvas.SetLeft(band, 0.4 * ScreenWidth);
        Canvas.SetTop(band, 0.68 * ScreenHeight);
        layer.Children.Add(band);

        await Task.Delay(900).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"drag: wrote {path}");
    }

    /// <summary>[S-11875] [S-11876] Twelve icons at <paramref name="size"/> packed on the widget grid round a clock, the grid drawn to judge them.</summary>
    private static async Task IconsAsync(Grid screen, Ticker ticker, ScreenMetrics metrics, double scale, DesktopIconSize size, string path)
    {
        screen.Children.Clear();
        DesktopCell.Apply(size);
        var host = new NoticeRender.RenderCardHost(ticker);
        var grid = new SnapGridOverlay();
        grid.Draw(metrics, scale);
        screen.Children.Add(grid);
        var layer = new Canvas { Width = ScreenWidth, Height = ScreenHeight };
        screen.Children.Add(layer);

        var clockRect = GridGeometry.Settle(new FracRect(0.25, 0.0, 0.3, 0.24), metrics);
        Place(layer, new WidgetCard(host, WidgetInstance.Create(new WidgetId("clock"), clockRect)), clockRect, metrics, scale);

        var box = DesktopGrid.IconSize(DesktopCell.Grid(size), metrics);
        var packed = DesktopGrid.Pack([.. Enumerable.Range(0, 12).Select(i => new FracRect(i * 0.001, 0.9, box.W, box.H))], [clockRect], box, metrics);
        string[] states = ["thisPc", "recycleBin", "app", "web", "twoWords", "longName", "layout", "selected", "ownTwoWords", "hover", "focus", "app"];
        for (var i = 0; i < packed.Count; i++)
        {
            if (packed[i] is { } at)
            {
                await IconAsync(layer, host, metrics, scale, at, states[i]).ConfigureAwait(true);
            }
        }

        // [S-11880] The surface's one join over them, as the desktop runs it: icons that touch stay free.
        WidgetCard.Join([.. layer.Children.OfType<WidgetCard>()], metrics, scale, editMode: false, _ => 0);
        Hang(layer);

        await Task.Delay(900).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"drag: wrote {path}");
    }

    /// <summary>One desktop icon at <paramref name="at"/>'s corner, in its box on the grid, in a preview state.</summary>
    private static async Task<WidgetCard> IconAsync(Canvas layer, IWidgetCardHost host, ScreenMetrics metrics, double scale, FracRect at, string state)
    {
        var kind = ModuleCatalog.Widgets.First(d => typeof(IDesktopItemConfig).IsAssignableFrom(d.ConfigType));
        var (w, h) = DesktopGrid.IconSize(DesktopCell.Grid(DesktopCell.Current), metrics);
        var rect = GridGeometry.Settle(new FracRect(at.X, at.Y, w, h), metrics);
        rect = rect with { W = w, H = h };
        var card = new WidgetCard(host, WidgetInstance.Create(kind.Id, rect));
        Place(layer, card, rect, metrics, scale);
        if (card.View is IWidgetPreviewState preview)
        {
            await preview.ShowPreviewStateAsync(state).ConfigureAwait(true);
        }

        return card;
    }

    private static void Place(Canvas layer, WidgetCard card, FracRect rect, ScreenMetrics metrics, double scale)
    {
        var cell = GridGeometry.ToPixels(rect, metrics, scale);
        card.Width = cell.Width;
        card.Height = cell.Height;
        card.SetLiveRect(rect);
        Canvas.SetLeft(card, cell.X);
        Canvas.SetTop(card, cell.Y);
        layer.Children.Add(card);
    }

    /// <summary>Stand-in wallpapers, a dark and a light wash: render fixtures, not design colours.</summary>
    private static IEnumerable<(string Name, Brush Wall)> Wallpapers()
    {
        yield return ("darkwall", Wash(Windows.UI.Color.FromArgb(255, 30, 52, 84), Windows.UI.Color.FromArgb(255, 92, 58, 80)));
        yield return ("lightwall", Wash(Windows.UI.Color.FromArgb(255, 214, 226, 238), Windows.UI.Color.FromArgb(255, 240, 222, 206)));
    }

    private static LinearGradientBrush Wash(Windows.UI.Color from, Windows.UI.Color to) => new()
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(1, 1),
        GradientStops =
        {
            new GradientStop { Color = from, Offset = 0 },
            new GradientStop { Color = to, Offset = 1 },
        },
    };
}
#endif
