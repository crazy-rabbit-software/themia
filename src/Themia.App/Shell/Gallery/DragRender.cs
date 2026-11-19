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
/// lasso selection (icons in their selection look with their corners, [S-11889], cards with their corners) under the band.
/// </summary>
internal static class DragRender
{
    public const string Flag = "--drag-png";

    private const string Scope = "drag-render";
    private const double ScreenWidth = 960;
    private const double ScreenHeight = 540;

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
                foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
                {
                    DesignApplier.Apply(new DesignState { Theme = theme });
                    screen.RequestedTheme = DesignApplier.Theme;
                    var tag = theme == AppTheme.Light ? "light" : "dark";
                    foreach (var (wallName, wall) in Wallpapers())
                    {
                        screen.Background = wall;
                        await MoveAsync(screen, ticker, metrics, scale, System.IO.Path.Combine(outDir, $"drag-move-{tag}-{wallName}.png")).ConfigureAwait(true);
                        await LassoAsync(screen, ticker, metrics, System.IO.Path.Combine(outDir, $"drag-lasso-{tag}-{wallName}.png")).ConfigureAwait(true);
                        await FencesAsync(screen, ticker, metrics, System.IO.Path.Combine(outDir, $"drag-fences-{tag}-{wallName}.png")).ConfigureAwait(true);
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
                        await FencesAsync(screen, ticker, metrics, System.IO.Path.Combine(outDir, $"drag-fences-glass-{tag}-{wallName}.png")).ConfigureAwait(true);
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
    /// [S-11874] [S-11881] [S-11886] A clock mid-move on the locked desktop: the card where it lands, the round
    /// local grid round it, its corner marks and the marks of the cards near it fading with the grid (one
    /// touching it, one a little apart, one past the fade without any), a desktop icon beside.
    /// </summary>
    private static async Task MoveAsync(Grid screen, Ticker ticker, ScreenMetrics metrics, double scale, string path)
    {
        screen.Children.Clear();
        DesktopCell.Apply(DesktopIconSize.Medium);
        var host = new NoticeRender.RenderCardHost(ticker);
        var local = new LocalGridOverlay();
        screen.Children.Add(local);
        var layer = new Canvas { Width = ScreenWidth, Height = ScreenHeight };
        screen.Children.Add(layer);

        var clock = GridGeometry.Settle(new FracRect(0.42, 0.33, 0.3, 0.24), metrics);
        var moving = new WidgetCard(host, WidgetInstance.Create(new WidgetId("clock"), clock));
        Place(layer, moving, clock, metrics);
        var standing = new List<WidgetCard>();
        foreach (var (kind, at) in new[] { ("countdown", new FracRect(0.17, 0.33, 0.25, 0.24)), ("wifiToggle", new FracRect(0.5, 0.66, 0.13, 0.22)), ("clock", new FracRect(0.76, 0.02, 0.22, 0.2)) })
        {
            var rect = GridGeometry.Settle(at, metrics);
            var card = new WidgetCard(host, WidgetInstance.Create(new WidgetId(kind), rect));
            Place(layer, card, rect, metrics);
            standing.Add(card);
        }

        await IconAsync(layer, host, metrics, new FracRect(0.02, 0.04, 0, 0), "app").ConfigureAwait(true);

        // [S-11889] An icon just below where the clock lands: its corners fade in with the grid like a card's.
        standing.Add(await IconAsync(layer, host, metrics, new FracRect(0.36, 0.6, 0, 0), "app").ConfigureAwait(true));

        // The marks on their plates over every card, at the strengths the locked move gives them.
        var markers = new EditMarkerLayer();
        var borders = LocalGrid.Borders(clock, [moving.Model.Id], standing.Select(c => (c.Model.Id, c.LiveRect)), metrics);
        foreach (var card in layer.Children.OfType<WidgetCard>().ToList())
        {
            markers.Add(card);
            card.ShowMoveMarks(borders.TryGetValue(card.Model.Id, out var alpha) ? alpha : null);
        }

        screen.Children.Add(markers.Halos);
        screen.Children.Add(markers.Inks);

        local.Show(clock, metrics, scale);
        await Task.Delay(700).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"drag: wrote {path}");
    }

    /// <summary>[S-11877] A mixed selection under the band: two icons selected (their look), a clock selected (its corners), one icon and one card not.</summary>
    private static async Task LassoAsync(Grid screen, Ticker ticker, ScreenMetrics metrics, string path)
    {
        screen.Children.Clear();
        DesktopCell.Apply(DesktopIconSize.Medium);
        var clock = WidgetInstance.Create(new WidgetId("clock"), GridGeometry.Settle(new FracRect(0.2, 0.08, 0.3, 0.24), metrics));
        var other = WidgetInstance.Create(new WidgetId("clock"), GridGeometry.Settle(new FracRect(0.62, 0.55, 0.3, 0.24), metrics));
        var selected = new HashSet<string>(StringComparer.Ordinal) { clock.Id };
        var host = new NoticeRender.RenderCardHost(ticker) { Selected = selected };
        var layer = new Canvas { Width = ScreenWidth, Height = ScreenHeight };
        screen.Children.Add(layer);

        Place(layer, new WidgetCard(host, clock), clock.Rect, metrics);
        Place(layer, new WidgetCard(host, other), other.Rect, metrics);
        // [S-11889] The selected icons in the desktop's one selection: their corners over their highlight.
        selected.Add((await IconAsync(layer, host, metrics, new FracRect(0.02, 0.04, 0, 0), "selected").ConfigureAwait(true)).Model.Id);
        selected.Add((await IconAsync(layer, host, metrics, new FracRect(0.02, 0.26, 0, 0), "ownSelected").ConfigureAwait(true)).Model.Id);
        await IconAsync(layer, host, metrics, new FracRect(0.02, 0.6, 0, 0), "app").ConfigureAwait(true);

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
    /// [S-13000] [S-13007] [S-13011] [S-13012] Fences on the locked desktop: one with two items selected among the free
    /// icons' selection (the desktop's one look in both), one rolled up to its title bar, and the rectangle a right-drag
    /// draws to make one, in the band's look.
    /// </summary>
    private static async Task FencesAsync(Grid screen, Ticker ticker, ScreenMetrics metrics, string path)
    {
        screen.Children.Clear();
        DesktopCell.Apply(DesktopIconSize.Medium);
        var host = new NoticeRender.RenderCardHost(ticker);
        var layer = new Canvas { Width = ScreenWidth, Height = ScreenHeight };
        screen.Children.Add(layer);

        await IconAsync(layer, host, metrics, new FracRect(0.02, 0.04, 0, 0), "thisPc").ConfigureAwait(true);
        await IconAsync(layer, host, metrics, new FracRect(0.02, 0.26, 0, 0), "selected").ConfigureAwait(true);
        await IconAsync(layer, host, metrics, new FracRect(0.02, 0.48, 0, 0), "recycleBin").ConfigureAwait(true);

        var work = GridGeometry.Settle(new FracRect(0.14, 0.04, 0.27, 0.46), metrics);
        await FenceAsync(layer, host, metrics, work, "selected").ConfigureAwait(true);
        var rolled = GridGeometry.Settle(new FracRect(0.45, 0.04, 0.25, 0.06), metrics);
        await FenceAsync(layer, host, metrics, rolled, "rolledUp").ConfigureAwait(true);
        var many = GridGeometry.Settle(new FracRect(0.73, 0.04, 0.25, 0.5), metrics);
        await FenceAsync(layer, host, metrics, many, "many").ConfigureAwait(true);

        // The rectangle a right-drag on empty desktop draws before "Create fence here".
        var band = new Border { Width = 0.3 * ScreenWidth, Height = 0.3 * ScreenHeight, IsHitTestVisible = false };
        SelectionChrome.PaintBand(band);
        Canvas.SetLeft(band, 0.4 * ScreenWidth);
        Canvas.SetTop(band, 0.62 * ScreenHeight);
        layer.Children.Add(band);

        await Task.Delay(900).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"drag: wrote {path}");
    }

    /// <summary>A fence at <paramref name="rect"/> in a preview state.</summary>
    private static async Task FenceAsync(Canvas layer, IWidgetCardHost host, ScreenMetrics metrics, FracRect rect, string state)
    {
        if (ModuleCatalog.Widgets.FirstOrDefault(d => typeof(IHoldsDesktopItems).IsAssignableFrom(d.ConfigType)) is not { } kind)
        {
            return;
        }

        var card = new WidgetCard(host, WidgetInstance.Create(kind.Id, rect));
        Place(layer, card, rect, metrics);
        if (card.View is IWidgetPreviewState preview)
        {
            await preview.ShowPreviewStateAsync(state).ConfigureAwait(true);
        }
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
        Place(layer, new WidgetCard(host, WidgetInstance.Create(new WidgetId("clock"), clockRect)), clockRect, metrics);

        var box = DesktopGrid.IconSize(DesktopCell.Grid(size), metrics);
        var packed = DesktopGrid.Pack([.. Enumerable.Range(0, 12).Select(i => new FracRect(i * 0.001, 0.9, box.W, box.H))], [clockRect], box, metrics);
        string[] states = ["thisPc", "recycleBin", "app", "web", "twoWords", "longName", "layout", "selected", "ownTwoWords", "hover", "focus", "app"];
        for (var i = 0; i < packed.Count; i++)
        {
            if (packed[i] is { } at)
            {
                await IconAsync(layer, host, metrics, at, states[i]).ConfigureAwait(true);
            }
        }

        // [S-11880] The surface's one join over them, as the desktop runs it: icons that touch stay free.
        WidgetCard.Join([.. layer.Children.OfType<WidgetCard>()], metrics, scale, editMode: false, _ => 0);

        await Task.Delay(900).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"drag: wrote {path}");
    }

    /// <summary>One desktop icon at <paramref name="at"/>'s corner, in its box on the grid, in a preview state.</summary>
    private static async Task<WidgetCard> IconAsync(Canvas layer, IWidgetCardHost host, ScreenMetrics metrics, FracRect at, string state)
    {
        var kind = ModuleCatalog.Widgets.First(d => typeof(IDesktopItemConfig).IsAssignableFrom(d.ConfigType));
        var (w, h) = DesktopGrid.IconSize(DesktopCell.Grid(DesktopCell.Current), metrics);
        var rect = GridGeometry.Settle(new FracRect(at.X, at.Y, w, h), metrics);
        rect = rect with { W = w, H = h };
        var card = new WidgetCard(host, WidgetInstance.Create(kind.Id, rect));
        Place(layer, card, rect, metrics);
        if (card.View is IWidgetPreviewState preview)
        {
            await preview.ShowPreviewStateAsync(state).ConfigureAwait(true);
        }

        return card;
    }

    private static void Place(Canvas layer, WidgetCard card, FracRect rect, ScreenMetrics metrics)
    {
        var cell = GridGeometry.ToPixels(rect, metrics);
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
