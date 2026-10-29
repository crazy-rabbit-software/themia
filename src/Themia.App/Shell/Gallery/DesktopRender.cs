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
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-5544] [S-5605] [S-6733] Debug only: a stand-in desktop of item widgets, off-screen, to PNGs.
/// <c>Themia.exe --desktop-png &lt;outDir&gt;</c> writes <c>desktop-&lt;theme&gt;-&lt;wallpaper&gt;.png</c>
/// for both themes over a dark, a light and a busy stand-in wallpaper: six item widgets (the preview
/// states, so no user's desktop is read) with two selected, one renaming, one cut, and the desktop's
/// band (<see cref="SelectionChrome.PaintBand"/>) half over them.
/// </summary>
internal static class DesktopRender
{
    public const string Flag = "--desktop-png";

    private const string Scope = "desktop-render";
    private const double ScreenWidth = 400;
    private const double ScreenHeight = 360;
    private const double Cell = 8;

    private static readonly (int Column, int Row, string Key, string State)[] Items =
    [
        (0, 0, "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "thisPc"),
        (0, 1, "::{645FF040-5081-101B-9F08-00AA002F954E}", "recycleBin"),
        (0, 2, @"C:\Users\Public\Desktop\Notes.lnk", "cut"),
        (1, 0, @"C:\Users\Public\Desktop\Holiday photos", "selected"),
        (1, 1, @"C:\Users\Public\Desktop\Report.docx", "focus"),
        (1, 2, @"C:\Users\Public\Desktop\Quarterly budget review with all the departments 2026.xlsx", "longName"),
        (2, 0, @"C:\Users\Public\Desktop\Report.docx", "editing"),
        (2, 1, @"C:\Users\Public\Desktop\Holiday photos", "dropTarget"),
        (2, 2, @"C:\Users\Public\Desktop\desktop notes.txt", "hidden"),
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
                        foreach (var (column, row, key, state) in Items)
                        {
                            var x = Cell + (column * kind.DefaultWidth);
                            var y = Cell + (row * kind.DefaultHeight);
                            var rect = new FracRect(x / ScreenWidth, y / ScreenHeight, kind.DefaultWidth / ScreenWidth, kind.DefaultHeight / ScreenHeight);
                            var item = new DesktopItem(key, DesktopItemKeys.FallbackName(key), DesktopItemKind.File);
                            var card = new WidgetCard(host, AppHost.NewItemWidget(kind.Id, item, rect)) { Width = kind.DefaultWidth, Height = kind.DefaultHeight };
                            Canvas.SetLeft(card, x);
                            Canvas.SetTop(card, y);
                            layer.Children.Add(card);
                            if (card.View is IWidgetPreviewState preview)
                            {
                                await preview.ShowPreviewStateAsync(state).ConfigureAwait(true);
                            }
                        }

                        // [S-5544] The band from empty desktop, half over the first two columns' middle row.
                        var band = new Border { Width = 150, Height = 150, IsHitTestVisible = false };
                        SelectionChrome.PaintBand(band);
                        Canvas.SetLeft(band, Cell + 40);
                        Canvas.SetTop(band, Cell + 60);
                        layer.Children.Add(band);
                        screen.Children.Add(layer);

                        await Task.Delay(900).ConfigureAwait(true);
                        var path = System.IO.Path.Combine(outDir, $"desktop-{(theme == AppTheme.Light ? "light" : "dark")}-{wallName}.png");
                        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
                        Console.WriteLine($"desktop: wrote {path}");
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
