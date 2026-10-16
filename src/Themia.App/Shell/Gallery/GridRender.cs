#if DEBUG
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-06c] Debug only: the edit-mode snap grid (the real <see cref="SnapGridOverlay"/>) on a stand-in
/// display, off-screen, to PNGs. <c>Themia.exe --grid-png &lt;outDir&gt;</c> writes
/// <c>grid-&lt;theme&gt;-&lt;wallpaper&gt;.png</c> for both themes over a dark and a light stand-in
/// wallpaper, each with a clock snapped to the grid, at the window's own DPI.
/// </summary>
internal static class GridRender
{
    private const string Scope = "grid-render";
    private const string Flag = "--grid-png";

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

                var screen = new Grid
                {
                    Width = ScreenWidth,
                    Height = ScreenHeight,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                };

                window = new Window { Content = new Grid { Children = { screen } }, Title = "Themia grid render" };
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((ScreenWidth + 40) * scale), (int)((ScreenHeight + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);

                ticker = new Ticker(dispatcher);
                var host = new NoticeRender.RenderCardHost(ticker);
                var metrics = new ScreenMetrics(ScreenWidth, ScreenHeight);

                foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
                {
                    DesignApplier.Apply(new DesignState { Theme = theme });
                    screen.RequestedTheme = DesignApplier.Theme;

                    foreach (var (wallName, wall) in Wallpapers())
                    {
                        screen.Children.Clear();
                        screen.Background = wall;

                        var grid = new SnapGridOverlay();
                        grid.Draw(metrics, scale);
                        screen.Children.Add(grid);

                        // A widget on the grid, so the major lines can be judged against a snapped edge.
                        var rect = GridGeometry.Settle(new FracRect(0.1, 0.18, 0.3, 0.3), metrics, snapEnabled: true);
                        var cell = GridGeometry.ToPixels(rect, metrics);
                        var layer = new Canvas { Width = ScreenWidth, Height = ScreenHeight };
                        var card = new WidgetCard(host, WidgetInstance.Create(WidgetKind.Clock, rect)) { Width = cell.Width, Height = cell.Height };
                        Canvas.SetLeft(card, cell.X);
                        Canvas.SetTop(card, cell.Y);
                        layer.Children.Add(card);
                        screen.Children.Add(layer);

                        await Task.Delay(600).ConfigureAwait(true);
                        var name = $"grid-{(theme == AppTheme.Light ? "light" : "dark")}-{wallName}.png";
                        var path = System.IO.Path.Combine(outDir, name);
                        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
                        Console.WriteLine($"grid: wrote {path}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the grid failed", ex);
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
