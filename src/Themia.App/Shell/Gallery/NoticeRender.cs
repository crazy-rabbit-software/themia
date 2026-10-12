#if DEBUG
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Windows.Graphics;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// Debug only: draw a notice stack ([S-880]..[S-883]) and widget error cards ([S-891]/[S-890]) to
/// PNGs, off-screen. <c>Themia.exe --notices-png &lt;outDir&gt;</c>.
/// <para>
/// The notices are the real <see cref="NoticeCard"/>s, stacked as the surface stacks them in the
/// bottom-right corner of a stand-in work area above a taskbar strip. The error cards are real
/// <see cref="WidgetCard"/>s at the XS, S and M tiers: one failed (with Retry and
/// Details), one paused by safe mode.
/// </para>
/// </summary>
internal static class NoticeRender
{
    private const string Scope = "notice-render";

    private const double ScreenWidth = 960;
    private const double ScreenHeight = 540;
    private const double TaskbarHeight = 48;

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, "--notices-png");
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, "usage: --notices-png <outDir>");
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
                    Background = DesignApplier.Brush("SolidBackgroundFillColorSecondaryBrush", Colors.DimGray),
                };

                window = new Window { Content = new Grid { Children = { screen } }, Title = "Themia notice render" };
                UiLanguage.ApplyLanguageOnly(screen);
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                var dpi = Win32.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96.0 : 1.0;

                // Off-screen and never activated: it must not appear on the user's desktop.
                window.AppWindow.Move(new PointInt32(-32000, -32000));
                window.AppWindow.Resize(new SizeInt32((int)((ScreenWidth + 40) * scale), (int)((ScreenHeight + 80) * scale)));
                window.AppWindow.Show(activateWindow: false);

                DrawNotices(screen);
                await Task.Delay(800).ConfigureAwait(true);
                var notices = System.IO.Path.Combine(outDir, "notices-stack.png");
                await RenderPng.SaveAsync(screen, notices).ConfigureAwait(true);
                Console.WriteLine($"notices: wrote {notices}");

                ticker = new Ticker(dispatcher);
                var cards = DrawCards(screen, ticker);
                await Task.Delay(800).ConfigureAwait(true);
                var errors = System.IO.Path.Combine(outDir, "error-cards.png");
                await RenderPng.SaveAsync(screen, errors).ConfigureAwait(true);
                Console.WriteLine($"notices: wrote {errors}");

                // Retry on a card whose view is fine: the real view comes back in its place.
                foreach (var card in cards)
                {
                    card.Retry();
                }

                await Task.Delay(800).ConfigureAwait(true);
                var retried = System.IO.Path.Combine(outDir, "error-cards-retried.png");
                await RenderPng.SaveAsync(screen, retried).ConfigureAwait(true);
                Console.WriteLine($"notices: wrote {retried}");

                foreach (var card in cards)
                {
                    card.Detach();
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "rendering the notices failed", ex);
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

    /// <summary>The stack as a surface draws it: three notices, newest at the bottom, over the taskbar.</summary>
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
                Message = "The free plan holds 10 widgets. Remove one, or upgrade to add more.",
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

        var single = new NoticeCard(new Notice(NoticeKind.Success, "Settings reset") { Message = "Your previous settings are kept next to the settings file." }, _ => { })
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(Fluent.Space4),
        };
        UiLanguage.ApplyFlow(single);
        screen.Children.Add(single);

        var safe = new NoticeCard(
            new Notice(NoticeKind.Warning, "Widgets are paused")
            {
                Message = "Themia didn't finish starting the last few times. Start your widgets here, or reset your settings in Settings › About.",
                Action = new NoticeAction("Start widgets", () => { }),
                Sticky = true,
            },
            _ => { })
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(Fluent.Space4, 120, 0, 0),
        };
        UiLanguage.ApplyFlow(safe);
        screen.Children.Add(safe);
    }

    /// <summary>Failed cards at XS, S and M, and a paused one at S.</summary>
    private static List<WidgetCard> DrawCards(Grid screen, Ticker ticker)
    {
        screen.Children.Clear();
        var canvas = new Canvas();
        screen.Children.Add(canvas);

        var host = new RenderCardHost(ticker);
        var cards = new List<WidgetCard>();
        (double W, double H, bool Paused)[] sizes = [(120, 120, false), (200, 150, false), (320, 240, false), (200, 150, true)];
        var x = Fluent.Space4;

        foreach (var (w, h, paused) in sizes)
        {
            WidgetCard.StartPaused = paused;
            var card = new WidgetCard(host, WidgetInstance.Create(WidgetKind.Clock, new FracRect(0, 0, 0.1, 0.1)))
            {
                Width = w,
                Height = h,
            };
            WidgetCard.StartPaused = false;

            card.ApplyGap(0);
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

    /// <summary>A card host with nothing behind it: every request is accepted and ignored. Also the motion render's ([S-1016]).</summary>
    internal sealed class RenderCardHost(Ticker ticker) : IWidgetCardHost
    {
        public ScreenMetrics Metrics { get; } = new(1920, 1080);

        public DesignState Design { get; } = new();

        public Ticker Ticker => ticker;

        /// <summary>[S-3713] Edit mode, for the properties strip's render; false elsewhere.</summary>
        public bool EditMode { get; init; }

        /// <summary>[S-3713] The ids drawn selected; none elsewhere.</summary>
        public IReadOnlySet<string> Selected { get; init; } = new HashSet<string>();

        public ViewState? ViewForCards => null;

        /// <summary>[S-3300] Every card of a render is on one stand-in view, so a linked preview state shows on each.</summary>
        public Themia.Core.Links.LinkBoard Links { get; } = new();

        public bool CanPaste => false;

        public bool IsPreview => true;

        public void OnGeometryPreview(WidgetCard card)
        {
            // ignore: a render does not move cards
        }

        public void OnGeometryCommitted(WidgetCard card)
        {
            // ignore: as above
        }

        public void RequestConfigure(WidgetCard card)
        {
            // ignore: no settings window here
        }

        public void RequestDelete(WidgetCard card)
        {
            // ignore: a fixed set of cards
        }

        public void RequestEditMode(bool enabled)
        {
            // ignore: a render's edit mode is fixed
        }

        public bool IsSelected(WidgetCard card) => Selected.Contains(card.Model.Id);

        public void RequestSelect(WidgetCard card)
        {
            // ignore: nothing to select into
        }

        public void RequestCopy(WidgetCard card)
        {
            // ignore: no clipboard here
        }

        public void RequestCut(WidgetCard card)
        {
            // ignore: as above
        }

        public void RequestPaste()
        {
            // ignore: as above
        }

        public void RequestSaveConfig()
        {
            // ignore: nothing is persisted
        }

        public void RequestTextInput(WidgetCard card, bool wanted)
        {
            // ignore: no keyboard here
        }

        public void KeyboardPress(WidgetCard card)
        {
            // ignore: as above
        }
    }
}
#endif
