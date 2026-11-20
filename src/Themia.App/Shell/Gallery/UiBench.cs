#if DEBUG
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Storage;
using Themia.Sdk;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-2360] Debug only: <c>Themia.exe --ui-bench &lt;out.txt&gt; [--frames n] [--repeat n]</c> measures what the edit
/// mode's everyday steps cost on a real surface: a real <see cref="AppHost"/> over a detached store (nothing of the
/// user's read or saved) and a real <see cref="DesktopWindow"/> on a 1920x1080 stand-in display parked off-screen, the
/// drag bench's 21 widgets on it and two views more. Each step is driven <c>--repeat</c> times (8) through the surface's
/// own controls (the edit bar's entries and the pages' heads clicked through their automation peers, so their Click
/// runs) and watched for <c>--frames</c> rendered frames (24) after it. Per step: the UI thread's milliseconds of the
/// step and its layout (median, 95th percentile), its CPU time over the whole step (the frames after it too, the
/// idle frames' own taken off), the frames longer than 16.7 ms and the longest, the layout passes, the elements put in
/// the tree, the page drawings, the bytes allocated on the UI thread and the collections.
/// </summary>
internal static partial class UiBench
{
    public const string Flag = "--ui-bench";

    private const string Scope = "ui-bench";
    private const string DisplayId = "ui-bench-display";

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is null)
        {
            Log.Error(Scope, $"usage: {Flag} <out.txt> [--frames n] [--repeat n]");
            app.Exit();
            return true;
        }

        var frames = int.TryParse(ArgAfter(args, "--frames"), out var f) && f > 0 ? f : 24;
        var repeat = int.TryParse(ArgAfter(args, "--repeat"), out var r) && r > 0 ? r : 8;
        _ = RunAsync(app, Path.GetFullPath(args[at + 1]), frames, repeat);
        return true;
    }

    private static string? ArgAfter(string[] args, string flag)
    {
        var at = Array.IndexOf(args, flag);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    private static async Task RunAsync(Application app, string outPath, int frames, int repeat)
    {
        var lines = new List<string>();
        AppHost? host = null;
        DesktopWindow? surface = null;
        Probe? probe = null;

        void Say(string line)
        {
            Console.WriteLine(line);
            lines.Add(line);
        }

        try
        {
            var (view, others, rows, icons) = Scene();
            var state = new AppState { Views = [view, .. others], ActiveViewId = view.Id };
            state.Design.WidgetCollisions = true;
            state.ShowViewOn(DisplayId, view.Id);

            var bounds = new Win32.Rect { Left = -32000, Top = -32000, Right = -32000 + 1920, Bottom = -32000 + 1080 };
            var monitor = new MonitorTarget(DisplayId, IdResolved: true, "ui-bench", bounds, bounds with { Bottom = bounds.Bottom - 48 }, IsPrimary: false);
            host = new AppHost(DispatcherQueue.GetForCurrentThread(), AppStateStore.Detached(Path.Combine(Path.GetTempPath(), "themia-ui-bench", "state.json")), state);
            surface = new DesktopWindow(host, monitor);
            surface.ShowSurface();

            // A diagnostic's wait, not a correctness path: the surface settles and its intro ends.
            await Task.Delay(2500).ConfigureAwait(true);
            Touch(host, surface, rows);
            await Task.Delay(800).ConfigureAwait(true);

            var root = surface.DebugRoot;
            probe = new Probe(root, frames);
            var cards = view.Widgets.Select(w => surface.CardFor(w.Id)).OfType<WidgetCard>().ToList();
            Say($"ui-bench: {cards.Count} cards ({icons.Count} icons) and {others.Count} views more, surface {surface.Metrics.Width:0}x{surface.Metrics.Height:0} DIPs " +
                $"at scale {root.XamlRoot?.RasterizationScale ?? 1:0.##}; {repeat} times each, {frames} frames watched after each; " +
                $"UI thread's cycle counter at {probe.CyclesPerMs / 1e6:0.00} GHz; Debug build (XAML and JIT slower than the Release AOT build)");
            Say(Probe.Header);

            // The frames' own cost with nothing done: what each step's CPU time is less of.
            await probe.Idle(repeat, Say).ConfigureAwait(true);

            await probe.Pair("edit mode: enter", "edit mode: leave", () => host.SetEditMode(true), () => host.SetEditMode(false), repeat, Say).ConfigureAwait(true);
            host.SetEditMode(true);
            await probe.Settle().ConfigureAwait(true);

            // A change of the state that changes nothing a card shows (a setting of the app's, a widget's rect kept): what
            // every change costs the surface (every card laid again).
            await probe.Run("a change of the state (nothing shown)", () => host.Mutate(_ => { }, relayout: true, undo: null), repeat, Say).ConfigureAwait(true);

            // One widget: selected (its page opens, [S-2343]) and the selection cleared (its page goes).
            var clock = rows[0][0];
            await probe.Pair("select one widget (page opens)", "deselect (page closes)", () => host.Select(clock), () => host.Select(null), repeat, Say).ConfigureAwait(true);

            // Its page by its entry on the bar, closed and opened again; its tabs.
            host.Select(clock);
            await probe.Settle().ConfigureAwait(true);
            await probe.Pair("selection page (1): close by entry", "selection page (1): open by entry", () => Click(root, "selection"), () => Click(root, "selection"), repeat, Say).ConfigureAwait(true);
            await probe.Pair("selection page (1): Appearance tab", "selection page (1): Settings tab", () => Tab(root, 1), () => Tab(root, 0), repeat, Say).ConfigureAwait(true);

            // Several widgets: the page of several.
            host.Select(null);
            await probe.Settle().ConfigureAwait(true);
            string[] several = [rows[1][0], rows[1][1], rows[1][2]];
            await probe.Pair("select 3 widgets (page opens)", "deselect 3 (page closes)", () => host.SelectMany(view.Id, several), () => host.Select(null), repeat, Say).ConfigureAwait(true);
            host.SelectMany(view.Id, several);
            await probe.Settle().ConfigureAwait(true);
            await probe.Pair("selection page (3): close by entry", "selection page (3): open by entry", () => Click(root, "selection"), () => Click(root, "selection"), repeat, Say).ConfigureAwait(true);
            if (All<PageTabButton>(root).Count() > 1)
            {
                await probe.Pair("selection page (3): Appearance tab", "selection page (3): Settings tab", () => Tab(root, 1), () => Tab(root, 0), repeat, Say).ConfigureAwait(true);
            }

            host.Select(null);
            await probe.Settle().ConfigureAwait(true);

            // Every page of the bar, opened and closed by its entry.
            foreach (var page in SettingsPages.Order)
            {
                await probe.Pair($"{page} page: open", $"{page} page: close", () => Click(root, "page:" + page), () => Click(root, "page:" + page), repeat, Say).ConfigureAwait(true);
            }

            // A card opened and closed by its head: a view's on Views, the first section of Appearance (its live preview over it).
            Click(root, "page:" + SettingsPageId.Views);
            await probe.Settle().ConfigureAwait(true);
            var viewCard = ViewsMenu.Keys.View(others[0].Id) + "#head";
            await probe.Pair("Views: expand a view's card", "Views: collapse it", () => Click(root, viewCard), () => Click(root, viewCard), repeat, Say).ConfigureAwait(true);
            Click(root, "page:" + SettingsPageId.Views);

            Click(root, "page:" + SettingsPageId.Appearance);
            await probe.Settle().ConfigureAwait(true);
            if (Find<PageView>(root, _ => true)?.DebugCards().FirstOrDefault() is { } section)
            {
                await probe.Pair($"Appearance: expand '{section}'", "Appearance: collapse it", () => Click(root, section + "#head"), () => Click(root, section + "#head"), repeat, Say).ConfigureAwait(true);
            }

            // A setting switched on the long Appearance page: new rows, the whole page drawn again, every card repainted.
            const string tabular = "appearance/text/tabularNumerals";
            await probe.Pair("Appearance: switch a setting", "Appearance: switch it back", () => Toggle(root, tabular), () => Toggle(root, tabular), repeat, Say).ConfigureAwait(true);

            Click(root, "page:" + SettingsPageId.Appearance);

            // A setting changed on a page that lays no card out again: Lock widgets on General, on and off ([S-3531]: the page shows it).
            Click(root, "page:" + SettingsPageId.General);
            await probe.Settle().ConfigureAwait(true);
            await probe.Pair("General: toggle Lock widgets", "General: toggle it back", () => Toggle(root, SettingsMenu.Keys.LockWidgets), () => Toggle(root, SettingsMenu.Keys.LockWidgets), repeat, Say).ConfigureAwait(true);
            Click(root, "page:" + SettingsPageId.General);
            await probe.Settle().ConfigureAwait(true);

            // The add page, opened and closed by Add; its subjects.
            await probe.Pair("add page: open", "add page: close", () => Click(root, "add"), () => Click(root, "add"), repeat, Say).ConfigureAwait(true);
            Click(root, "add");
            await probe.Settle().ConfigureAwait(true);
            AddFolderId[] subjects = [AddFolderId.Time, AddFolderId.System, AddFolderId.Weather, AddFolderId.Start];
            var subject = 0;
            await probe.Run("add page: switch subject", () => Find<AddPageView>(root, _ => true)?.ShowItem(subjects[subject++ % subjects.Length]), repeat * 2, Say).ConfigureAwait(true);
            Click(root, "add");
            await probe.Settle().ConfigureAwait(true);

            // A card's menu, opened (and closed untimed).
            var menuCard = surface.CardFor(rows[0][1])!;
            await probe.Run("card menu: open", () => surface.OpenCardMenu(menuCard, null), repeat, Say, after: () =>
            {
                foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(root.XamlRoot))
                {
                    popup.IsOpen = false;
                }
            }).ConfigureAwait(true);

            host.SetEditMode(false);
            await probe.Settle().ConfigureAwait(true);

            // What a whole snapshot of a state document of a real size costs (taken before a change to take a throwing one
            // back): written as the store writes it, and read back as an import or a take-back reads it.
            var document = Document(77 * 1024);
            var json = JsonSerializer.Serialize(document, StateJson.AppState);
            Say($"ui-bench: a state document of {json.Length / 1024.0:0.0} KB ({document.Views.Sum(v => v.Widgets.Count)} widgets in {document.Views.Count} views)");
            await probe.Run("state snapshot: write (UTF-8 bytes)", () => _ = JsonSerializer.SerializeToUtf8Bytes(document, StateJson.AppState), repeat, Say).ConfigureAwait(true);
            await probe.Run("state snapshot: read back", () => _ = JsonSerializer.Deserialize(json, StateJson.AppState), repeat, Say).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the ui bench failed", ex);
            Say($"ui-bench: failed: {ex.Message}");
        }
        finally
        {
            probe?.Dispose();
            surface?.CloseSurface();
            host?.Dispose();
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            await File.WriteAllLinesAsync(outPath, lines).ConfigureAwait(true);
            app.Exit();
        }
    }

    /// <summary>A click on the button tagged <paramref name="tag"/>, through its automation peer (its Click runs); a toggle's toggled.</summary>
    private static void Click(FrameworkElement root, string tag)
    {
        if (Find<Control>(root, c => c.Tag is string t && t == tag) is not { } control)
        {
            throw new InvalidOperationException($"nothing tagged '{tag}' to click");
        }

        switch (FrameworkElementAutomationPeer.CreatePeerForElement(control))
        {
            case IInvokeProvider invoke:
                invoke.Invoke();
                break;
            case IToggleProvider toggle:
                toggle.Toggle();
                break;
            default:
                throw new InvalidOperationException($"'{tag}' cannot be clicked");
        }
    }

    /// <summary>
    /// A switch's row clicked: what its click does (a toggle's automation peer flips the switch without its Click, which
    /// is what does it).
    /// </summary>
    private static void Toggle(FrameworkElement root, string key)
    {
        if (Find<PageView>(root, _ => true) is not { } page || !page.DebugClick(key))
        {
            throw new InvalidOperationException($"no switch '{key}' to click");
        }
    }

    /// <summary>A click on the selection page's tab number <paramref name="index"/>.</summary>
    private static void Tab(FrameworkElement root, int index)
    {
        var tabs = All<PageTabButton>(root).ToList();
        if (index >= tabs.Count)
        {
            throw new InvalidOperationException($"the page has {tabs.Count} tabs, not {index + 1}");
        }

        ((IInvokeProvider)FrameworkElementAutomationPeer.CreatePeerForElement(tabs[index])).Invoke();
    }

    private static T? Find<T>(DependencyObject root, Func<T, bool> wanted)
        where T : DependencyObject
        => All<T>(root).FirstOrDefault(wanted);

    private static IEnumerable<T> All<T>(DependencyObject root)
        where T : DependencyObject
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is T found)
            {
                yield return found;
            }

            for (var i = VisualTreeHelper.GetChildrenCount(node) - 1; i >= 0; i--)
            {
                stack.Push(VisualTreeHelper.GetChild(node, i));
            }
        }
    }

    /// <summary>
    /// The drag bench's scene (three rows of cards that will touch, six alone, five desktop icons) and two views more
    /// with a few widgets each, so the Views page has cards to open.
    /// </summary>
    private static (ViewState View, List<ViewState> Others, List<List<string>> Rows, List<string> Icons) Scene()
    {
        var view = ViewState.Create("Bench");
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

        var others = new List<ViewState>();
        foreach (var name in new[] { "Work", "Play" })
        {
            var other = ViewState.Create(name);
            other.Widgets.Add(WidgetInstance.Create(new WidgetId("clock"), new FracRect(0.1, 0.1, 0.1, 0.1)));
            other.Widgets.Add(WidgetInstance.Create(new WidgetId("memory"), new FracRect(0.3, 0.1, 0.1, 0.1)));
            others.Add(other);
        }

        return (view, others, rows, icons);
    }

    /// <summary>A state document at least <paramref name="bytes"/> long: views of the bench's kinds, as many as it takes.</summary>
    private static AppState Document(int bytes)
    {
        var state = new AppState();
        string[] kinds = ["clock", "date", "battery", "cpu", "memory", "gpu", "wifiToggle", "countdown", "stopwatch", "moon", "pomodoro", "header", "desktopItem"];
        while (JsonSerializer.Serialize(state, StateJson.AppState).Length < bytes)
        {
            var view = ViewState.Create($"View {state.Views.Count + 1}");
            for (var i = 0; i < kinds.Length; i++)
            {
                view.Widgets.Add(WidgetInstance.Create(new WidgetId(kinds[i]), new FracRect(0.05 + (0.07 * i), 0.1, 0.06, 0.1)));
            }

            state.Views.Add(view);
        }

        state.ActiveViewId = state.Views[0].Id;
        return state;
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

    /// <summary>
    /// What a step costs, watched on the surface's root: the layout passes (its <c>LayoutUpdated</c>), the rendered frames
    /// (<c>CompositionTarget.Rendering</c>), the UI thread's own cycles (<c>QueryThreadCycleTime</c>: what the thread ran,
    /// whatever else the machine was doing), its allocations and the collections, and the elements in the tree before and after.
    /// </summary>
    private sealed partial class Probe : IDisposable
    {
        public const string Header =
            "step                                    | sync ms med   p95 | cpu ms med   p95 | frames>20ms worst ms | layouts | new elems | drawings | alloc KB | gc0/1/2";

        /// <summary>
        /// A frame that took longer than a 60 Hz frame: past its 16.7 ms by more than the jitter the idle frames show here
        /// (their longest about 18.5 ms), so a missed frame and not the timer's spread.
        /// </summary>
        private const double LongFrameMs = 20;

        private readonly FrameworkElement _root;
        private readonly int _frames;
        private readonly List<long> _stamps = [];
        private int _layouts;
        private bool _recording;
        private TaskCompletionSource? _frame;
        private double _idleCyclesPerFrame;

        public Probe(FrameworkElement root, int frames)
        {
            _root = root;
            _frames = frames;
            _root.LayoutUpdated += OnLayout;
            CompositionTarget.Rendering += OnRendering;
            CyclesPerMs = Calibrate();
        }

        /// <summary>The UI thread's cycle counter's rate: the fastest of a few short spins (a spin the machine preempted counts fewer).</summary>
        public double CyclesPerMs { get; }

        public void Dispose()
        {
            _root.LayoutUpdated -= OnLayout;
            CompositionTarget.Rendering -= OnRendering;
        }

        private void OnLayout(object? sender, object e) => _layouts++;

        private void OnRendering(object? sender, object e)
        {
            if (_recording)
            {
                _stamps.Add(Stopwatch.GetTimestamp());
            }

            var waiting = _frame;
            _frame = null;
            waiting?.TrySetResult();
        }

        private Task NextFrame()
        {
            _frame ??= new TaskCompletionSource();
            return _frame.Task;
        }

        private async Task Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                await NextFrame().ConfigureAwait(true);
            }
        }

        /// <summary>The surface left to settle between steps (posted drawings, a glide).</summary>
        public Task Settle() => Frames(_frames);

        /// <summary>Nothing done, <paramref name="repeat"/> times: the frames' own CPU time, taken off every step's.</summary>
        public async Task Idle(int repeat, Action<string> say)
        {
            var samples = new List<Sample>();
            for (var i = 0; i < repeat; i++)
            {
                samples.Add(await Measure(() => { }).ConfigureAwait(true));
            }

            _idleCyclesPerFrame = Median(samples.Select(s => s.WindowCycles / (double)Math.Max(1, s.Frames)).ToList());
            say(Row("idle (nothing done)", samples));
        }

        /// <summary><paramref name="act"/> and <paramref name="back"/> one after the other, <paramref name="repeat"/> times: two rows.</summary>
        public async Task Pair(string name, string backName, Action act, Action back, int repeat, Action<string> say)
        {
            var there = new List<Sample>();
            var again = new List<Sample>();
            for (var i = 0; i < repeat; i++)
            {
                there.Add(await Measure(act).ConfigureAwait(true));
                again.Add(await Measure(back).ConfigureAwait(true));
            }

            say(Row(name, there));
            say(Row(backName, again));
        }

        public async Task Run(string name, Action act, int repeat, Action<string> say, Action? after = null)
        {
            var samples = new List<Sample>();
            for (var i = 0; i < repeat; i++)
            {
                samples.Add(await Measure(act).ConfigureAwait(true));
                if (after is not null)
                {
                    after();
                    await Settle().ConfigureAwait(true);
                }
            }

            say(Row(name, samples));
        }

        private async Task<Sample> Measure(Action act)
        {
            await Frames(3).ConfigureAwait(true);
            var before = Census();
            var drawingsBefore = Drawings(before);
            var gc = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
            var layouts = _layouts;
            _stamps.Clear();
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var cycles = ThreadCycles();
            _recording = true;
            var started = Stopwatch.GetTimestamp();

            act();
            _root.UpdateLayout();

            var syncMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var syncCycles = ThreadCycles() - cycles;
            await Frames(_frames).ConfigureAwait(true);
            _recording = false;
            var windowCycles = ThreadCycles() - cycles;
            var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            var passes = _layouts - layouts;
            var collections = (GC.CollectionCount(0) - gc.Item1, GC.CollectionCount(1) - gc.Item2, GC.CollectionCount(2) - gc.Item3);

            var after = Census();
            var created = after.Count(e => !before.Contains(e));
            var drawn = Drawings(after) - drawingsBefore;

            var intervals = new List<double>(_stamps.Count);
            var last = started;
            foreach (var stamp in _stamps)
            {
                intervals.Add(Stopwatch.GetElapsedTime(last, stamp).TotalMilliseconds);
                last = stamp;
            }

            return new Sample(syncMs, syncCycles, windowCycles, _stamps.Count, intervals.Count(i => i > LongFrameMs), intervals.DefaultIfEmpty(0).Max(), passes, created, Math.Max(0, drawn), bytes, collections);
        }

        /// <summary>Every element in the surface's tree and its open popups'.</summary>
        private HashSet<DependencyObject> Census()
        {
            var seen = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);
            foreach (var element in All<DependencyObject>(_root))
            {
                seen.Add(element);
            }

            if (_root.XamlRoot is { } xamlRoot)
            {
                foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
                {
                    seen.Add(popup);
                    if (popup.Child is { } child)
                    {
                        foreach (var element in All<DependencyObject>(child))
                        {
                            seen.Add(element);
                        }
                    }
                }
            }

            return seen;
        }

        /// <summary>How many times the pages in the tree have been drawn.</summary>
        private static int Drawings(HashSet<DependencyObject> tree) => tree.OfType<PageView>().Sum(p => p.Drawings);

        private string Row(string name, List<Sample> samples)
        {
            var sync = samples.Select(s => s.SyncMs).ToList();
            var cpu = samples.Select(s => Math.Max(0, s.WindowCycles - (_idleCyclesPerFrame * s.Frames)) / CyclesPerMs).ToList();
            var n = samples.Count;
            return $"{name,-40}| {Median(sync),8:0.0} {P95(sync),5:0.0} | {Median(cpu),7:0.0} {P95(cpu),5:0.0} | " +
                $"{samples.Sum(s => s.Long),5}/{samples.Sum(s => s.Frames),-4} {samples.Max(s => s.Worst),6:0.0} | " +
                $"{samples.Average(s => s.Layouts),7:0.0} | {samples.Average(s => s.Created),9:0} | {samples.Average(s => s.Drawings),8:0.0} | " +
                $"{samples.Average(s => s.Bytes) / 1024,8:0} | {samples.Sum(s => s.Collections.Item1)}/{samples.Sum(s => s.Collections.Item2)}/{samples.Sum(s => s.Collections.Item3)} (n={n})";
        }

        private static double Median(List<double> values)
        {
            values.Sort();
            return values.Count == 0 ? 0 : values[values.Count / 2];
        }

        private static double P95(List<double> values)
        {
            values.Sort();
            return values.Count == 0 ? 0 : values[Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * 0.95) - 1)];
        }

        private static double Calibrate()
        {
            var best = 0.0;
            for (var i = 0; i < 8; i++)
            {
                var cycles = ThreadCycles();
                var started = Stopwatch.GetTimestamp();
                while (Stopwatch.GetElapsedTime(started).TotalMilliseconds < 10)
                {
                    // ignore: a spin, measured.
                }

                var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                best = Math.Max(best, (ThreadCycles() - cycles) / ms);
            }

            return best;
        }

        private static long ThreadCycles() => QueryThreadCycleTime(GetCurrentThread(), out var cycles) ? (long)cycles : 0;

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool QueryThreadCycleTime(nint thread, out ulong cycles);

        [LibraryImport("kernel32.dll")]
        private static partial nint GetCurrentThread();

        private sealed record Sample(
            double SyncMs,
            long SyncCycles,
            long WindowCycles,
            int Frames,
            int Long,
            double Worst,
            int Layouts,
            int Created,
            int Drawings,
            long Bytes,
            (int, int, int) Collections);
    }
}
#endif
