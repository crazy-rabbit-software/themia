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
/// [S-2107] Debug only: a fixture surface of touching and separated widgets, grouped and drawn by the
/// real <see cref="CardGroupLayer"/> under real cards, to PNGs, off-screen.
/// <c>Themia.exe --groups-png &lt;outDir&gt; [--theme light|dark]</c>. One PNG per look: the default
/// design, no gap, a dashed border with a radius of 16, and translucent cards.
/// </summary>
internal static class GroupRender
{
    private const string Scope = "group-render";
    private const string Flag = "--groups-png";

    private const double ScreenWidth = 960;
    private const double ScreenHeight = 540;

    /// <summary>
    /// The fixture, in DIPs on a 960x540 screen (10 DIP snap cells): a touching pair, an L, a pair one
    /// empty cell apart, a staircase of three, and a card beside a transparent widget.
    /// </summary>
    private static readonly (WidgetKind Kind, double X, double Y, double W, double H, WidgetSurface Surface)[] Scene =
    [
        (WidgetKind.Clock, 20, 20, 200, 140, WidgetSurface.Card),
        (WidgetKind.Cpu, 220, 20, 160, 140, WidgetSurface.Card),

        (WidgetKind.Memory, 20, 190, 240, 130, WidgetSurface.Card),
        (WidgetKind.Notes, 20, 320, 130, 200, WidgetSurface.Card),

        (WidgetKind.Countdown, 420, 20, 180, 140, WidgetSurface.Card),
        (WidgetKind.Cpu, 610, 20, 180, 140, WidgetSurface.Card),

        (WidgetKind.Cpu, 300, 210, 160, 120, WidgetSurface.Card),
        (WidgetKind.Memory, 460, 210, 160, 120, WidgetSurface.Card),
        (WidgetKind.Clock, 460, 330, 220, 120, WidgetSurface.Card),

        (WidgetKind.Clock, 720, 210, 200, 120, WidgetSurface.Card),
        (WidgetKind.Clock, 720, 330, 200, 120, WidgetSurface.Transparent),
    ];

    /// <summary>The application theme can only be chosen before the resources load: read by <c>App</c>.</summary>
    public static ApplicationTheme? RequestedTheme(string[] args)
    {
        if (Array.IndexOf(args, Flag) < 0)
        {
            return null;
        }

        return Option(args, "--theme")?.ToLowerInvariant() switch
        {
            "light" => ApplicationTheme.Light,
            "dark" => ApplicationTheme.Dark,
            _ => null,
        };
    }

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, $"usage: {Flag} <outDir> [--theme light|dark]");
            Console.WriteLine($"usage: {Flag} <outDir> [--theme light|dark]");
            app.Exit();
            return true;
        }

        var outDir = System.IO.Path.GetFullPath(args[at + 1]);
        var themeName = RequestedTheme(args) == ApplicationTheme.Light ? "light" : "dark";

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;
            Ticker? ticker = null;
            var cards = new List<WidgetCard>();

            try
            {
                System.IO.Directory.CreateDirectory(outDir);

                ticker = new Ticker(dispatcher);
                var host = new NoticeRender.RenderCardHost(ticker);
                var design = host.Design;
                design.Theme = themeName == "light" ? AppTheme.Light : AppTheme.Dark;
                DesignApplier.Apply(design);

                var screen = new Grid
                {
                    Width = ScreenWidth,
                    Height = ScreenHeight,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,

                    // A stand-in wallpaper, so a gap, a translucent card and the outside of a corner read.
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1),
                        GradientStops =
                        {
                            new GradientStop { Color = Windows.UI.Color.FromArgb(255, 38, 70, 110), Offset = 0 },
                            new GradientStop { Color = Windows.UI.Color.FromArgb(255, 160, 110, 80), Offset = 1 },
                        },
                    },
                };

                var groups = new CardGroupLayer();
                var layer = new Canvas { Width = ScreenWidth, Height = ScreenHeight };
                screen.Children.Add(groups.Element);
                screen.Children.Add(layer);

                window = new Window { Content = new Grid { Children = { screen } }, Title = "Themia group render" };
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((ScreenWidth + 40) * scale), (int)((ScreenHeight + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);

                var metrics = new ScreenMetrics(ScreenWidth, ScreenHeight);
                foreach (var (kind, x, y, w, h, surface) in Scene)
                {
                    var rect = new FracRect(x / ScreenWidth, y / ScreenHeight, w / ScreenWidth, h / ScreenHeight);
                    var model = WidgetInstance.Create(kind, rect);
                    model.Surface = surface;
                    var card = new WidgetCard(host, model);
                    card.SetLiveRect(rect);
                    layer.Children.Add(card);
                    cards.Add(card);
                }

                void Layout()
                {
                    foreach (var card in cards)
                    {
                        var cell = GridGeometry.ToPixels(card.LiveRect, metrics);
                        Canvas.SetLeft(card, cell.X);
                        Canvas.SetTop(card, cell.Y);
                        card.Width = cell.Width;
                        card.Height = cell.Height;
                        card.ApplyGap(design.WidgetGapPx);
                        card.ApplyDesign();
                    }

                    groups.Apply(cards, metrics, design, enabled: true);
                }

                async Task Save(string look)
                {
                    Layout();
                    await Task.Delay(400).ConfigureAwait(true);
                    var path = System.IO.Path.Combine(outDir, $"groups-{themeName}-{look}.png");
                    await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
                    Console.WriteLine($"groups: wrote {path} ({groups.Region.Count} region rects)");
                }

                await Task.Delay(800).ConfigureAwait(true);
                await Save("default").ConfigureAwait(true);

                design.WidgetGapPx = 0;
                await Save("nogap").ConfigureAwait(true);

                design.WidgetGapPx = 12;
                design.CornerRadiusPx = 16;
                design.BorderStyle = WidgetBorderStyle.Dashed;
                design.BorderWidthPx = 2;
                design.BorderColor = "#FFB000";
                await Save("dashed").ConfigureAwait(true);

                design.WidgetGapPx = 8;
                design.CornerRadiusPx = 8;
                design.BorderStyle = WidgetBorderStyle.Solid;
                design.BorderWidthPx = 1;
                design.BorderColor = string.Empty;
                design.SurfaceOpacity = 0.6;
                design.HeaderBackgroundColor = "#3060C0";
                design.HeaderOpacity = 0.8;
                await Save("translucent-header").ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the groups failed", ex);
                Console.WriteLine($"groups: failed: {ex.Message}");
            }
            finally
            {
                foreach (var card in cards)
                {
                    card.Detach();
                }

                ticker?.Dispose();
                window?.Close();
                app.Exit();
            }
        });

        return true;
    }

    private static string? Option(string[] args, string name)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }
}
#endif
