#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-855] Debug only: <c>Themia.exe --check displays &lt;outDir&gt;</c>. A real <see cref="AppHost"/> over a detached
/// store and two real <see cref="DesktopWindow"/>s on stand-in displays off-screen, side by side as the owner's (two
/// 2560 × 1440 panels, the taskbar 72 pixels wide on the left of one and on the right of the other), both showing one
/// view laid out against one display's work area or the other's (a column flush with each work area's edges, a
/// joined pair at the right, a clock at one work area's centre). Once the surfaces have settled, every card is
/// checked against each display's own work area: a card under that display's taskbar is a finding. Then where a new
/// widget lands when placed at either display's far edge, and where each display's edit bar stands (centred in its
/// own work area). [S-6608] Then a widget dragged half over each display's own taskbar (owner, 2026-11-21: "the widgets
/// should be able to go over the screen"): it lands there and stays through the settles that follow, partly behind the
/// taskbar, the visible minimum on both work areas. Last a second view laid on the left display alone (a widget hung over
/// its taskbar, one flush with the monitor's right edge) is shown on the right display too: the flush one moves clear of
/// the right display's taskbar, the hung one stays, and a widget placed at either far edge lands clear of both. Writes
/// <c>displays.txt</c> (PASS or FAIL per check) and <c>display-left.png</c> / <c>display-right.png</c> (the first view,
/// the dragged widgets over the taskbars) and <c>display-left-work.png</c> / <c>display-right-work.png</c> (the second),
/// each taskbar's strip tinted. Nothing reaches the real displays or the user's state.
/// </summary>
internal static class DisplaysCheck
{
    private const string Scope = "displays-check";
    private const string LeftId = "displays-check-taskbar-left";
    private const string RightId = "displays-check-taskbar-right";

    /// <summary>The stand-in taskbar's width, physical pixels (Windows' 48 at 150 %).</summary>
    private const int Taskbar = 72;

    /// <summary>
    /// [S-11872] Under a DIP past a taskbar's edge is the card's edge put on its whole device pixel (a taskbar's edge is
    /// rarely on a whole DIP: 2488 pixels at 150 % is 1658.67): a pixel's rounding, not a widget under the taskbar.
    /// </summary>
    private const double Rounding = 1;

    public static RenderCommand Command { get; } = new(
        RenderVerb.Check,
        "displays",
        "<outDir>",
        """
        one view on two stand-in displays, the taskbar left on one and right on the other, through a real host and surfaces:
        every card clear of both taskbars, placement at the far edges, a widget dragged over each taskbar staying there
        (the visible minimum kept), a view shown on the other display, PASS or FAIL [S-855]
        """,
        run => RunAsync(run.PathArg(0)));

    private static async Task<int> RunAsync(string outDir)
    {
        var report = new CheckReport(Scope, Path.Combine(outDir, "displays.txt"));
        void Say(string line) => report.Say(line);
        void Check(bool ok, string what) => report.Expect(ok, what);

        Log.AddSink(static record =>
        {
            if (record.Level >= LogLevel.Info)
            {
                Console.WriteLine(LogFormat.Record(record));
            }
        });

        StandInDesktop? desk = null;
        try
        {
            Directory.CreateDirectory(outDir);
            var state = AppState.CreateDefault();
            var view = state.Views[0];
            view.Widgets.Clear();

            // Off-screen, side by side: the left panel's taskbar on its left, the right panel's on its right (the outer edges).
            var leftBounds = StandInDesktop.OffScreen(2560, 1440);
            var rightBounds = StandInDesktop.OffScreen(2560, 1440, left: 2560);
            var leftMonitor = new MonitorTarget(LeftId, IdResolved: true, "displays-check-left", leftBounds, leftBounds with { Left = leftBounds.Left + Taskbar }, IsPrimary: false);
            var rightMonitor = new MonitorTarget(RightId, IdResolved: true, "displays-check-right", rightBounds, rightBounds with { Right = rightBounds.Right - Taskbar }, IsPrimary: false);

            state.ShowViewOn(LeftId, view.Id);
            state.ShowViewOn(RightId, view.Id);

            desk = StandInDesktop.Create(Scope, state);
            var host = desk.Host;
            var left = desk.Add(leftMonitor);
            var right = desk.Add(rightMonitor);
            desk.Show();

            // A diagnostic's wait, not a correctness path: both roots load and settle, the intro ends.
            await Task.Delay(2500).ConfigureAwait(true);

            // The view as laid out on one display or the other, each clock at the size the display paints it (its sizing
            // on the grid): flush with the left display's taskbar and with the right monitor edge, flush with the left
            // monitor edge and with the right display's taskbar, a clock at the left display's work area's centre, and a
            // joined pair (touching cells) at the right monitor edge. Laid in one change, as an arrangement arrives: laid on no
            // work area of these displays (a file from before, another PC), so the view's own area is none.
            var screen = left.Metrics;
            var probe = WidgetInstance.Create(new WidgetId("clock"), new FracRect(0.4, 0.4, 0.1, 0.1));
            var size = SizeFit.Shown(probe.Rect, SizeFit.Limits(probe, screen, 1), screen);
            var (w, h) = (size.W, size.H);
            var t = (double)Taskbar / 2560;
            var widgets = new (string Name, WidgetInstance Widget)[]
            {
                ("flush-left-taskbar", WidgetInstance.Create(new WidgetId("clock"), new FracRect(t, 0.10, w, h))),
                ("flush-left-edge", WidgetInstance.Create(new WidgetId("clock"), new FracRect(0, 0.35, w, h))),
                ("flush-right-edge", WidgetInstance.Create(new WidgetId("clock"), new FracRect(1 - w, 0.10, w, h))),
                ("flush-right-taskbar", WidgetInstance.Create(new WidgetId("clock"), new FracRect(1 - t - w, 0.35, w, h))),
                ("pair-a", WidgetInstance.Create(new WidgetId("clock"), new FracRect(1 - (2 * w), 0.60, w, h))),
                ("pair-b", WidgetInstance.Create(new WidgetId("clock"), new FracRect(1 - w, 0.60, w, h))),
                ("centred", WidgetInstance.Create(new WidgetId("clock"), new FracRect(t + ((1 - t - w) / 2), 0.80, w, h))),
            };
            host.Mutate(s => { s.Views[0].Widgets.AddRange(widgets.Select(it => it.Widget)); s.Views[0].WorkArea = null; }, undo: null);
            await Task.Delay(800).ConfigureAwait(true);
            host.SettleSurfaces();
            await Task.Delay(500).ConfigureAwait(true);

            Say($"displays-check: one view on two displays, the taskbar {Taskbar} px on the left of one and the right of the other");
            var running = host;
            foreach (var (surface, name) in new[] { (left, "left"), (right, "right") })
            {
                var work = WorkDips(surface);
                Say($"  display {name}: {surface.Metrics.Width:0}x{surface.Metrics.Height:0} DIPs, work area x {work.X:0.#}..{work.X + work.Width:0.#}");
                foreach (var (label, widget) in widgets)
                {
                    var model = running.State.Views[0].Find(widget.Id) ?? widget;
                    if (surface.CardFor(widget.Id) is not { } card)
                    {
                        Check(false, $"{name}: {label} has no card");
                        continue;
                    }

                    var box = new DipRect(Canvas.GetLeft(card), Canvas.GetTop(card), card.Width, card.Height);
                    var under = Under(box, work, surface.Metrics);
                    Check(under < Rounding, $"{name}: {label,-20} at x {box.X:0.#}..{box.X + box.Width:0.#} stored x {model.Rect.X:0.####}" +
                        (under >= Rounding ? $" UNDER THE TASKBAR by {under:0.#} DIPs" : string.Empty));
                }

                // [S-2240] The pair still meets edge to edge.
                if (surface.CardFor(widgets[4].Widget.Id) is { } a && surface.CardFor(widgets[5].Widget.Id) is { } b)
                {
                    var seam = Canvas.GetLeft(b) - (Canvas.GetLeft(a) + a.Width);
                    Check(Math.Abs(seam) < 0.5, $"{name}: the joined pair meets ({seam:0.#} DIPs apart)");
                }
            }

            // Placement at either display's far edge: the new widget is clear of every taskbar of the displays showing the view.
            foreach (var (surface, name, x) in new[] { (left, "left", 0.995), (right, "right", 0.005), (left, "left", 0.005), (right, "right", 0.995) })
            {
                var payload = PlacementPayload.ForNew(new WidgetId("clock"), 200, 160);
                var landed = payload.Plan(x, 0.5, surface.Metrics, surface.SizesNow).Added[0].Rect;
                var onLeft = Under(Box(landed, left.Metrics), WorkDips(left), left.Metrics);
                var onRight = Under(Box(landed, right.Metrics), WorkDips(right), right.Metrics);
                Check(onLeft < Rounding && onRight < Rounding, $"placed on the {name} display at x {x}: lands at x {landed.X:0.####}..{landed.Right:0.####}" +
                    (onLeft >= Rounding ? $", under the left display's taskbar by {onLeft:0.#}" : string.Empty) +
                    (onRight >= Rounding ? $", under the right display's taskbar by {onRight:0.#}" : string.Empty));
            }

            // [S-6608] A widget dragged half its width over each display's own taskbar through the drag's rules (BlockMove's,
            // BlockGeometry.Move), landed as a drag lands (Land), then every settle that follows a change: it stays.
            foreach (var (surface, name, index, sign) in new[] { (left, "left", 0, -1.0), (right, "right", 3, 1.0) })
            {
                var id = widgets[index].Widget.Id;
                var model = host.State.Views[0].Find(id)!;
                var start = model.Rect;
                var dragged = BlockGeometry.Apply(start, BlockGeometry.Move([start], sign * start.W / 2, 0, surface.Metrics), surface.Metrics);
                host.Land(surface, [new Landing(id, dragged)], UndoAction.Move);
                host.Mutate(_ => { }, undo: null);
                host.SettleSurfaces();
                await Task.Delay(500).ConfigureAwait(true);

                var kept = host.State.Views[0].Find(id)!.Rect;
                Check(kept == dragged, $"{name}: {widgets[index].Name} dragged over its taskbar to x {dragged.X:0.####}..{dragged.Right:0.####} stays at x {kept.X:0.####}..{kept.Right:0.####}");
                foreach (var (on, onName) in new[] { (left, "left"), (right, "right") })
                {
                    var work = WorkDips(on);
                    var box = on.CardFor(id) is { } card ? new DipRect(Canvas.GetLeft(card), Canvas.GetTop(card), card.Width, card.Height) : Box(kept, on.Metrics);
                    var shows = Math.Min(box.X + box.Width, work.X + work.Width) - Math.Max(box.X, work.X);
                    Check(shows >= GridGeometry.MinVisibleDip - Rounding, $"  on the {onName} display {shows:0.#} DIPs of it show on the work area (at least {GridGeometry.MinVisibleDip:0})");
                    if (ReferenceEquals(on, surface))
                    {
                        var under = Under(box, work, on.Metrics);
                        Check(under >= Rounding, $"  on the {onName} display {under:0.#} DIPs of it are behind the taskbar");
                    }
                }
            }

            // [S-852] Each display's edit bar between its own work area's sides, so centred in it (read from its margins:
            // edit mode is not entered, an activatable surface could take the foreground).
            foreach (var (surface, name) in new[] { (left, "left"), (right, "right") })
            {
                var work = WorkDips(surface);
                if (Find<EditBar>(surface.DebugRoot) is not { } bar)
                {
                    Check(false, $"{name}: no edit bar");
                    continue;
                }

                var from = bar.Margin.Left;
                var to = surface.Metrics.Width - bar.Margin.Right;
                Check(Math.Abs(from - work.X) <= 0.5 && Math.Abs(to - (work.X + work.Width)) <= 0.5,
                    $"{name}: the edit bar stands between x {from:0.#} and {to:0.#}, centred at {(from + to) / 2:0.#}; its work area's centre {work.X + (work.Width / 2):0.#}");
            }

            foreach (var (surface, name) in new[] { (left, "left"), (right, "right") })
            {
                await Picture(surface, Path.Combine(outDir, $"display-{name}.png")).ConfigureAwait(true);
            }

            // [S-855] A second view laid on the left display alone, then shown on the right display too.
            var second = ViewState.Create("Work");
            host.Mutate(s => { s.Views.Add(second); s.ShowViewOn(LeftId, second.Id); }, undo: null);
            await Task.Delay(800).ConfigureAwait(true);
            var hung = WidgetInstance.Create(new WidgetId("clock"), new FracRect(t - (w / 2), 0.2, w, h));
            var flush = WidgetInstance.Create(new WidgetId("clock"), new FracRect(1 - w, 0.5, w, h));
            host.Mutate(s => s.FindView(second.Id)!.Widgets.AddRange([hung, flush]), undo: null);
            var hungAt = hung.Rect;
            await Task.Delay(500).ConfigureAwait(true);
            host.ShowView(RightId, second.Id);
            await Task.Delay(800).ConfigureAwait(true);
            host.SettleSurfaces();
            await Task.Delay(500).ConfigureAwait(true);

            Say("  a second view laid on the left display alone, then shown on the right display too:");
            var flushAt = host.State.FindView(second.Id)!.Find(flush.Id)!.Rect;
            Check(Under(Box(flushAt, right.Metrics), WorkDips(right), right.Metrics) < Rounding,
                $"  the widget flush with the monitor's right edge moves clear of the right display's taskbar: x {flushAt.X:0.####}..{flushAt.Right:0.####}");
            var hungNow = host.State.FindView(second.Id)!.Find(hung.Id)!.Rect;
            Check(hungNow == hungAt, $"  the widget hung over the left display's taskbar stays: x {hungNow.X:0.####} (was {hungAt.X:0.####})");
            foreach (var (surface, name, x) in new[] { (left, "left", 0.005), (right, "right", 0.995) })
            {
                var landed = PlacementPayload.ForNew(new WidgetId("clock"), 200, 160).Plan(x, 0.5, surface.Metrics, surface.SizesNow).Added[0].Rect;
                var onLeft = Under(Box(landed, left.Metrics), WorkDips(left), left.Metrics);
                var onRight = Under(Box(landed, right.Metrics), WorkDips(right), right.Metrics);
                Check(onLeft < Rounding && onRight < Rounding, $"  placed on the {name} display at x {x}: lands at x {landed.X:0.####}..{landed.Right:0.####}, clear of both taskbars");
            }

            foreach (var (surface, name) in new[] { (left, "left"), (right, "right") })
            {
                await Picture(surface, Path.Combine(outDir, $"display-{name}-work.png")).ConfigureAwait(true);
            }
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

    /// <summary>The display's own work area in the surface's DIPs.</summary>
    private static DipRect WorkDips(DesktopWindow surface)
    {
        var bounds = surface.Monitor.Bounds;
        var work = surface.Monitor.WorkArea;
        var metrics = surface.Metrics;
        var scale = bounds.Width / metrics.Width;
        return new DipRect((work.Left - bounds.Left) / scale, (work.Top - bounds.Top) / scale, work.Width / scale, work.Height / scale);
    }

    private static DipRect Box(FracRect rect, ScreenMetrics screen)
        => new(rect.X * screen.Width, rect.Y * screen.Height, rect.W * screen.Width, rect.H * screen.Height);

    /// <summary>
    /// How far, in DIPs, a box reaches under the taskbar: past a work area's edge that is inside the monitor (one at the
    /// monitor's own edge may be hung over, [S-6608]).
    /// </summary>
    private static double Under(DipRect box, DipRect work, ScreenMetrics screen)
    {
        var under = 0.0;
        if (work.X > 0.5)
        {
            under = Math.Max(under, Math.Min(work.X, box.X + box.Width) - box.X);
        }

        if (work.Y > 0.5)
        {
            under = Math.Max(under, Math.Min(work.Y, box.Y + box.Height) - box.Y);
        }

        if (work.X + work.Width < screen.Width - 0.5)
        {
            under = Math.Max(under, box.X + box.Width - Math.Max(work.X + work.Width, box.X));
        }

        if (work.Y + work.Height < screen.Height - 0.5)
        {
            under = Math.Max(under, box.Y + box.Height - Math.Max(work.Y + work.Height, box.Y));
        }

        return Math.Max(0, under);
    }

    private static T? Find<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T found)
        {
            return found;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (Find<T>(VisualTreeHelper.GetChild(root, i)) is { } inner)
            {
                return inner;
            }
        }

        return null;
    }

    /// <summary>The surface drawn, its taskbar's strip tinted: what the display shows outside it is cut away by the region.</summary>
    private static async Task Picture(DesktopWindow surface, string path)
    {
        var work = WorkDips(surface);
        var metrics = surface.Metrics;
        var strip = work.X > 0.5
            ? new DipRect(0, 0, work.X, metrics.Height)
            : new DipRect(work.X + work.Width, 0, metrics.Width - (work.X + work.Width), metrics.Height);
        var tint = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = strip.Width,
            Height = strip.Height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(strip.X, strip.Y, 0, 0),
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(150, 220, 30, 30)),
            IsHitTestVisible = false,
        };

        var root = (Panel)surface.DebugRoot;
        root.Children.Add(tint);
        await Task.Delay(200).ConfigureAwait(true);
        await RenderHarness.SaveAsync(root, path, Scope).ConfigureAwait(true);
        root.Children.Remove(tint);
    }
}
#endif
