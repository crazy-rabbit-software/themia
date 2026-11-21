#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug only: draw a notice stack ([S-880]..[S-883]) and widget error cards ([S-891]/[S-890]) to
/// PNGs, off-screen. <c>Themia.exe --render notices &lt;outDir&gt; [--theme light|dark]</c>.
/// <para>
/// The notices are the real <see cref="NoticeCard"/>s, stacked as the surface stacks them in the
/// bottom-right corner of a stand-in work area above a taskbar strip. The error cards are real
/// <see cref="WidgetCard"/>s at the XS, S and M tiers: one failed (with Retry and
/// Details), one paused by safe mode.
/// </para>
/// </summary>
internal static class NoticeRender
{
    private const double ScreenWidth = 960;
    private const double ScreenHeight = 540;
    private const double TaskbarHeight = 48;

    public static RenderCommand Command { get; } = new(
        RenderVerb.Render,
        "notices",
        "<outDir> [--theme light|dark]",
        """
        a notice stack in the corner of a stand-in work area [S-880]..[S-883] and every shape a notice has [S-881a]
        (notices-stack.png), and widget error cards at
        XS, S and M, failed and paused by safe mode [S-891] [S-890] (error-cards.png), then retried (error-cards-retried.png)
        """,
        RunAsync);

    private static async Task<int> RunAsync(RenderRun run)
    {
        var outDir = run.PathArg(0);
        System.IO.Directory.CreateDirectory(outDir);

        using var harness = RenderHarness.Open(run, "Themia notice render", null, ScreenWidth, ScreenHeight);
        var screen = new Grid
        {
            Width = ScreenWidth,
            Height = ScreenHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = DesignApplier.Brush("SolidBackgroundFillColorSecondaryBrush", Colors.DimGray),
        };

        harness.Window.Window.Content = new Grid { Children = { screen } };
        UiLanguage.ApplyLanguageOnly(screen);

        DrawNotices(screen);
        await Task.Delay(800).ConfigureAwait(true);
        await RenderHarness.SaveAsync(screen, System.IO.Path.Combine(outDir, "notices-stack.png"), "notices").ConfigureAwait(true);

        var cards = DrawCards(screen, harness);
        await Task.Delay(800).ConfigureAwait(true);
        await RenderHarness.SaveAsync(screen, System.IO.Path.Combine(outDir, "error-cards.png"), "notices").ConfigureAwait(true);

        // Retry on a card whose view is fine: the real view comes back in its place.
        foreach (var card in cards)
        {
            card.Retry();
        }

        await Task.Delay(800).ConfigureAwait(true);
        await RenderHarness.SaveAsync(screen, System.IO.Path.Combine(outDir, "error-cards-retried.png"), "notices").ConfigureAwait(true);
        return RenderCommandLine.Passed;
    }

    /// <summary>
    /// The stack as a surface draws it: three notices, newest at the bottom, over the taskbar; at the left, [S-881a] every
    /// shape a notice has: a title alone, a title with its button on its line, a title over its detail, and the detail with
    /// its button under it (a sticky one).
    /// </summary>
    private static void DrawNotices(Grid screen)
    {
        screen.Children.Clear();
        screen.Children.Add(Taskbar());

        var layer = new StackPanel
        {
            Spacing = Fluent.Space2,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, Fluent.Space4, TaskbarHeight + Fluent.Space4),
        };

        Notice[] stack =
        [
            new(NoticeKind.Warning, "Free plan limit reached")
            {
                Message = "The free plan holds 20 widgets. Remove one, or upgrade to add more.",
                Action = new NoticeAction("See plans", () => { }),
            },
            new(NoticeKind.Error, "Default output not changed")
            {
                Message = "Windows did not allow changing the default output, so this widget now shows Speakers.",
            },
            new(NoticeKind.Info, "Deleted 3 widgets") { Action = new NoticeAction("Undo", () => { }) },
        ];

        foreach (var notice in stack)
        {
            layer.Children.Add(new NoticeCard(notice, _ => { }));
        }

        UiLanguage.ApplyFlow(layer);
        screen.Children.Add(layer);

        // [S-881a] Every shape, one under another: the same inset over and under each.
        var shapes = new StackPanel
        {
            Spacing = Fluent.Space2,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(Fluent.Space4),
        };
        Notice[] every =
        [
            Notice.Info("Copied 2 widgets"),
            new(NoticeKind.Info, "Deleted 3 widgets") { Action = new NoticeAction("Undo", () => { }) },
            new(NoticeKind.Success, "Settings reset") { Message = "Your previous settings are kept next to the settings file." },
            new(NoticeKind.Warning, "Widgets are paused")
            {
                Message = "Themia didn't finish starting the last few times. Start your widgets here, or reset your settings in Settings › Back up & reset.",
                Action = new NoticeAction("Start widgets", () => { }),
                Sticky = true,
            },
        ];

        foreach (var notice in every)
        {
            shapes.Children.Add(new NoticeCard(notice, _ => { }));
        }

        UiLanguage.ApplyFlow(shapes);
        screen.Children.Add(shapes);
    }

    /// <summary>Failed cards at XS, S and M, and a paused one at S.</summary>
    private static List<WidgetCard> DrawCards(Grid screen, RenderHarness harness)
    {
        screen.Children.Clear();
        var canvas = new Canvas();
        screen.Children.Add(canvas);

        var host = new RenderCardHost(harness.Ticker);
        var cards = new List<WidgetCard>();
        (double W, double H, bool Paused)[] sizes = [(120, 120, false), (200, 150, false), (320, 240, false), (200, 150, true)];
        var x = Fluent.Space4;

        foreach (var (w, h, paused) in sizes)
        {
            WidgetCard.StartPaused = paused;
            var card = harness.Track(new WidgetCard(host, WidgetInstance.Create(new WidgetId("clock"), new FracRect(0, 0, 0.1, 0.1)))
            {
                Width = w,
                Height = h,
            });
            WidgetCard.StartPaused = false;

            if (!paused)
            {
                card.FailForPreview(new InvalidOperationException("The clock could not read the time zone 'Mars/Olympus_Mons'."));
            }

            Canvas.SetLeft(card, x);
            Canvas.SetTop(card, Fluent.Space4 * 2);
            canvas.Children.Add(card);
            cards.Add(card);
            x += w + Fluent.Space4;
        }

        return cards;
    }

    private static Rectangle Taskbar() => new()
    {
        Height = TaskbarHeight,
        VerticalAlignment = VerticalAlignment.Bottom,
        Fill = DesignApplier.Brush("SolidBackgroundFillColorBaseBrush", Colors.Black),
    };

    /// <summary>A preview card host for the renders: the stand-in wallpaper's screen and backdrop while one is in use ([S-2119]), and the kit services' preview state. Also the motion render's ([S-1016]).</summary>
    internal sealed class RenderCardHost(Ticker ticker) : PreviewCardHost(ticker)
    {
        /// <summary>The render's screen: the stand-in wallpaper's while one is in use ([S-2119]).</summary>
        protected override ScreenMetrics? Screen => RenderWallpaper.Screen;

        /// <summary>[S-9929] The preview state this host's kit service cards replay; null for <c>items</c>.</summary>
        public string? FixtureState { get; set; }

        /// <summary>[S-2119] The stand-in wallpaper's backdrop while one is in use.</summary>
        public override Backdrop? Backdrop => RenderWallpaper.Backdrop;
    }
}
#endif
