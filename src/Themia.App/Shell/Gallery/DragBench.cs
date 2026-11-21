#if DEBUG
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-11890] Debug only: <c>Themia.exe --drag-bench &lt;out.txt&gt; [--frames n] [--events n]</c> measures what a
/// move's pointer steps cost on a real surface. A real <see cref="AppHost"/> over a detached store (nothing read or
/// saved of the user's) and a real <see cref="DesktopWindow"/> on a 1920x1080 stand-in display parked off-screen,
/// showing 21 widgets: three rows of touching cards (joined, [S-2240]), six cards alone and five desktop icons. Each
/// run drives a synthetic pointer path of <c>--frames</c> frames (120) with <c>--events</c> pointer moves a frame (4,
/// a fast mouse at 120 Hz) through <see cref="DesktopWindow.BeginMove"/>, <see cref="DesktopWindow.PreviewMove"/> and
/// <see cref="DesktopWindow.CommitMove"/>, the surface's own path, then undoes it; runs: the locked desktop and edit
/// mode, solid and Liquid glass. Printed per run: the milliseconds per frame
/// each stage took (<see cref="DragProfile"/>), the frame's whole time (median, 95th percentile, worst) with the
/// synchronous layout pass, and the drop.
/// </summary>
internal static class DragBench
{
    public const string Flag = "--drag-bench";

    private const string Scope = "drag-bench";
    private const string DisplayId = "drag-bench-display";

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is null)
        {
            Log.Error(Scope, $"usage: {Flag} <out.txt> [--frames n] [--events n]");
            app.Exit();
            return true;
        }

        var frames = int.TryParse(ArgAfter(args, "--frames"), out var f) && f > 0 ? f : 120;
        var events = int.TryParse(ArgAfter(args, "--events"), out var e) && e > 0 ? e : 4;
        _ = RunAsync(app, Path.GetFullPath(args[at + 1]), frames, events);
        return true;
    }

    private static string? ArgAfter(string[] args, string flag)
    {
        var at = Array.IndexOf(args, flag);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    private static async Task RunAsync(Application app, string outPath, int frames, int events)
    {
        var lines = new List<string>();
        AppHost? host = null;
        DesktopWindow? surface = null;

        void Say(string line)
        {
            Console.WriteLine(line);
            lines.Add(line);
        }

        try
        {
            var (view, rows, icons) = Scene();
            var state = new AppState { Views = [view], ActiveViewId = view.Id };
            state.ShowViewOn(DisplayId, view.Id);

            var bounds = new Win32.Rect { Left = -32000, Top = -32000, Right = -32000 + 1920, Bottom = -32000 + 1080 };
            var monitor = new MonitorTarget(DisplayId, IdResolved: true, "drag-bench", bounds, bounds with { Bottom = bounds.Bottom - 48 }, IsPrimary: false);
            host = new AppHost(DispatcherQueue.GetForCurrentThread(), AppStateStore.Detached(Path.Combine(Path.GetTempPath(), "themia-drag-bench", "state.json")), state);
            surface = new DesktopWindow(host, monitor);
            surface.ShowSurface();

            // A diagnostic's wait, not a correctness path: the surface settles and its intro ends.
            await Task.Delay(2500).ConfigureAwait(true);
            Touch(host, surface, rows);
            await Task.Delay(800).ConfigureAwait(true);

            var cards = view.Widgets.Select(w => surface.CardFor(w.Id)).OfType<WidgetCard>().ToList();
            Say($"drag-bench: {cards.Count} cards ({icons.Count} icons), {cards.Count(c => c.Outline != CardOutline.Free)} joined, " +
                $"surface {surface.Metrics.Width:0}x{surface.Metrics.Height:0} DIPs at scale {surface.DebugRoot.XamlRoot?.RasterizationScale ?? 1:0.##}, " +
                $"{frames} frames x {events} pointer moves");

            // [S-11890] What drawing the local grid's lines for a screen costs, once per surface (its relayout).
            var prepared = Stopwatch.GetTimestamp();
            new LocalGridOverlay().Prepare(surface.Metrics, surface.DebugRoot.XamlRoot?.RasterizationScale ?? 1);
            Say($"drag-bench: the local grid's lines drawn for the screen in {Stopwatch.GetElapsedTime(prepared).TotalMilliseconds:0.000} ms");

            foreach (var glass in new[] { false, true })
            {
                host.Mutate(s => s.Design.Material = glass ? CardMaterial.LiquidGlass : CardMaterial.Solid, relayout: true, undo: null);
                foreach (var edit in new[] { false, true })
                {
                    host.SetEditMode(edit);
                    await Task.Delay(600).ConfigureAwait(true);

                    // Locked: a card of a joined row, alone (the press selected nothing). Edit: a joined row and an icon.
                    var held = surface.CardFor(rows[0][1])!;
                    if (edit)
                    {
                        host.SelectMany(view.Id, [.. rows[0], icons[0]]);
                    }

                    await RunAsync(surface, held, frames, events, $"{(edit ? "edit" : "locked")} {(glass ? "glass" : "solid")}", Say).ConfigureAwait(true);
                    host.Undo();
                    host.Select(null);
                    await Task.Delay(400).ConfigureAwait(true);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the drag bench failed", ex);
            Say($"drag-bench: failed: {ex.Message}");
        }
        finally
        {
            DragProfile.On = false;
            surface?.CloseSurface();
            host?.Dispose();
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            await File.WriteAllLinesAsync(outPath, lines).ConfigureAwait(true);
            app.Exit();
        }
    }

    /// <summary>One move along the path: each frame its pointer moves, the frame's work, the layout pass; then the drop.</summary>
    private static async Task RunAsync(DesktopWindow surface, WidgetCard held, int frames, int events, string name, Action<string> say)
    {
        var metrics = surface.Metrics;
        var root = surface.DebugRoot;
        var totals = new List<double>(frames);
        DragProfile.Reset();
        DragProfile.On = true;
        surface.BeginMove(held);
        for (var frame = 0; frame < frames; frame++)
        {
            var started = Stopwatch.GetTimestamp();
            for (var e = 1; e <= events; e++)
            {
                // A loop out and back: right and down, round, back to the start, at a fast hand's speed.
                var t = (frame + ((double)e / events)) / frames;
                var dx = 420 * Math.Sin(2 * Math.PI * t);
                var dy = 260 * Math.Sin(4 * Math.PI * t);
                surface.PreviewMove(held, dx / metrics.Width, dy / metrics.Height);
            }

            surface.FlushMove();
            DragProfile.Start();
            root.UpdateLayout();
            DragProfile.Lap(DragStage.Layout);
            totals.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            // Let the frame render, as the app would between pointer moves.
            await NextFrameAsync().ConfigureAwait(true);
        }

        DragProfile.Start();
        surface.CommitMove(held);
        DragProfile.Lap(DragStage.Commit);
        DragProfile.On = false;

        totals.Sort();
        var stages = string.Join("  ", Enum.GetValues<DragStage>().Where(s => s != DragStage.Commit)
            .Select(s => $"{s} {DragProfile.Ms(s) / frames:0.000}"));
        say($"{name,-13} per frame ms: {stages}");
        say($"{string.Empty,-13} frame total ms: median {totals[totals.Count / 2]:0.000}  p95 {totals[(int)(totals.Count * 0.95)]:0.000}  " +
            $"max {totals[^1]:0.000}  drop {DragProfile.Ms(DragStage.Commit):0.000}");
    }

    private static Task NextFrameAsync()
    {
        var done = new TaskCompletionSource();
        void OnRendering(object? sender, object args)
        {
            CompositionTarget.Rendering -= OnRendering;
            done.TrySetResult();
        }

        CompositionTarget.Rendering += OnRendering;
        return done.Task;
    }

    /// <summary>
    /// The scene: three rows of cards that will touch (<see cref="Touch"/>), six cards alone and five desktop icons
    /// (own icons) down the right edge.
    /// </summary>
    private static (ViewState View, List<List<string>> Rows, List<string> Icons) Scene()
    {
        var view = ViewState.Create("Drag bench");
        var rows = new List<List<string>>();
        foreach (var (kinds, y) in new[]
        {
            (new[] { "clock", "date", "battery" }, 0.08),
            (new[] { "cpu", "memory", "gpu" }, 0.38),
            (new[] { "wifiToggle", "bluetoothToggle", "soundToggle", "nightLightToggle" }, 0.68),
        })
        {
            var row = new List<string>();
            var x = 0.05;
            foreach (var kind in kinds)
            {
                var widget = WidgetInstance.Create(new WidgetId(kind), new FracRect(x, y, 0.1, 0.1));
                view.Widgets.Add(widget);
                row.Add(widget.Id);
                x += 0.12;
            }

            rows.Add(row);
        }

        foreach (var (kind, x, y) in new[]
        {
            ("countdown", 0.6, 0.08), ("stopwatch", 0.6, 0.38), ("moon", 0.6, 0.68),
            ("pomodoro", 0.78, 0.38), ("header", 0.78, 0.08), ("lock", 0.8, 0.68),
        })
        {
            view.Widgets.Add(WidgetInstance.Create(new WidgetId(kind), new FracRect(x, y, 0.1, 0.1)));
        }

        var icons = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var icon = WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(0.95, 0.05 + (i * 0.11), 0.04, 0.08));
            view.Widgets.Add(icon);
            icons.Add(icon.Id);
        }

        return (view, rows, icons);
    }

    /// <summary>Each row's cards put edge to edge where they are drawn, so they join.</summary>
    private static void Touch(AppHost host, DesktopWindow surface, List<List<string>> rows)
    {
        host.Mutate(
            state =>
            {
                var view = state.ViewForMonitor(DisplayId);
                foreach (var row in rows)
                {
                    var first = surface.CardFor(row[0])!.LiveRect;
                    var x = first.X;
                    foreach (var id in row)
                    {
                        var shown = surface.CardFor(id)!.LiveRect;
                        view.Widgets.First(w => w.Id == id).Rect = shown with { X = x, Y = first.Y };
                        x += shown.W;
                    }
                }
            },
            relayout: true,
            undo: null);
    }
}
#endif
