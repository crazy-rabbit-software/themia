#if DEBUG
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Hosting;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Themia.App.Shell.Motion;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1018] Debug only: <c>Themia.exe --check startup &lt;out.txt&gt; [--state &lt;state.json&gt;] [--show &lt;view&gt;]
/// [--hold ms] [--hide] [--twice] [--wallpaper &lt;picture&gt;] [--interrupt ms]</c>.
/// A real <see cref="AppHost"/> over a detached store (a copy of the state is read, never written) and a
/// real <see cref="DesktopWindow"/> on a stand-in display off-screen (<see cref="StandInDesktop"/>), started on the
/// view as the app starts on it: the provisional pass, the root loaded, the startup intro. Once the intro should long
/// have ended (<c>--hold</c>, 3 s), every card is reported as the compositor holds it (its visual's opacity, scale and
/// translation), with its layout and what UI Automation sees under the surface (the desktop's list
/// and its children, [S-5576]); a card not at rest is marked NOT AT REST and fails the check; <c>out.png</c> is the
/// surface drawn. <c>--hide</c> hides the window through the intro, <c>--twice</c> shows the view
/// on a second stand-in display too, <c>--wallpaper</c> frosts the cards over that picture, <c>--interrupt</c> lays the
/// surface out again that long after and reads the cards at once. The log goes to the console, never the user's log folder.
/// </summary>
internal static class StartupCheck
{
    private const string Scope = "startup-check";
    private const string DisplayId = "startup-check-display";

    /// <summary>How long the check waits for the start (and <c>--hide</c> keeps the window hidden) when no <c>--hold</c> is given.</summary>
    private const int HoldMs = 3000;

    public static RenderCommand Command { get; } = new(
        RenderVerb.Check,
        "startup",
        "<out.txt> [--state <state.json>] [--show <view>] [--hold ms] [--hide] [--twice] [--wallpaper <picture>] [--interrupt ms]",
        """
        the startup intro on a real host and surface off-screen: long past it, every card as the compositor holds it
        (opacity, scale, translation), its layout and what UI Automation sees; a card NOT AT REST fails; <out>.png [S-1018]
        --state <state.json>   the views from a copy of a state file (read, never written)
        --show <view>          that view (the first by default)
        --hold ms              read the cards that long after the start (3 s by default; less: an intro caught in flight)
        --hide                 the window hidden through the intro, --hold long, then shown and --hold waited again [S-1018]
        --twice                the view on a second stand-in display too
        --wallpaper <picture>  the cards frosted over that picture
        --interrupt ms         a layout pass that long after, the cards read at once after it [S-1017]
        """,
        RunAsync);

    private static async Task<int> RunAsync(RenderRun run)
    {
        var outPath = run.PathArg(0);
        var hold = Milliseconds(run, "--hold", least: 1) ?? HoldMs;
        var interrupt = Milliseconds(run, "--interrupt", least: 0);
        var report = new CheckReport(Scope, outPath);
        void Say(string line) => report.Say(line);
        StandInDesktop? desk = null;

        Log.AddSink(static record => Console.WriteLine(LogFormat.Record(record)));

        try
        {
            var state = run.Option("--state") is { } source
                ? StateDocument.Parse(File.ReadAllText(source)).State ?? throw new InvalidDataException($"{source} is not a state document")
                : AppState.CreateDefault();
            var view = run.Option("--show") is not { } show ? state.Views[0] : state.Views.First(v => string.Equals(v.Name, show, StringComparison.OrdinalIgnoreCase));
            state.ShowViewOn(DisplayId, view.Id);

            using var wallpaper = run.Option("--wallpaper") is { } picture
                ? RenderWallpaper.Use(picture, new Themia.Core.Geometry.ScreenMetrics(2560, 1440))
                : null;

            desk = StandInDesktop.Create(Scope, state);
            var bounds = StandInDesktop.OffScreen(3840, 2160);
            var surface = desk.Add(StandInDesktop.Display(DisplayId, bounds, taskbar: 72));
            if (run.Has("--twice"))
            {
                state.ShowViewOn(DisplayId + "-2", view.Id);
                desk.Add(StandInDesktop.Display(DisplayId + "-2", StandInDesktop.OffScreen(3840, 2160, left: 3840 + 100), taskbar: 72));
            }

            desk.Show();

            if (run.Has("--hide"))
            {
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(surface.DebugWindow));
                Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id).Hide();

                // a timing, not a wait: how long the window stays hidden through its start.
                await Task.Delay(hold).ConfigureAwait(true);
                Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id).Show(activateWindow: false);
            }

            // A diagnostic's wait, not a correctness path: the intro is 500 ms; this is long past it (or --hold's).
            await Task.Delay(hold).ConfigureAwait(true);

            // [S-1017] A layout pass while the intro is still in flight: the cards are read at once after it.
            if (interrupt is { } after)
            {
                // a timing, not a wait: the layout pass lands that far into the intro.
                await Task.Delay(after).ConfigureAwait(true);
                desk.Host.Mutate(_ => { }, undo: null);
            }

            Say($"startup-check: view '{view.Name}', {view.Widgets.Count} widget(s), intro {state.Design.IntroAnimation} {state.Design.IntroAnimationMs} ms, animations {(SystemMotion.AnimationsEnabled ? "on" : "off")}");
            var unrested = 0;
            foreach (var (shown, widget) in desk.Surfaces.SelectMany(s => view.Widgets.Select(w => (s, w))))
            {
                if (shown.CardFor(widget.Id) is not { } card)
                {
                    Say($"  {widget.Config.GetType().Name,-28} NO CARD");
                    unrested++;
                    continue;
                }

                var visual = ElementCompositionPreview.GetElementVisual(card);
                var translation = visual.Properties.TryGetVector3("Translation", out var t) == Microsoft.UI.Composition.CompositionGetValueStatus.Succeeded ? t : Vector3.Zero;
                var rest = visual.Opacity == 1 && visual.Scale == Vector3.One && translation == Vector3.Zero && card.Opacity == 1 && card.Visibility == Visibility.Visible;
                unrested += rest ? 0 : 1;
                Say($"  {widget.Config.GetType().Name,-28} {card.Visibility} xaml-opacity {card.Opacity:0.##} at {Microsoft.UI.Xaml.Controls.Canvas.GetLeft(card):0.#},{Microsoft.UI.Xaml.Controls.Canvas.GetTop(card):0.#} " +
                    $"size {card.ActualWidth:0.#}x{card.ActualHeight:0.#} visual opacity {visual.Opacity:0.###} scale {visual.Scale.X:0.###} translation {translation.Y:0.#}{(rest ? string.Empty : "  NOT AT REST")}");
            }

            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(surface.DebugRoot);
            var box = peer.GetBoundingRectangle();
            Say($"automation: {peer.GetAutomationControlType()} '{peer.GetName()}' at {box.X:0},{box.Y:0} {box.Width:0}x{box.Height:0}");
            try
            {
                Say($"automation: {peer.GetChildren()?.Count ?? 0} child(ren)");
            }
            catch (Exception ex)
            {
                Say($"automation: the children failed: {ex.Message}");
            }

            await RenderPng.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, ".png")).ConfigureAwait(true);
            report.Say(unrested == 0 ? "startup-check: every card at rest" : $"startup-check: {unrested} card(s) NOT AT REST", unrested);
        }
        catch (Exception ex)
        {
            report.Crash(ex);
        }
        finally
        {
            desk?.Dispose();
        }

        return await report.FinishAsync().ConfigureAwait(true);
    }

    /// <summary>The milliseconds after <paramref name="option"/> (at least <paramref name="least"/>), or null when it is not given.</summary>
    private static int? Milliseconds(RenderRun run, string option, int least)
        => run.Option(option) is not { } given ? null
            : int.TryParse(given, out var ms) && ms >= least ? ms
            : throw run.Usage($"{option} {given}: milliseconds, at least {least}");
}
#endif
