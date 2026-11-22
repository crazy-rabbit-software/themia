#if DEBUG
using System.Diagnostics;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-11890] Debug only: <c>Themia.exe --check drag-bench &lt;out.txt&gt; [--frames n] [--events n]</c> measures what a
/// move's pointer steps cost on a real surface. A real <see cref="AppHost"/> over a detached store (nothing read or
/// saved of the user's) and a real <see cref="DesktopWindow"/> on a 1920x1080 stand-in display parked off-screen
/// (<see cref="StandInDesktop"/>),
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
    private const string Scope = "drag-bench";
    private const string DisplayId = "drag-bench-display";

    public static RenderCommand Command { get; } = new(
        RenderVerb.Check,
        "drag-bench",
        "<out.txt> [--frames n] [--events n]",
        """
        what each stage of a move's pointer step costs on a real surface of 21 widgets, locked and edit mode, solid and
        Liquid glass: milliseconds per frame by stage, the frame's median, 95th percentile and worst, the drop [S-11890]
        --frames n   the pointer path's frames (120)
        --events n   pointer moves a frame (4: a fast mouse at 120 Hz)
        """,
        run => RunAsync(run.PathArg(0), Count(run, "--frames", 120), Count(run, "--events", 4)));

    /// <summary>The whole number after <paramref name="option"/>, at least 1, or <paramref name="fallback"/> when it is not given.</summary>
    internal static int Count(RenderRun run, string option, int fallback)
        => run.Option(option) is not { } given ? fallback
            : int.TryParse(given, out var count) && count > 0 ? count
            : throw run.Usage($"{option} {given}: a whole number of at least 1");

    private static async Task<int> RunAsync(string outPath, int frames, int events)
    {
        var report = new CheckReport(Scope, outPath);
        void Say(string line) => report.Say(line);
        StandInDesktop? desk = null;

        try
        {
            var (view, rows, icons) = Scene("Drag bench");
            var state = new AppState { Views = [view], ActiveViewId = view.Id };
            state.ShowViewOn(DisplayId, view.Id);

            desk = StandInDesktop.Create(Scope, state);
            var host = desk.Host;
            var surface = desk.Add(StandInDesktop.Display(DisplayId, StandInDesktop.OffScreen(1920, 1080)));
            StandInDesktop stand = desk;
            desk.Show();

            // The surface laid out and settled, its intro ended ([S-15902]).
            await stand.ReadyAsync("the surface", looks: false).ConfigureAwait(true);
            Touch(host, surface, rows);
            await stand.ReadyAsync("the rows touched", looks: false).ConfigureAwait(true);

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
                    await stand.ReadyAsync($"{(edit ? "edit" : "locked")} {(glass ? "glass" : "solid")}", looks: false).ConfigureAwait(true);

                    // Locked: a card of a joined row, alone (the press selected nothing). Edit: a joined row and an icon.
                    var held = surface.CardFor(rows[0][1])!;
                    if (edit)
                    {
                        host.SelectMany(view.Id, [.. rows[0], icons[0]]);
                    }

                    await RunAsync(surface, held, frames, events, $"{(edit ? "edit" : "locked")} {(glass ? "glass" : "solid")}", Say).ConfigureAwait(true);
                    host.Undo();
                    host.Select(null);
                    await stand.ReadyAsync("undone", looks: false).ConfigureAwait(true);
                }
            }
        }
        catch (Exception ex)
        {
            report.Crash(ex);
        }
        finally
        {
            DragProfile.On = false;
            desk?.Dispose();
        }

        return await report.FinishAsync().ConfigureAwait(true);
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
    /// The scene of a view named <paramref name="name"/>: three rows of cards that will touch (<see cref="Touch"/>), six
    /// cards alone and five desktop icons (own icons) down the right edge. The UI bench draws it too.
    /// </summary>
    internal static (ViewState View, List<List<string>> Rows, List<string> Icons) Scene(string name)
    {
        var view = ViewState.Create(name);
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

    /// <summary>Each row's cards put edge to edge where <paramref name="surface"/> draws them, so they join.</summary>
    internal static void Touch(AppHost host, DesktopWindow surface, List<List<string>> rows)
    {
        host.Mutate(
            state =>
            {
                var view = state.ViewForMonitor(surface.Monitor.Id);
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
