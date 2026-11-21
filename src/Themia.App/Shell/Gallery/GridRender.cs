#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-06c] Debug only: the edit-mode snap grid (the real <see cref="SnapGridOverlay"/>) on a stand-in
/// display, off-screen, to PNGs. <c>Themia.exe --render grid &lt;outDir&gt;</c> writes
/// <c>grid-&lt;theme&gt;-&lt;wallpaper&gt;.png</c> for both themes over a dark and a light stand-in
/// wallpaper, each with a clock snapped to the grid, at the window's own DPI.
/// </summary>
internal static class GridRender
{
    private const double ScreenWidth = 960;
    private const double ScreenHeight = 540;

    public static RenderCommand Command { get; } = new(
        RenderVerb.Render,
        "grid",
        "<outDir>",
        """
        the edit-mode snap grid alone (every 5th line heavier) with a clock snapped to it, both themes over a dark and a light
        wallpaper, at the window's own DPI (grid-<theme>-<wall>.png) [S-06c]
        """,
        RunAsync);

    private static async Task<int> RunAsync(RenderRun run)
    {
        var outDir = run.PathArg(0);
        System.IO.Directory.CreateDirectory(outDir);

        var screen = new Grid
        {
            Width = ScreenWidth,
            Height = ScreenHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        using var harness = RenderHarness.Open(run, "Themia grid render", new Grid { Children = { screen } }, ScreenWidth, ScreenHeight);
        var scale = harness.Window.Scale;
        var host = new NoticeRender.RenderCardHost(harness.Ticker);
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
                var rect = GridGeometry.Settle(new FracRect(0.1, 0.18, 0.3, 0.3), metrics);
                var cell = GridGeometry.PaintedCell(rect, metrics, scale);
                var layer = new Canvas { Width = ScreenWidth, Height = ScreenHeight };
                var card = harness.Track(new WidgetCard(host, WidgetInstance.Create(new WidgetId("clock"), rect)) { Width = cell.Width, Height = cell.Height });
                Canvas.SetLeft(card, cell.X);
                Canvas.SetTop(card, cell.Y);
                layer.Children.Add(card);
                screen.Children.Add(layer);

                await Task.Delay(600).ConfigureAwait(true);
                var name = $"grid-{(theme == AppTheme.Light ? "light" : "dark")}-{wallName}.png";
                await RenderHarness.SaveAsync(screen, System.IO.Path.Combine(outDir, name), "grid").ConfigureAwait(true);
            }
        }

        return RenderCommandLine.Passed;
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
