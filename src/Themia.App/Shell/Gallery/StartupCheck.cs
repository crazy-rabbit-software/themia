#if DEBUG
using System.Numerics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Hosting;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.App.Shell.Motion;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1018] Debug only: <c>Themia.exe --startup-check &lt;out.txt&gt; [--state &lt;state.json&gt;] [--show &lt;view&gt;]
/// [--hold ms] [--hide] [--twice] [--wallpaper &lt;picture&gt;] [--interrupt ms]</c>.
/// A real <see cref="AppHost"/> over a detached store (a copy of the state is read, never written) and a
/// real <see cref="DesktopWindow"/> on a stand-in display off-screen, started on the view as the app starts
/// on it: the provisional pass, the root loaded, the startup intro. Once the intro should long have ended,
/// every card is reported as the compositor holds it (its visual's opacity, scale and translation), with
/// its layout and what UI Automation sees under the surface (the desktop's list and its children, [S-5576]);
/// a card not at rest is marked NOT AT REST; <c>out.png</c> is the surface drawn. <c>--hide</c> hides the
/// window through the intro, <c>--twice</c> shows the view on a second stand-in display too, <c>--wallpaper</c>
/// frosts the cards over that picture, <c>--interrupt</c> lays the surface out again that long after the hold and reads the cards at once. The log goes to the console, never the user's log folder.
/// </summary>
internal static class StartupCheck
{
    private const string Scope = "startup-check";
    private const string Flag = "--startup-check";
    private const string DisplayId = "startup-check-display";

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is null)
        {
            Log.Error(Scope, "usage: --startup-check <out.txt> [--state <state.json>] [--show <view>]");
            app.Exit();
            return true;
        }

        var outPath = Path.GetFullPath(args[at + 1]);
        _ = RunAsync(app, outPath, ArgAfter(args, "--state"), ArgAfter(args, "--show"), int.TryParse(ArgAfter(args, "--hold"), out var hold) && hold > 0 ? hold : 3000);
        return true;
    }

    private static string? ArgAfter(string[] args, string flag)
    {
        var at = Array.IndexOf(args, flag);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    private static async Task RunAsync(Application app, string outPath, string? statePath, string? show, int holdMs)
    {
        var lines = new List<string>();
        AppHost? host = null;
        DesktopWindow? surface = null;
        DesktopWindow? second = null;

        void Say(string line)
        {
            Console.WriteLine(line);
            lines.Add(line);
        }

        Log.AddSink(static record => Console.WriteLine(LogFormat.Record(record)));

        try
        {
            var state = statePath is { } source
                ? StateDocument.Parse(File.ReadAllText(source)).State ?? throw new InvalidDataException($"{source} is not a state document")
                : AppState.CreateDefault();
            var view = show is null ? state.Views[0] : state.Views.First(v => string.Equals(v.Name, show, StringComparison.OrdinalIgnoreCase));
            state.ShowViewOn(DisplayId, view.Id);

            var bounds = new Win32.Rect { Left = -32000, Top = -32000, Right = -32000 + 3840, Bottom = -32000 + 2160 };
            var monitor = new MonitorTarget(DisplayId, IdResolved: true, "startup-check", bounds, bounds with { Bottom = bounds.Bottom - 72 }, IsPrimary: false);

            using var wallpaper = ArgAfter(Environment.GetCommandLineArgs(), "--wallpaper") is { } picture
                ? RenderWallpaper.Use(picture, new Themia.Core.Geometry.ScreenMetrics(2560, 1440))
                : null;

            host = new AppHost(DispatcherQueue.GetForCurrentThread(), AppStateStore.Detached(Path.Combine(Path.GetTempPath(), "themia-startup-check", "state.json")), state);
            surface = new DesktopWindow(host, monitor);
            surface.ShowSurface();
            if (Environment.GetCommandLineArgs().Contains("--twice"))
            {
                var right = bounds with { Left = bounds.Right + 100, Right = bounds.Right + 100 + 3840 };
                state.ShowViewOn(DisplayId + "-2", view.Id);
                second = new DesktopWindow(host, new MonitorTarget(DisplayId + "-2", IdResolved: true, "startup-check-2", right, right with { Bottom = right.Bottom - 72 }, IsPrimary: false));
                second.ShowSurface();
            }

            if (Environment.GetCommandLineArgs().Contains("--hide"))
            {
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(surface.DebugWindow));
                Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id).Hide();
                await Task.Delay(holdMs).ConfigureAwait(true);
                Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id).Show(activateWindow: false);
            }

            // A diagnostic's wait, not a correctness path: the intro is 500 ms; this is long past it.
            await Task.Delay(holdMs).ConfigureAwait(true);

            // [S-1017] A layout pass while the intro is still in flight: the cards are read at once after it.
            if (int.TryParse(ArgAfter(Environment.GetCommandLineArgs(), "--interrupt"), out var after) && after >= 0)
            {
                await Task.Delay(after).ConfigureAwait(true);
                host.Mutate(_ => { }, undo: null);
            }

            Say($"startup-check: view '{view.Name}', {view.Widgets.Count} widget(s), intro {state.Design.IntroAnimation} {state.Design.IntroAnimationMs} ms, animations {(SystemMotion.AnimationsEnabled ? "on" : "off")}");
            var unrested = 0;
            foreach (var (shown, widget) in new[] { surface, second }.OfType<DesktopWindow>().SelectMany(s => view.Widgets.Select(w => (s, w))))
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
            Say(unrested == 0 ? "startup-check: every card at rest" : $"startup-check: {unrested} card(s) NOT AT REST");
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the startup check failed", ex);
            Say($"startup-check: failed: {ex.Message}");
        }
        finally
        {
            second?.CloseSurface();
            surface?.CloseSurface();
            host?.Dispose();
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            await File.WriteAllLinesAsync(outPath, lines).ConfigureAwait(true);
            app.Exit();
        }
    }
}
#endif
