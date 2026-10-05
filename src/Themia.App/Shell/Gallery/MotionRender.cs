#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Shell.Motion;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Motion;
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1016] Debug only: freeze intros mid-flight and draw them to PNGs, off-screen.
/// <c>Themia.exe --motion-png &lt;outDir&gt;</c>.
/// <para>
/// A render target cannot see compositor animations, so each frame is posed from the same Core data
/// the compositor plays (<see cref="IntroTracks"/>, <see cref="Hyperspace"/>): real cards on a
/// stand-in screen, every card at its own point of the cascade. The hacker frame uses the real tiles
/// through the real card hook. What this proves is the choreography — where every widget, star and
/// tile is at a given moment — not the compositor's playback, which only the live app shows.
/// </para>
/// </summary>
internal static class MotionRender
{
    private const string Scope = "motion-render";

    private const double ScreenWidth = 960;
    private const double ScreenHeight = 540;
    private const double IntroMs = 500;
    private const double Gap = 8;

    private static readonly (WidgetKind Kind, FracRect Rect)[] Scene =
    [
        (WidgetKind.Clock, new FracRect(0.03, 0.05, 0.22, 0.28)),
        (WidgetKind.Cpu, new FracRect(0.27, 0.05, 0.2, 0.28)),
        (WidgetKind.Memory, new FracRect(0.03, 0.36, 0.22, 0.3)),
        (WidgetKind.Calculator, new FracRect(0.62, 0.08, 0.17, 0.5)),
        (WidgetKind.Countdown, new FracRect(0.8, 0.08, 0.17, 0.25)),
        (WidgetKind.Notes, new FracRect(0.55, 0.62, 0.42, 0.3)),
    ];

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, "--motion-png");
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, "usage: --motion-png <outDir>");
            app.Exit();
            return true;
        }

        var outDir = System.IO.Path.GetFullPath(args[at + 1]);

        _ = dispatcher.TryEnqueue(async () =>
        {
            Window? window = null;
            Ticker? ticker = null;
            var cards = new List<WidgetCard>();

            try
            {
                System.IO.Directory.CreateDirectory(outDir);

                // A stand-in wallpaper, so the veil and the fades read against something.
                var screen = new Grid
                {
                    Width = ScreenWidth,
                    Height = ScreenHeight,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(1, 1),
                        GradientStops =
                        {
                            new GradientStop { Color = Windows.UI.Color.FromArgb(255, 38, 70, 110), Offset = 0 },
                            new GradientStop { Color = Windows.UI.Color.FromArgb(255, 120, 72, 96), Offset = 1 },
                        },
                    },
                };

                var stars = new Canvas { Width = ScreenWidth, Height = ScreenHeight, Clip = new RectangleGeometry { Rect = new Rect(0, 0, ScreenWidth, ScreenHeight) } };
                var veil = new Rectangle { Fill = new SolidColorBrush(Colors.Black), Opacity = 0 };
                var layer = new Canvas { Width = ScreenWidth, Height = ScreenHeight };
                screen.Children.Add(veil);
                screen.Children.Add(stars);
                screen.Children.Add(layer);

                window = new Window { Content = new Grid { Children = { screen } }, Title = "Themia motion render" };
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((ScreenWidth + 40) * scale), (int)((ScreenHeight + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);

                ticker = new Ticker(dispatcher);
                var host = new NoticeRender.RenderCardHost(ticker);

                foreach (var (kind, rect) in Scene)
                {
                    var card = new WidgetCard(host, WidgetInstance.Create(kind, rect))
                    {
                        Width = rect.W * ScreenWidth,
                        Height = rect.H * ScreenHeight,
                    };

                    card.ApplyGap(Gap);
                    Canvas.SetLeft(card, rect.X * ScreenWidth);
                    Canvas.SetTop(card, rect.Y * ScreenHeight);
                    layer.Children.Add(card);
                    cards.Add(card);
                }

                await Task.Delay(1200).ConfigureAwait(true);

                async Task Save(string name)
                {
                    await Task.Delay(150).ConfigureAwait(true);
                    var path = System.IO.Path.Combine(outDir, name + ".png");
                    await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
                    Console.WriteLine($"motion: wrote {path}");
                }

                await Save("rest").ConfigureAwait(true);

                // The keyframe styles, 250 ms into a 500 ms intro: the cascade shows across the cards.
                foreach (var style in new[] { IntroAnimation.Fade, IntroAnimation.Scale, IntroAnimation.Slide, IntroAnimation.Sides, IntroAnimation.TopBottom, IntroAnimation.Flip, IntroAnimation.Blur })
                {
                    PoseCards(cards, style, IntroMode.Startup, 250);
                    await Save($"{style}-250ms").ConfigureAwait(true);
                }

                // Sides early, so the two sheets are visibly travelling as blocks.
                PoseCards(cards, IntroAnimation.Sides, IntroMode.Startup, 60);
                await Save("Sides-60ms").ConfigureAwait(true);

                // Hyperspace: the static field, the tease, cruise streaks, the crash, the veil lifting.
                foreach (var ms in new[] { 600.0, 2300, 2900, 3450, 3800 })
                {
                    PoseHyperspace(cards, layer, veil, stars, ms);
                    await Save($"Hyperspace-{ms:0}ms").ConfigureAwait(true);
                }

                Reset(cards, layer, veil, stars);

                // Hacker: the real tiles, through the real hook, before any has started to fade.
                for (var i = 0; i < cards.Count; i++)
                {
                    var tile = HackerTile.Build(i, cards[i].Width - Gap, cards[i].Height - Gap, 8);
                    cards[i].SetMotionCover(tile.Tile);
                }

                await Save("Hacker-covered").ConfigureAwait(true);

                foreach (var card in cards)
                {
                    card.SetMotionCover(null);
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the intros failed", ex);
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

    /// <summary>
    /// Every card as it is <paramref name="ms"/> into the intro, after its own share of the cascade
    /// ([S-1760]). Hyperspace is posed on its natural clock (a show <see cref="Hyperspace.NaturalMs"/>
    /// long), so the moments below are the warp's own.
    /// </summary>
    private static void PoseCards(List<WidgetCard> cards, IntroAnimation style, IntroMode mode, double ms)
    {
        var tracks = IntroTracks.For(style)!;
        var schedule = IntroSchedule.For(style, mode, style == IntroAnimation.Hyperspace ? Hyperspace.NaturalMs : IntroMs, cards.Count);
        SheetOffsets? sheets = IntroSheets.AxisFor(style) is { } axis
            ? IntroSheets.Offsets(Scene.Select(s => s.Rect), axis, axis == SheetAxis.X ? ScreenWidth : ScreenHeight)
            : null;

        for (var i = 0; i < cards.Count; i++)
        {
            var rect = Scene[i].Rect;
            var start = style switch
            {
                IntroAnimation.Hyperspace => Hyperspace.FromOffset(rect, ScreenWidth, ScreenHeight),
                _ when sheets is { } s && IntroSheets.AxisFor(style) is { } a => IntroSheets.StartFor(rect, a, s),
                _ => (0.0, 0.0),
            };

            var pose = tracks.At((ms - schedule.DelayMs(i)) / schedule.WidgetMs, start);
            Pose(cards[i], tracks, pose);
        }
    }

    private static void Pose(WidgetCard card, IntroTracks tracks, IntroPose pose)
    {
        card.Opacity = pose.Opacity;
        card.RenderTransformOrigin = new Point(0.5, 0.5);

        if (tracks.Axis == IntroAxis.Tilt)
        {
            card.RenderTransform = new CompositeTransform { TranslateX = pose.X, TranslateY = pose.Y, ScaleX = pose.Scale, ScaleY = pose.Scale };
            card.Projection = new PlaneProjection { RotationX = pose.Rotation, CenterOfRotationY = 0 };
            return;
        }

        card.Projection = null;
        card.RenderTransform = new CompositeTransform
        {
            TranslateX = pose.X,
            TranslateY = pose.Y,
            ScaleX = pose.Scale,
            ScaleY = pose.Scale,
            Rotation = pose.Rotation,
        };
    }

    private static void PoseHyperspace(List<WidgetCard> cards, Canvas layer, Rectangle veil, Canvas stars, double ms)
    {
        PoseCards(cards, IntroAnimation.Hyperspace, IntroMode.Startup, ms);

        // The veil holds black to the jump and lifts across the deceleration.
        veil.Opacity = ms <= Hyperspace.JumpMs ? 1 : 1 - IntroTracks.Eased(MotionEase.Settle, (ms - Hyperspace.JumpMs) / Hyperspace.DecelMs);

        // The layer takes the shake.
        var shake = Hyperspace.Shake;
        var f = (ms - Hyperspace.StaticMs) / Hyperspace.ShakeMs;
        layer.RenderTransform = new CompositeTransform
        {
            TranslateX = IntroTracks.Sample([.. shake.Select(k => new MotionKey(k.At, k.X, MotionEase.Linear))], f, 0),
            TranslateY = IntroTracks.Sample([.. shake.Select(k => new MotionKey(k.At, k.Y, MotionEase.Linear))], f, 0),
            Rotation = IntroTracks.Sample([.. shake.Select(k => new MotionKey(k.At, k.Degrees, MotionEase.Linear))], f, 0),
            CenterX = ScreenWidth / 2,
            CenterY = ScreenHeight / 2,
        };

        // The field, from the same closed form the compositor expressions evaluate.
        stars.Children.Clear();
        var radius = Hyperspace.Radius(ScreenWidth, ScreenHeight);
        var sample = Hyperspace.Samples(25, ms)[^1];
        var cx = ScreenWidth / 2;
        var cy = ScreenHeight * Hyperspace.CentreYFraction;

        foreach (var star in Hyperspace.Stars(Hyperspace.StarCount, 1977, radius))
        {
            var d = Hyperspace.Distance(star, sample.Travel, radius);
            var a = Hyperspace.Angle(star, sample.Travel, radius);
            var depth = d / radius;
            var trail = Hyperspace.Streak(star, sample.Level, d, radius);
            var (r, g, b) = Hyperspace.Colour(star, radius);
            stars.Children.Add(new Line
            {
                X1 = cx + (Math.Cos(a) * d),
                Y1 = cy + (Math.Sin(a) * d),
                X2 = cx + (Math.Cos(a) * (d - trail)),
                Y2 = cy + (Math.Sin(a) * (d - trail)),
                Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b)),
                StrokeThickness = Hyperspace.Thickness(sample.Level, depth),
                Opacity = Hyperspace.Alpha(star, sample.Level, depth, ms),
            });
        }
    }

    private static void Reset(List<WidgetCard> cards, Canvas layer, Rectangle veil, Canvas stars)
    {
        foreach (var card in cards)
        {
            card.Opacity = 1;
            card.RenderTransform = null;
            card.Projection = null;
        }

        layer.RenderTransform = null;
        veil.Opacity = 0;
        stars.Children.Clear();
    }
}
#endif
