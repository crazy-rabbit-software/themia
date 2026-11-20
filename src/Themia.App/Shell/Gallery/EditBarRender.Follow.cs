#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Geometry;
using Themia.Core.Settings;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-2350] A page's flyout follows its page both ways (owner, 2026-11-20: "the settings height does not change back
/// when i close a section again"): the Views page hung from its entry and hung again by the flyout's own trigger
/// (<see cref="PageFlyout.PageResized"/>, the surface's), then Desktop's card opened and closed by its head, its icon
/// picker unfolded and folded inside it, the card closed by Escape's way back, and Work's sub-page (shorter than the
/// list) opened and left. Writes <c>page-&lt;theme&gt;-follow-&lt;step&gt;.png</c> per step and says each height, PASS
/// where the flyout stands at its page's height after every step and every closed step is as tall as the page was
/// before it opened. <c>--editbar-png --only follow</c> draws these alone.
/// </summary>
internal static partial class EditBarRender
{
    private static async Task FollowShots(Grid screen, string outDir, string theme)
    {
        screen.Children.Clear();
        screen.Height = OpenHeight;
        var flat = screen.Background;
        screen.Background = Wallpaper();
        var canvas = new Canvas { Clip = new RectangleGeometry { Rect = new Rect(0, 0, ScreenWidth, OpenHeight) } };
        screen.Children.Add(canvas);
        Blobs(canvas);

        var bar = Bar(new EditBarState(null, 0, SettingsPageId.Views, false, ScreenWidth - (2 * ShellMetricsBarTop)));
        var barRect = PlaceBar(canvas, bar, ScreenWidth);
        // Two views: the page with a card opened stays under 70 % of the work area, so only the trigger can shrink it back.
        var host = new RenderViewsHost(2);
        var view = new PageView(() => PageRows.Of(SettingsPages.Views(host), edit => host.Page?.Ask(edit)));
        host.Page = view;
        UiLanguage.ApplyFlow(view);
        var area = new PixelRect(0, 0, ScreenWidth, OpenHeight);
        var flyout = HangFlyout(canvas, view, bar, barRect, SettingsPageId.Views, area);
        void Hang() => flyout.Place(FlyoutPlacement.Place(bar.EntryBox(SettingsPageId.Views, canvas) ?? barRect, barRect, area, flyout.ContentHeight));

        // The surface's trigger (DesktopWindow.Show): hung again whenever the page grows or shrinks; drawn, then hung, as
        // the surface opens a page (OpenPage).
        flyout.PageResized += Hang;
        view.Show();
        canvas.UpdateLayout();
        Hang();

        // Desktop's card opens in place (Work, the view this display shows, is a sub-page of its own: a page shorter
        // than the list).
        var desktop = ViewsMenu.Keys.View("desktop");
        var icon = ViewsMenu.Keys.Icon("desktop");
        var steps = new List<(string Step, double Height, double Wanted)>();
        async Task Step(string name, Action act)
        {
            act();
            await Task.Delay(600).ConfigureAwait(true);
            canvas.UpdateLayout();

            // What the flyout should stand at for the page as it is now: its rows' height up to 70 % of the work area.
            var wanted = FlyoutPlacement.Place(bar.EntryBox(SettingsPageId.Views, canvas) ?? barRect, barRect, area, flyout.ContentHeight).Height
                + Themia.Sdk.Kit.ShellMetrics.Flyout.BeakHeight;
            steps.Add((name, flyout.Height, wanted));
            var path = System.IO.Path.Combine(outDir, $"page-{theme}-follow-{name}.png");
            await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
            Console.WriteLine($"editbar: wrote {path} (flyout {flyout.Height:0.#} tall, its page wants {wanted:0.#})");
        }

        await Step("closed", () => { }).ConfigureAwait(true);
        await Step("open", () => ClickHead(view, desktop)).ConfigureAwait(true);
        await Step("open-icon", () => ClickHead(view, icon)).ConfigureAwait(true);
        await Step("open-icon-folded", () => ClickHead(view, icon)).ConfigureAwait(true);
        await Step("closed-by-head", () => ClickHead(view, desktop)).ConfigureAwait(true);
        await Step("open-again", () => ClickHead(view, desktop)).ConfigureAwait(true);
        await Step("closed-by-escape", () => view.Back()).ConfigureAwait(true);
        await Step("sub-page", () => ClickHead(view, ViewsMenu.Keys.View("work"))).ConfigureAwait(true);
        await Step("sub-page-back", () => view.Back()).ConfigureAwait(true);

        var h = steps.ToDictionary(s => s.Step, s => s.Height);
        var ok = steps.All(s => Math.Abs(s.Height - s.Wanted) <= 0.5)
            && h["open"] > h["closed"] + 0.5
            && h["open-icon"] > h["open"] + 0.5
            && Math.Abs(h["closed-by-head"] - h["closed"]) <= 0.5
            && Math.Abs(h["closed-by-escape"] - h["closed"]) <= 0.5
            && Math.Abs(h["sub-page-back"] - h["closed"]) <= 0.5;
        Console.WriteLine($"editbar: follow {(ok ? "PASS" : "FAIL")}: " + string.Join(", ", steps.Select(s => $"{s.Step} {s.Height:0.#}" + (Math.Abs(s.Height - s.Wanted) > 0.5 ? $" (wants {s.Wanted:0.#})" : string.Empty))));

        view.Close();
        screen.Background = flat;
    }

    /// <summary>A click on the head of the card or picker at <paramref name="key"/>, through its button's own click.</summary>
    private static void ClickHead(PageView view, string key)
    {
        if (Head(view, key + "#head") is { } button && FrameworkElementAutomationPeer.CreatePeerForElement(button) is ButtonAutomationPeer peer)
        {
            peer.Invoke();
            return;
        }

        Console.WriteLine($"editbar: no head {key} to click");
    }

    private static Button? Head(DependencyObject root, string tag)
    {
        if (root is Button { Tag: string t } button && t == tag)
        {
            return button;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (Head(VisualTreeHelper.GetChild(root, i), tag) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
#endif
