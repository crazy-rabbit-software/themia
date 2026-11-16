#if DEBUG
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-5544] [S-5605] [S-6733] Debug only: a stand-in desktop of desktop icons, off-screen, to PNGs.
/// <c>Themia.exe --desktop-png &lt;outDir&gt;</c> writes <c>desktop-&lt;theme&gt;-&lt;wallpaper&gt;.png</c>
/// for both themes over a dark, a light and a busy stand-in wallpaper: twenty icons of both sources
/// mixed (the preview states, so no user's desktop is read), three columns of desktop items and two of
/// own icons, showing the rest, the hover, the selection (active and inactive), the focus, a drop
/// target, a cut and a hidden item, a name on two lines and one cut short, the rename box, and the
/// desktop's band (<see cref="SelectionChrome.PaintBand"/>) half over them.
/// [S-5593] <c>--icon-size small|medium|large</c> draws them at that desktop icon size (medium without
/// it), the files named <c>desktop-&lt;size&gt;-…</c> for the other two. [S-5610] Under the icons, the
/// tips some of them show, as boxes: a tip is a popup, which a render cannot catch.
/// </summary>
internal static class DesktopRender
{
    public const string Flag = "--desktop-png";

    private const string Scope = "desktop-render";
    private const double Cell = 8;
    private const double TipsHeight = 270;

    private static readonly (int Column, int Row, string State)[] Items =
    [
        (0, 0, "thisPc"), (0, 1, "recycleBin"), (0, 2, "cut"), (0, 3, "hidden"),
        (1, 0, "selected"), (1, 1, "focus"), (1, 2, "twoWords"), (1, 3, "longName"),
        (2, 0, "hover"), (2, 1, "inactive"), (2, 2, "dropTarget"), (2, 3, "editing"),
        (3, 0, "app"), (3, 1, "ownSelected"), (3, 2, "ownTwoWords"), (3, 3, "layout"),
        (4, 0, "web"), (4, 1, "ownInactive"), (4, 2, "ownDropTarget"), (4, 3, "ownFocus"),
    ];

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

        // [S-5593] The desktop's icon size for this render: the cell, the icons and their pixels follow it.
        var sizeAt = Array.IndexOf(args, "--icon-size");
        var size = DesktopIconSize.Medium;
        if (sizeAt >= 0 && (sizeAt + 1 >= args.Length || !Enum.TryParse(args[sizeAt + 1], ignoreCase: true, out size) || !Enum.IsDefined(size)))
        {
            Log.Error(Scope, "usage: --icon-size small|medium|large");
            app.Exit();
            return true;
        }

        DesktopCell.Apply(size);
        var grid = DesktopCell.Grid(size);
        var ScreenWidth = (2 * Cell) + (5 * grid.Width);
        var ScreenHeight = (2 * Cell) + (4 * grid.Height) + TipsHeight;
        var prefix = size == DesktopIconSize.Medium ? "desktop" : $"desktop-{size.ToString().ToLowerInvariant()}";
        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;
            Ticker? ticker = null;
            try
            {
                System.IO.Directory.CreateDirectory(outDir);
                var screen = new Grid { Width = ScreenWidth, Height = ScreenHeight, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                window = new Window { Content = new Grid { Children = { screen } }, Title = "Themia desktop render" };
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((ScreenWidth + 40) * scale), (int)((ScreenHeight + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);

                ticker = new Ticker(dispatcher);
                var host = new NoticeRender.RenderCardHost(ticker);
                var kind = ModuleCatalog.Widgets.First(d => typeof(IDesktopItemConfig).IsAssignableFrom(d.ConfigType));
                foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
                {
                    DesignApplier.Apply(new DesignState { Theme = theme });
                    screen.RequestedTheme = DesignApplier.Theme;
                    foreach (var (wallName, wall) in Wallpapers())
                    {
                        screen.Children.Clear();
                        screen.Background = wall;
                        var layer = new Canvas { Width = ScreenWidth, Height = ScreenHeight };
                        foreach (var (column, row, state) in Items)
                        {
                            var x = Cell + (column * kind.Natural.Width);
                            var y = Cell + (row * kind.Natural.Height);
                            var rect = new FracRect(x / ScreenWidth, y / ScreenHeight, kind.Natural.Width / ScreenWidth, kind.Natural.Height / ScreenHeight);
                            var card = new WidgetCard(host, WidgetInstance.Create(kind.Id, rect)) { Width = kind.Natural.Width, Height = kind.Natural.Height };
                            Canvas.SetLeft(card, x);
                            Canvas.SetTop(card, y);
                            layer.Children.Add(card);
                            if (card.View is IWidgetPreviewState preview)
                            {
                                await preview.ShowPreviewStateAsync(state).ConfigureAwait(true);
                            }
                        }

                        // [S-5544] The band from empty desktop, half over the first columns' middle rows.
                        var band = new Border { Width = 150, Height = 150, IsHitTestVisible = false };
                        SelectionChrome.PaintBand(band);
                        Canvas.SetLeft(band, Cell + 40);
                        Canvas.SetTop(band, Cell + 60);
                        layer.Children.Add(band);
                        // [S-5610] The tips, as the rule words them (DesktopTip): a file's, a folder's, a
                        // shortcut's and an own icon's on a Windows location; one whose name says it all has none.
                        var tips = new StackPanel { Spacing = Cell, MaxWidth = ScreenWidth - (2 * Cell) };
                        foreach (var tip in Tips())
                        {
                            tips.Children.Add(new ToolTip { Content = tip, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left });
                        }

                        Canvas.SetLeft(tips, Cell);
                        Canvas.SetTop(tips, Cell + (4 * grid.Height) + Cell);
                        layer.Children.Add(tips);
                        screen.Children.Add(layer);

                        await Task.Delay(900).ConfigureAwait(true);
                        var path = System.IO.Path.Combine(outDir, $"{prefix}-{(theme == AppTheme.Light ? "light" : "dark")}-{wallName}.png");
                        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
                        Console.WriteLine($"desktop: wrote {path}");

                        if (wallName != "busywall")
                        {
                            await RenderGhostsAsync(screen, layer, scale, System.IO.Path.Combine(outDir, $"{prefix}-ghost-{(theme == AppTheme.Light ? "light" : "dark")}-{wallName}.png")).ConfigureAwait(true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the desktop failed", ex);
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
    /// [S-5564] [S-5565] The ghost a drag shows, for 1, 2, 3 and 12 dragged icons, over the wallpaper:
    /// composed by the very call a drag makes (<see cref="ItemDrag.Ghost"/>) from the pictures the
    /// stand-in icons' views keep, each drawn at its own pixels with a small mark where the pointer holds
    /// it. Says how long composing the largest one took: the whole cost of the picture at a drag's start.
    /// </summary>
    private static async Task RenderGhostsAsync(Grid screen, Canvas desktop, double scale, string path)
    {
        var views = desktop.Children.OfType<WidgetCard>().Select(card => card.View).OfType<IDesktopIconView>().ToList();
        var icons = views.Select(view => view.DragIcon).OfType<Themia.Core.Files.GhostIcon>().ToList();
        if (icons.Count < Themia.Core.Files.DragGhost.MaxShown || views[0].DragIconBox is not { ActualWidth: > 0 } box)
        {
            Console.WriteLine($"desktop: only {icons.Count} of {views.Count} icons keep a picture for the ghost; none drawn");
            return;
        }

        var side = box.ActualWidth;
        var grab = new Point(side * 0.6, side * 0.55);
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 * Cell, Margin = new Thickness(2 * Cell), VerticalAlignment = VerticalAlignment.Top };
        var held = desktop.Visibility;
        foreach (var count in new[] { 1, 2, 3, 12 })
        {
            var ghost = ItemDrag.Ghost([.. icons.Take(Math.Min(count, Themia.Core.Files.DragGhost.MaxShown))], count, side, grab, scale);
            if (ghost is null)
            {
                Console.WriteLine($"desktop: no ghost for {count}");
                continue;
            }

            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap(ghost.Width, ghost.Height);
            System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.CopyTo(ghost.Bgra, bitmap.PixelBuffer);
            bitmap.Invalidate();
            var (hotX, hotY) = ghost.Anchor;
            var cell = new Grid { Width = ghost.Width / scale, Height = ghost.Height / scale, VerticalAlignment = VerticalAlignment.Top };
            cell.Children.Add(new Image { Source = bitmap, Stretch = Stretch.Fill });

            // The pointer's place: a render fixture, not design.
            cell.Children.Add(new Border
            {
                Width = 3, Height = 3, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(hotX - 1.5, hotY - 1.5, 0, 0), Background = new SolidColorBrush(Microsoft.UI.Colors.Red),
            });
            strip.Children.Add(cell);
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        const int Runs = 200;
        for (var i = 0; i < Runs; i++)
        {
            _ = ItemDrag.Ghost([.. icons.Take(Themia.Core.Files.DragGhost.MaxShown)], 12, side, grab, scale);
        }

        Console.WriteLine($"desktop: a ghost of 12 at {scale:0.##}x composes in {watch.Elapsed.TotalMilliseconds / Runs:0.000} ms (mean of {Runs})");

        desktop.Visibility = Visibility.Collapsed;
        screen.Children.Add(strip);
        await Task.Delay(300).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"desktop: wrote {path}");
        screen.Children.Remove(strip);
        desktop.Visibility = held;
    }

    /// <summary>[S-5610] The tips of four stand-in icons, worded by the rule; fixed facts, no disk read.</summary>
    private static IEnumerable<string> Tips()
    {
        var locale = Themia.Core.Localization.Strings.Current;
        var written = new DateTime(2026, 11, 14, 10, 42, 0);
        string?[] tips =
        [
            DesktopTip.Item(locale, new DesktopItem(@"C:\Users\Sample\Desktop\Budget.xlsx", "Budget", DesktopItemKind.File),
                new DesktopItemFacts("Microsoft Excel Worksheet", 48_640, written, written), nameCut: false),
            DesktopTip.Item(locale, new DesktopItem(@"C:\Users\Sample\Desktop\Projects", "Projects", DesktopItemKind.Folder),
                new DesktopItemFacts(null, null, written, written), nameCut: false),
            DesktopTip.Item(locale, new DesktopItem(@"C:\Users\Sample\Desktop\Mission Planner.lnk", "Mission Planner for the long weekend", DesktopItemKind.Shortcut),
                new DesktopItemFacts("Shortcut", 1_204, written, written, @"C:\Program Files\Mission Planner\MissionPlanner.exe"), nameCut: true),
            DesktopTip.Own(locale, "Papers", Themia.Core.Widgets.LaunchTargets.Classify("shell:Personal"), nameCut: false),
        ];
        return tips.OfType<string>();
    }

    /// <summary>Stand-in wallpapers, a dark wash, a light one and a busy one: render fixtures, not design colours.</summary>
    private static IEnumerable<(string Name, Brush Wall)> Wallpapers()
    {
        yield return ("darkwall", Wash((0, Windows.UI.Color.FromArgb(255, 30, 52, 84)), (1, Windows.UI.Color.FromArgb(255, 92, 58, 80))));
        yield return ("lightwall", Wash((0, Windows.UI.Color.FromArgb(255, 236, 240, 246)), (1, Windows.UI.Color.FromArgb(255, 250, 238, 226))));
        yield return ("busywall", Wash(
            (0.00, Windows.UI.Color.FromArgb(255, 250, 250, 245)), (0.12, Windows.UI.Color.FromArgb(255, 40, 120, 60)),
            (0.25, Windows.UI.Color.FromArgb(255, 255, 220, 120)), (0.40, Windows.UI.Color.FromArgb(255, 120, 180, 230)),
            (0.55, Windows.UI.Color.FromArgb(255, 245, 245, 245)), (0.70, Windows.UI.Color.FromArgb(255, 200, 80, 60)),
            (0.85, Windows.UI.Color.FromArgb(255, 230, 230, 200)), (1.00, Windows.UI.Color.FromArgb(255, 60, 60, 70))));
    }

    private static LinearGradientBrush Wash(params (double Offset, Windows.UI.Color Color)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        foreach (var (offset, color) in stops)
        {
            brush.GradientStops.Add(new GradientStop { Color = color, Offset = offset });
        }

        return brush;
    }
}
#endif
