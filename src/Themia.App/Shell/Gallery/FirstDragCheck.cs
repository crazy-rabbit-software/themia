#if DEBUG
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-13060] Debug only: <c>Themia.exe --first-drag-check &lt;out.txt&gt; [--kinds a,b] [--locked] [--probe]</c>. The first
/// press on a widget, dragged past the threshold, moves it, whatever is under the pointer. A real <see cref="AppHost"/> over a
/// detached store and a real <see cref="DesktopWindow"/> on a stand-in display parked off-screen show every widget kind (or
/// <c>--kinds</c>) in batches, each on its sample. For every card the check finds, in its real visual tree, a list row (an
/// item), a button, a slider's track and thumb, a text box, a chart and an empty spot, and presses there, moves past the
/// threshold and lets go through the card's own press, move and release steps (<see cref="WidgetCard.CheckPress"/>), the
/// lease and the foreground included (the surface's button is the check's: <see cref="DesktopWindow.CheckButton"/>; it never
/// takes the foreground). What a part does with a press before the card hears it is read from what stands there, as WinUI's
/// controls route it (ButtonBase, ToggleSwitch, Thumb, Slider and the text boxes take the press; ScrollViewer does not) and
/// as this app's own parts do. Posted mouse messages would be truer, but WinUI's input reads pointer messages only: none
/// reaches XAML (<c>--probe</c> shows it). Per kind and spot: moved or stayed against what it should, a foreground taken
/// mid-press, focus left in the part; the last line PASS or FAIL. Never moves the real mouse, activates no window.
/// </summary>
internal static partial class FirstDragCheck
{
    public const string Flag = "--first-drag-check";
    private const string Scope = "first-drag-check";
    private const string DisplayId = "first-drag-check-display";
    private const int Batch = 12;

    /// <summary>How far a test drags, in DIPs: several grid cells, so the card lands on another cell.</summary>
    private const double Reach = 48;

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is null)
        {
            Log.Error(Scope, $"usage: {Flag} <out.txt> [--kinds a,b] [--locked] [--probe]");
            app.Exit();
            return true;
        }

        var kindsAt = Array.IndexOf(args, "--kinds");
        var only = kindsAt >= 0 && kindsAt + 1 < args.Length
            ? args[kindsAt + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal)
            : null;
        _ = RunAsync(app, Path.GetFullPath(args[at + 1]), only, args.Contains("--locked"), args.Contains("--probe"));
        return true;
    }

    private static async Task RunAsync(Application app, string outPath, HashSet<string>? only, bool locked, bool probe)
    {
        var lines = new List<string>();
        var failures = 0;

        void Say(string line)
        {
            Console.WriteLine(line);
            lines.Add(line);
        }

        Log.AddSink(static record =>
        {
            if (record.Level >= LogLevel.Error)
            {
                Console.WriteLine(LogFormat.Record(record));
            }
        });

        try
        {
            var kinds = ModuleCatalog.Widgets.Where(d => only is null || only.Contains(d.Id.Value)).OrderBy(d => d.Id.Value, StringComparer.Ordinal).ToList();
            Say($"first-drag-check: lock widgets {(locked ? "on" : "off")}, {kinds.Count} kind(s)");
            if (probe)
            {
                await ProbeAsync(kinds.Take(1).ToList(), Say).ConfigureAwait(true);
                return;
            }

            var failedSpots = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            var tested = 0;
            for (var start = 0; start < kinds.Count; start += Batch)
            {
                await using var desk = await Desk.StartAsync(kinds.Skip(start).Take(Batch).ToList(), locked).ConfigureAwait(true);
                foreach (var (kind, card) in desk.Cards)
                {
                    var results = new List<string>();
                    var sign = 1;
                    foreach (var spot in Enum.GetValues<Spot>())
                    {
                        if (Find(desk, card, spot) is not { } found)
                        {
                            continue;
                        }

                        var outcome = await DragAsync(desk, card, kind, found, sign, locked).ConfigureAwait(true);
                        sign = -sign;
                        tested++;
                        results.Add($"{spot.ToString().ToLowerInvariant()} {outcome.Text}");
                        if (!outcome.Ok)
                        {
                            failures++;
                            if (!failedSpots.TryGetValue(spot.ToString(), out var list))
                            {
                                failedSpots[spot.ToString()] = list = [];
                            }

                            list.Add(kind.Id.Value);
                        }
                    }

                    var verdict = results.TrueForAll(r => !r.Contains("FAIL", StringComparison.Ordinal)) ? "PASS" : "FAIL";
                    Say($"{verdict} {kind.Id.Value}: {(results.Count == 0 ? "no spot found" : string.Join("; ", results))}");
                }
            }

            Say($"first-drag-check: {tested} press(es) on {kinds.Count} kind(s)");
            foreach (var (spot, list) in failedSpots)
            {
                Say($"  failed on a {spot.ToLowerInvariant()}: {list.Count} kind(s): {string.Join(", ", list)}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the first drag check failed", ex);
            failures++;
            Say($"first-drag-check: failed: {ex.Message}");
        }
        finally
        {
            Say(failures == 0 ? "first-drag-check: PASS" : $"first-drag-check: FAIL ({failures})");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            await File.WriteAllLinesAsync(outPath, lines).ConfigureAwait(true);
            app.Exit();
        }
    }

    // ---- one press ------------------------------------------------------------------------------

    /// <summary>What stands under a test's press.</summary>
    private enum Spot
    {
        Row,
        Button,
        Track,
        Thumb,
        Text,
        Chart,
        Empty,
    }

    private readonly record struct Found(Spot Spot, FrameworkElement Part, UIElement Source, Point At, Routing Routing);

    private readonly record struct Outcome(bool Ok, string Text);

    /// <summary>
    /// The press at <paramref name="found"/>, a move past the threshold out to <see cref="Reach"/> (left or right by
    /// <paramref name="sign"/>) and the release, through the card's own steps; then whether the card moved as it should.
    /// </summary>
    private static async Task<Outcome> DragAsync(Desk desk, WidgetCard card, WidgetDescriptor kind, Found found, int sign, bool locked)
    {
        var model = () => desk.Host.State.Views[0].Find(card.Model.Id)?.Rect;
        var before = model();
        var held = true;
        desk.Surface.CheckButton = () => held;
        var taken = desk.Surface.CheckForegroundMidPress;
        try
        {
            // What the part does with the press before the card hears it: a button or a text box takes the focus.
            if (found.Routing.FocusesOnPress && found.Part is Control control)
            {
                _ = control.Focus(FocusState.Pointer);
            }

            card.CheckPress(found.Source, found.At, found.Routing.Handled);
            await Task.Delay(16).ConfigureAwait(true);
            const int steps = 8;
            for (var step = 1; step <= steps; step++)
            {
                var dx = sign * Math.Max(3.5, Reach * step / steps);
                _ = card.CheckMove(new Point(found.At.X + dx, found.At.Y), found.Routing.HandlesMoves);
                await Task.Delay(16).ConfigureAwait(true);
            }

            held = false;
            card.CheckRelease();
            desk.Surface.CheckLetGo();
            await Task.Delay(120).ConfigureAwait(true);
        }
        finally
        {
            held = false;
            desk.Surface.CheckButton = null;
        }

        var moved = model() != before;
        var midPress = desk.Surface.CheckForegroundMidPress - taken;
        var focusKept = found.Routing.FocusesOnPress && FocusIsIn(found.Part);

        // What should happen: a widget that is not locked moves by any press but a thumb's or a part's own drag; a desktop
        // icon moves locked too ([S-12950]).
        var movable = !(locked && card.View is not IDesktopIconView) && card.View is not IWidgetOwnsDrags;
        var should = movable && !found.Routing.OwnDrag && found.Spot != Spot.Thumb;
        var ok = moved == should && midPress == 0 && !(moved && focusKept);
        var why = found.Routing.Why;
        var text = $"{(ok ? "ok" : "FAIL")} {(moved ? "moved" : "stayed")}{(should ? string.Empty : " (its own)")}"
            + (midPress > 0 ? ", took the foreground mid-press" : string.Empty)
            + (moved && focusKept ? ", left focus in the part" : string.Empty)
            + (ok ? string.Empty : $" [{found.Part.GetType().Name}{(why is null ? string.Empty : ": " + why)}]");
        return new Outcome(ok, text);
    }

    private static bool FocusIsIn(UIElement part)
    {
        if (part.XamlRoot is not { } root)
        {
            return false;
        }

        for (var at = FocusManager.GetFocusedElement(root) as DependencyObject; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (ReferenceEquals(at, part))
            {
                return true;
            }
        }

        return false;
    }

    // ---- what stands under a press --------------------------------------------------------------

    /// <summary>
    /// What a press at a point meets before the card hears it, as XAML routes it: <see cref="Handled"/>, a part took the
    /// press (the card's plain press handler never runs); <see cref="HandlesMoves"/>, it handles the moves after it too;
    /// <see cref="FocusesOnPress"/>, it takes the focus on the press; <see cref="OwnDrag"/>, the drag is the part's own.
    /// </summary>
    private readonly record struct Routing(bool Handled, bool HandlesMoves, bool FocusesOnPress, bool OwnDrag, string? Why);

    /// <summary>
    /// The routing of a press on <paramref name="source"/>, walked up to <paramref name="card"/>: WinUI's controls as their
    /// sources route a left press (ButtonBase handles and focuses it and captures, its moves go on; Thumb handles it and its
    /// drag's moves; Slider's track handles it and the moves; the text boxes handle and focus it and select by the moves;
    /// ScrollViewer leaves it); this app's parts as their code does (a seek bar scrubs; a fence's body and items and the
    /// folder take the press for their own band and drags; a widget that is one button takes it).
    /// </summary>
    private static Routing Route(UIElement source, WidgetCard card)
    {
        for (var at = source as DependencyObject; at is not null && !ReferenceEquals(at, card); at = VisualTreeHelper.GetParent(at))
        {
            switch (at)
            {
                case Thumb:
                    return new(true, true, false, true, "a thumb drags itself");
                case ButtonBase { IsEnabled: true } button:
                    return new(true, false, button.IsTabStop, false, "a button takes the press");
                case ToggleSwitch { IsEnabled: true }:
                    return new(true, false, true, false, "a toggle switch takes the press");
                case Slider { IsEnabled: true }:
                    return new(true, true, false, false, "a slider's track takes the press and its moves");
                case TextBox or RichEditBox or PasswordBox:
                    return new(true, true, true, false, "a text box takes the press and selects by the moves");
                case MusicSeekBar { CanSeek: true }:
                    return new(true, true, false, false, "the seek bar scrubs from the press");
                case FrameworkElement fe when fe.GetType().Name is "FenceView" or "FolderView":
                    return new(true, false, false, true, "its own band and drags");
                case IWidgetButton when at is UIElement { IsHitTestVisible: true }:
                    return new(true, false, false, false, "the widget's one button takes the press");
            }
        }

        return new(false, false, false, false, null);
    }

    /// <summary>The topmost element of <paramref name="card"/> under <paramref name="at"/> (the surface's DIPs).</summary>
    private static UIElement? Top(Desk desk, WidgetCard card, Point at)
    {
        foreach (var element in VisualTreeHelper.FindElementsInHostCoordinates(desk.HostPoint(at), card))
        {
            return element;
        }

        return null;
    }

    /// <summary>A spot of <paramref name="spot"/>'s sort in <paramref name="card"/>, pressed where that part is on top; or null.</summary>
    private static Found? Find(Desk desk, WidgetCard card, Spot spot)
    {
        if (spot == Spot.Empty)
        {
            return Empty(desk, card);
        }

        foreach (var part in Parts(card.View).Where(p => Is(p, spot)))
        {
            foreach (var local in Probes(part, spot))
            {
                var at = desk.PointIn(part, local);
                if (!Inside(desk, card, at) || Top(desk, card, at) is not { } top)
                {
                    continue;
                }

                // A row is pressed where its press reaches an item: the row's band, or the row it stands in.
                if (!Within(top, part) && !(spot == Spot.Row && ItemOf(top, card) is not null))
                {
                    continue;
                }

                // A row whose press lands on a control inside it is that control's spot, not the row's.
                var routing = Route(top, card);
                if (spot is Spot.Row or Spot.Chart && routing.Handled && !routing.OwnDrag)
                {
                    continue;
                }

                return new Found(spot, part, top, at, routing);
            }
        }

        return null;
    }

    /// <summary>The item a press on <paramref name="node"/> reaches on its way up to <paramref name="card"/>, or null.</summary>
    private static FrameworkElement? ItemOf(DependencyObject node, WidgetCard card)
    {
        for (var at = node; at is not null && !ReferenceEquals(at, card); at = VisualTreeHelper.GetParent(at))
        {
            if (at is FrameworkElement fe && (ItemMark.IsItem(fe) || WidgetItems.IsItem(fe)))
            {
                return fe;
            }
        }

        return null;
    }

    private static Found? Empty(Desk desk, WidgetCard card)
    {
        Found? own = null;
        for (var row = 1; row < 8; row++)
        {
            for (var column = 1; column < 8; column++)
            {
                var at = desk.PointIn(card, new Point(card.ActualWidth * column / 8, card.ActualHeight * row / 8));
                if (Top(desk, card, at) is not { } top)
                {
                    continue;
                }

                var routing = Route(top, card);
                var onPart = false;
                for (var node = top as DependencyObject; node is not null && !ReferenceEquals(node, card); node = VisualTreeHelper.GetParent(node))
                {
                    onPart |= node is FrameworkElement fe && (ItemMark.IsItem(fe) || IsChart(fe));
                }

                if (!routing.Handled && !onPart)
                {
                    return new Found(Spot.Empty, card, top, at, routing);
                }

                // A view whose ground is its own drag's (a fence's body, the folder): pressed too, it must keep it.
                own ??= routing.OwnDrag && !onPart ? new Found(Spot.Empty, card, top, at, routing) : null;
            }
        }

        return own;
    }

    private static bool Is(FrameworkElement part, Spot spot) => spot switch
    {
        Spot.Row => ItemMark.IsItem(part),
        Spot.Button => part is ButtonBase or ToggleSwitch || (part is IWidgetButton && part.IsHitTestVisible),
        Spot.Track => part is Slider or MusicSeekBar { CanSeek: true },
        Spot.Thumb => part is Thumb && Ancestor<Slider>(part) is not null,
        Spot.Text => part is TextBox or RichEditBox or PasswordBox,
        Spot.Chart => IsChart(part),
        _ => false,
    };

    private static bool IsChart(FrameworkElement part)
    {
        var name = part.GetType().Name;
        return name.Contains("Chart", StringComparison.Ordinal) || name.Contains("Graph", StringComparison.Ordinal)
            || name.Contains("Spark", StringComparison.Ordinal) || name.Contains("Ring", StringComparison.Ordinal)
            || name.Contains("Donut", StringComparison.Ordinal) || name.Contains("Gauge", StringComparison.Ordinal);
    }

    /// <summary>Where on <paramref name="part"/> to press (its own DIPs): its centre; on a track, the end away from its thumb.</summary>
    private static IEnumerable<Point> Probes(FrameworkElement part, Spot spot)
    {
        var (w, h) = (part.ActualWidth, part.ActualHeight);
        if (spot == Spot.Track)
        {
            var fraction = part is Slider slider && slider.Maximum > slider.Minimum
                ? (slider.Value - slider.Minimum) / (slider.Maximum - slider.Minimum)
                : part is MusicSeekBar bar ? bar.Value : 0.5;
            yield return new Point(w * (fraction > 0.5 ? 0.12 : 0.88), h / 2);
            yield break;
        }

        yield return new Point(w / 2, h / 2);
        yield return new Point(w * 0.25, h / 2);
        yield return new Point(w * 0.75, h / 2);
    }

    private static IEnumerable<FrameworkElement> Parts(DependencyObject root)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is UIElement { Visibility: Visibility.Collapsed })
            {
                continue;
            }

            if (node is FrameworkElement { ActualWidth: > 4, ActualHeight: > 4 } fe)
            {
                yield return fe;
            }

            for (var i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
            {
                stack.Push(VisualTreeHelper.GetChild(node, i));
            }
        }
    }

    private static T? Ancestor<T>(DependencyObject node)
        where T : class
    {
        for (var at = VisualTreeHelper.GetParent(node); at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (at is T found)
            {
                return found;
            }
        }

        return null;
    }

    private static bool Within(DependencyObject node, DependencyObject part)
    {
        for (var at = node; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (ReferenceEquals(at, part))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Inside(Desk desk, WidgetCard card, Point at)
    {
        var box = desk.PointIn(card, default);
        return at.X > box.X + 2 && at.Y > box.Y + 2 && at.X < box.X + card.ActualWidth - 2 && at.Y < box.Y + card.ActualHeight - 2;
    }

    // ---- the desk -------------------------------------------------------------------------------

    /// <summary>A host and a surface off-screen, holding one batch of kinds on their samples.</summary>
    private sealed class Desk : IAsyncDisposable
    {
        private Desk(AppHost host, DesktopWindow surface, List<(WidgetDescriptor Kind, WidgetCard Card)> cards)
        {
            Host = host;
            Surface = surface;
            Cards = cards;
        }

        public AppHost Host { get; }

        public DesktopWindow Surface { get; }

        public List<(WidgetDescriptor Kind, WidgetCard Card)> Cards { get; }

        public static async Task<Desk> StartAsync(IReadOnlyList<WidgetDescriptor> kinds, bool locked)
        {
            var state = AppState.CreateDefault();
            state.Preferences.LockWidgets = locked;
            var view = state.Views[0];
            view.Widgets.Clear();

            // Parked off-screen: nothing of it is ever under the real mouse. Cards stand well apart, so a test's drag
            // never meets another card.
            var bounds = new Win32.Rect { Left = -32000, Top = -32000, Right = -32000 + 3600, Bottom = -32000 + 2400 };
            const double W = 3600, H = 2400 - 48;
            var placed = new List<(WidgetDescriptor Kind, WidgetInstance Widget)>();
            double x = 160, y = 120, rowHeight = 0;
            foreach (var kind in kinds)
            {
                var natural = kind.SizingOf().Natural;
                if (x + natural.Width + 160 > W)
                {
                    x = 160;
                    y += rowHeight + 120;
                    rowHeight = 0;
                }

                var widget = WidgetInstance.Create(kind.Id, new FracRect(x / W, y / H, natural.Width / W, natural.Height / H));
                view.Widgets.Add(widget);
                placed.Add((kind, widget));
                x += natural.Width + 240;
                rowHeight = Math.Max(rowHeight, natural.Height);
            }

            state.ShowViewOn(DisplayId, view.Id);
            var monitor = new MonitorTarget(DisplayId, IdResolved: true, "first-drag-check", bounds, bounds with { Bottom = bounds.Bottom - 48 }, IsPrimary: false);
            var host = new AppHost(DispatcherQueue.GetForCurrentThread(), AppStateStore.Detached(Path.Combine(Path.GetTempPath(), "themia-first-drag-check", "state.json")), state);
            var surface = new DesktopWindow(host, monitor);
            host.AdoptSurfaceForCheck(surface);
            surface.ShowSurface();

            // A diagnostic's wait, not a correctness path: the surface lays out and the intro ends.
            await Task.Delay(2500).ConfigureAwait(true);

            var cards = new List<(WidgetDescriptor, WidgetCard)>();
            foreach (var (kind, widget) in placed)
            {
                if (surface.CardFor(widget.Id) is not { } card)
                {
                    continue;
                }

                if (card.View is IWidgetSample sample)
                {
                    if (card.ShowsSetup)
                    {
                        card.RunForPreview();
                    }

                    try
                    {
                        await sample.ShowSampleAsync().ConfigureAwait(true);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(Scope, $"{kind.Id}: its sample threw: {ex.Message}");
                    }
                }

                cards.Add((kind, card));
            }

            await Task.Delay(800).ConfigureAwait(true);
            return new Desk(host, surface, cards);
        }

        /// <summary>A point of <paramref name="element"/> in the surface root's DIPs.</summary>
        public Point PointIn(UIElement element, Point local) => element.TransformToVisual(Surface.DebugRoot).TransformPoint(local);

        /// <summary>The surface root's DIP <paramref name="at"/> in the XAML root's own coordinates.</summary>
        public Point HostPoint(Point at) => Surface.DebugRoot.TransformToVisual(null).TransformPoint(at);

        public ValueTask DisposeAsync()
        {
            Surface.CheckButton = null;
            Surface.CloseSurface();
            Host.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    // ---- --probe: posted messages ---------------------------------------------------------------

    private const uint WmMouseMove = 0x0200;
    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;

    /// <summary>
    /// Posts a click (WM_MOUSEMOVE, WM_LBUTTONDOWN, WM_LBUTTONUP) to every window of the surface at a card's centre and says
    /// how many pointer events XAML raised: none, since WinUI's input reads pointer messages, not posted mouse ones.
    /// </summary>
    private static async Task ProbeAsync(List<WidgetDescriptor> kinds, Action<string> say)
    {
        await using var desk = await Desk.StartAsync(kinds, locked: false).ConfigureAwait(true);
        var tree = new List<(nint Hwnd, string Class)>();
        Walk(desk.Surface.Hwnd, tree);
        var card = desk.Cards[0].Card;
        var events = 0;
        desk.Surface.DebugRoot.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, _) => events++), handledEventsToo: true);
        desk.Surface.DebugRoot.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => events++), handledEventsToo: true);
        var centre = desk.PointIn(card, new Point(card.ActualWidth / 2, card.ActualHeight / 2));
        foreach (var (hwnd, name) in tree)
        {
            events = 0;
            var scale = desk.Surface.BandScale;
            var origin = new Win32.Point();
            _ = Win32.ClientToScreen(hwnd, ref origin);
            var x = -32000 + (int)Math.Round(centre.X * scale) - origin.X;
            var y = -32000 + (int)Math.Round(centre.Y * scale) - origin.Y;
            var at = (nint)(((y & 0xFFFF) << 16) | (x & 0xFFFF));
            _ = Win32.PostMessage(hwnd, WmMouseMove, 0, at);
            await Task.Delay(60).ConfigureAwait(true);
            _ = Win32.PostMessage(hwnd, WmLButtonDown, 1, at);
            await Task.Delay(60).ConfigureAwait(true);
            _ = Win32.PostMessage(hwnd, WmLButtonUp, 0, at);
            await Task.Delay(200).ConfigureAwait(true);
            say($"  posted a click to {name}: XAML raised {events} pointer event(s)");
        }
    }

    private static void Walk(nint parent, List<(nint, string)> into)
    {
        into.Add((parent, ClassOf(parent)));
        for (var child = Win32.FindWindowEx(parent, 0, null, null); child != 0; child = Win32.FindWindowEx(parent, child, null, null))
        {
            Walk(child, into);
        }
    }

    private static unsafe string ClassOf(nint hwnd)
    {
        var buffer = stackalloc char[256];
        var length = Win32.GetClassName(hwnd, buffer, 256);
        return length > 0 ? new string(buffer, 0, length) : "?";
    }
}
#endif
