#if DEBUG
using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1883] Debug only: <c>Themia.exe --check switch &lt;rounds&gt; [&lt;out.txt&gt;] [--ordinary | --show &lt;view&gt;] [--per-view] [--dwell ms]</c>
/// measures what switching views costs and whether it leaks.
/// <c>[--state &lt;state.json&gt;]</c> takes the views from a copy of a state file (read, never written).
/// <c>--edit-menus</c> ([S-3593]) measures what edit mode's menus hold: the ordinary view shown, edit
/// mode entered, a widget selected, every bar menu opened (how long each first open takes), then Done,
/// and once more opened after it (how long a rebuild takes).
/// <c>--busy &lt;pictures&gt; [--stream &lt;url&gt;] [--soak s]</c> ([S-6601]) is the long-running desktop
/// instead: two 2560x1440 stand-in displays, one with seven galleries over a picture folder, one with
/// a video stream, the visualizer and the system widgets, measured every 30 s for <c>--soak</c>
/// seconds, then the showcase views switched through on the first display <c>rounds</c> times.
/// <para>
/// A real <see cref="AppHost"/> over a detached state store (nothing is saved; the user's state,
/// accounts and caches are never read) and a real <see cref="DesktopWindow"/> on a stand-in display
/// parked off-screen (<see cref="StandInDesktop"/>), so the switch runs the surface's own path: <see cref="AppHost.ShowView"/>, the
/// relayout, card detach and the intro. The state holds the showcase views ([S-1810]) and one empty
/// view. Each round shows every view for <c>--dwell</c> ms, then the empty one, forces a full
/// collection and prints working set, private bytes, the managed heap, the cards still alive
/// ([S-1884]), the views attached against the live widgets ([S-1880]), the ticker's subscriptions,
/// threads and handles; a breach of the rule is marked LEAK and fails the check. After round 1 the figures must stay
/// in one band: anything that keeps growing is held by something.
/// </para>
/// <para>
/// <c>--per-view</c> also prints what each view costs while it is shown. <c>--show &lt;view&gt;</c>
/// shows one view, measures it, writes <c>out.png</c> and, with every list scrolled to its end,
/// <c>out.scrolled.png</c>; with more than one round it then alternates that view and the empty one.
/// <c>--ordinary</c> does the same for the baseline ([S-1885]): one view of eight everyday widgets.
/// </para>
/// </summary>
internal static class SwitchCheck
{
    private const string Scope = "switch-check";
    private const string DisplayId = "switch-check-display";
    private const string SecondId = "switch-check-display-2";

    /// <summary>The stand-in display, in physical pixels.</summary>
    private const int Width = 1920;
    private const int Height = 1080;

    public static RenderCommand Command { get; } = new(
        RenderVerb.Check,
        "switch",
        "<rounds> [<out.txt>] [--ordinary | --show <view>] [--per-view] [--dwell ms] [--state <state.json>] [--edit-menus] [--busy <pictures> [--stream <url>] [--soak s]]",
        """
        what switching views costs and whether it leaks, on a real host and surface off-screen: every showcase view and an
        empty one, <rounds> times, then working set, private bytes, the managed heap, the cards still alive [S-1884], the views
        attached against the live widgets [S-1880], the ticker's subscriptions, threads, handles; a breach is LEAK and fails [S-1883]
        --dwell ms             how long each view is shown (250)
        --per-view             what each view costs while it is shown
        --show <view>          that view alone, measured, drawn to <out>.png and, every list scrolled to its end, <out>.scrolled.png [S-1887]
        --ordinary             the same for one view of eight everyday widgets [S-1885]
        --state <state.json>   the views from a copy of a state file (read, never written)
        --edit-menus           what edit mode's menus hold and give back, each first open timed [S-3593]
        --busy <pictures>      the long-running desktop: two displays, seven galleries over that folder, a video stream
                               (--stream <url>), the visualizer and the system widgets, measured every 30 s for --soak s [S-6601]
        """,
        RunAsync);

    /// <summary>The state to take the views from, and the long-running desktop ([S-6601]).</summary>
    private sealed record Options(string? StatePath, string? Pictures, string? Stream, int SoakSeconds);

    private static Task<int> RunAsync(RenderRun run)
    {
        var rounds = int.TryParse(run.Arg(0), out var r) && r >= 1 ? r : throw run.Usage($"<rounds> {run.Arg(0)}: a whole number of at least 1");
        var outPath = run.Args.Count > 1 && !StartArguments.LooksLikeFlag(run.Args[1]) ? run.PathArg(1) : null;
        var editMenus = run.Has("--edit-menus");
        var ordinary = run.Has("--ordinary") || editMenus;
        var dwell = Whole(run, "--dwell", 250);
        var options = new Options(run.Option("--state"), run.Option("--busy"), run.Option("--stream"), Whole(run, "--soak", 0));
        return editMenus
            ? EditMenusAsync(outPath)
            : RunAsync(rounds, outPath, ordinary, dwell, run.Has("--per-view"), run.Option("--show"), options);
    }

    /// <summary>The whole number after <paramref name="option"/> (0 or more), or <paramref name="fallback"/> when it is not given.</summary>
    private static int Whole(RenderRun run, string option, int fallback)
        => run.Option(option) is not { } given ? fallback
            : int.TryParse(given, out var value) && value >= 0 ? value
            : throw run.Usage($"{option} {given}: a whole number");

    private static async Task<int> RunAsync(int rounds, string? outPath, bool ordinary, int dwell, bool byView, string? show, Options options)
    {
        var report = new CheckReport(Scope, outPath);
        void Say(string line) => report.Say(line);
        void Measured((string Line, bool Leak) measure) => report.Say(measure.Line, measure.Leak ? 1 : 0);
        StandInDesktop? desk = null;

        try
        {
            var busy = options.Pictures is not null;
            var (width, height) = busy ? (2560, 1440) : (Width, Height);
            var monitor = StandInDesktop.Display(DisplayId, StandInDesktop.OffScreen(width, height));
            var screen = new ScreenMetrics(width, height) { Allowed = GridGeometry.AllowedArea(monitor.Info.Bounds, monitor.Info.WorkArea) };

            var empty = ViewState.Create("Empty");
            var state = new AppState { Views = [empty], ActiveViewId = empty.Id };

            if (options.StatePath is { } source)
            {
                // A copy of a state file: parsed with the app's own trust boundary, never written back.
                state = StateDocument.Parse(File.ReadAllText(source)).State
                    ?? throw new InvalidDataException($"{source} is not a state document");
                state.Views.Add(empty);
            }
            else if (ordinary)
            {
                state.Views.Insert(0, OrdinaryView());
            }
            else
            {
                var kinds = ModuleCatalog.Widgets.Select(k => new ShowcaseKind(k.Id, k.Name, k.Glyph)).ToList();
                ShowcaseViews.Replace(state, ShowcaseViews.Build(kinds, screen));
            }

            ViewState? galleries = null;
            ViewState? feeds = null;

            if (busy)
            {
                (galleries, feeds) = BusyViews(Path.GetFullPath(options.Pictures!), options.Stream);
                state.Views.Insert(0, feeds);
                state.Views.Insert(0, galleries);
            }

            state.ShowViewOn(DisplayId, empty.Id);
            if (busy)
            {
                state.ShowViewOn(SecondId, empty.Id);
            }

            desk = StandInDesktop.Create(Scope, state);
            var host = desk.Host;
            var surface = desk.Add(monitor);
            if (busy)
            {
                desk.Add(StandInDesktop.Display(SecondId, StandInDesktop.OffScreen(width, height, left: width + 100)));
            }

            desk.Show();
            await Task.Delay(1000).ConfigureAwait(true);

            // [S-6612] As the app does at start, so the process measured is the app's.
            MemoryPriority.Lower();

            Say($"switch-check: {state.Views.Count} views, {state.Views.Sum(v => v.Widgets.Count)} widgets, dwell {dwell} ms, gen0 budget at most {Gen0Budget()}, memory priority {MemoryPriority.Current()?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}");
            Measured(Measure("start (empty view)", host));

            if (busy)
            {
                await SoakAsync(desk, state, galleries!, feeds!, empty, options.SoakSeconds, rounds, dwell, Measured).ConfigureAwait(true);
                return await report.FinishAsync().ConfigureAwait(true);
            }

            if (ordinary || show is not null)
            {
                var view = ordinary ? state.Views[0] : state.Views.First(v => string.Equals(v.Name, show, StringComparison.OrdinalIgnoreCase));
                host.ShowView(DisplayId, view.Id);
                await Task.Delay(Math.Max(dwell, 3000)).ConfigureAwait(true);
                Measured(Measure($"'{view.Name}' shown ({view.Widgets.Count} widgets)", host, view));
                if (outPath is not null)
                {
                    await RenderHarness.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, ".png"), Scope).ConfigureAwait(true);

                    // Every list scrolled to its end: what a scroll builds ([S-1887]) is drawn too.
                    var scrolled = 0;
                    foreach (var scroller in Descendants(surface.DebugRoot).OfType<ScrollViewer>())
                    {
                        scrolled += scroller.ChangeView(null, scroller.ScrollableHeight, null, disableAnimation: true) ? 1 : 0;
                    }

                    await Task.Delay(1500).ConfigureAwait(true);
                    await RenderHarness.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, ".scrolled.png"), Scope).ConfigureAwait(true);
                    Measured(Measure($"'{view.Name}' {scrolled} list(s) scrolled to the end", host, view));
                }

                // a timing, not a wait: what the view holds once it has been shown 5 s more.
                await Task.Delay(5000).ConfigureAwait(true);
                Measured(Measure($"'{view.Name}' after 5 s more", host, view));
                host.ShowView(DisplayId, empty.Id);
                await Task.Delay(1000).ConfigureAwait(true);
                Measured(Measure("empty view again", host));

                // More rounds: this one view and the empty one, in turn.
                for (var round = 2; round <= rounds; round++)
                {
                    host.ShowView(DisplayId, view.Id);

                    // a timing, not a wait: the view shown for the dwell.
                    await Task.Delay(dwell).ConfigureAwait(true);
                    host.ShowView(DisplayId, empty.Id);
                    await Task.Delay(500).ConfigureAwait(true);
                    Measured(Measure($"round {round}", host));
                }

                // a timing, not a wait: what lets go a sample later (the NVIDIA library, [S-437]) has let go by now.
                await Task.Delay(3000).ConfigureAwait(true);
                Measured(Measure("empty view after 3 s more", host));
                return await report.FinishAsync().ConfigureAwait(true);
            }

            // --per-view: what each view costs while shown, over the empty view, averaged over rounds 2..n.
            var perView = new Dictionary<string, double>(StringComparer.Ordinal);
            var before = PrivateMb();

            for (var round = 1; round <= rounds; round++)
            {
                foreach (var view in state.Views.ToList())
                {
                    host.ShowView(DisplayId, view.Id);

                    // a timing, not a wait: the view shown for the dwell.
                    await Task.Delay(dwell).ConfigureAwait(true);

                    if (byView)
                    {
                        // What the view costs while it is shown, over the empty view just before it.
                        Collect();
                        var shownMb = PrivateMb();
                        if (round > 1)
                        {
                            perView[view.Name] = perView.GetValueOrDefault(view.Name) + (shownMb - before);
                        }

                        host.ShowView(DisplayId, empty.Id);
                        await Task.Delay(100).ConfigureAwait(true);
                        Collect();
                        before = PrivateMb();
                    }
                }

                host.ShowView(DisplayId, empty.Id);
                await Task.Delay(1000).ConfigureAwait(true);
                Measured(Measure($"round {round}", host));
            }

            foreach (var (name, growth) in perView.OrderByDescending(p => p.Value).Take(20))
            {
                Say($"  {name,-28} {growth / Math.Max(1, rounds - 1),7:0.0} MB private while shown");
            }

            Collect();
            Say("views still alive after the last round: " + string.Join(", ", LiveCensus.LiveViews().Select(v => $"{v.Type} x{v.Count}")));
            Say("cards still alive after the last round: " + string.Join(", ", LiveCensus.LiveCardKinds().Select(v => $"{v.Type} x{v.Count}")));
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

    /// <summary>
    /// [S-3593] What edit mode's menus hold and give back: the ordinary view shown, edit mode entered
    /// (the bar built), a widget selected (its page), each of the bar's menus opened once
    /// and the settings down to Appearance (each first open timed), Done (the menus released), then edit mode
    /// and the settings again (a rebuild timed).
    /// </summary>
    private static async Task<int> EditMenusAsync(string? outPath)
    {
        var report = new CheckReport(Scope, outPath);
        void Say(string line) => report.Say(line);
        void Measured((string Line, bool Leak) measure) => report.Say(measure.Line, measure.Leak ? 1 : 0);
        StandInDesktop? desk = null;

        try
        {
            var view = OrdinaryView();
            var state = new AppState { Views = [view], ActiveViewId = view.Id };
            state.ShowViewOn(DisplayId, view.Id);

            desk = StandInDesktop.Create(Scope, state);
            var host = desk.Host;
            var surface = desk.Add(StandInDesktop.Display(DisplayId, StandInDesktop.OffScreen(Width, Height)));
            desk.Show();
            await Task.Delay(2000).ConfigureAwait(true);
            MemoryPriority.Lower();

            string Timed(string label, Action open)
            {
                var clock = Stopwatch.StartNew();
                open();
                surface.DebugRoot.UpdateLayout();
                return $"{label}: {clock.Elapsed.TotalMilliseconds:0} ms";
            }

            Measured(Measure("view shown, not editing", host, view));
            host.SetEditMode(true);
            await Task.Delay(1000).ConfigureAwait(true);
            Measured(Measure("edit mode (the bar built)", host, view));

            host.Select(view.Widgets[0].Id);
            await Task.Delay(1000).ConfigureAwait(true);
            Measured(Measure("a widget selected (its menu)", host, view));

            Say(Timed("first open: Display", () => surface.OpenPage(Core.Settings.SettingsPageId.Display, [])));
            Say(Timed("first open: View", () => surface.OpenPage(Core.Settings.SettingsPageId.Views, [])));
            Say(Timed("first open: General", () => surface.OpenPage(Core.Settings.SettingsPageId.General, [])));
            Say(Timed("first open: Appearance", () => surface.OpenPage(Core.Settings.SettingsPageId.Appearance, [])));
            await Task.Delay(1000).ConfigureAwait(true);
            Measured(Measure("every bar menu opened", host, view));

            host.SetEditMode(false);
            await Task.Delay(1500).ConfigureAwait(true);
            Measured(Measure("after Done (menus released)", host, view));

            host.SetEditMode(true);
            await Task.Delay(500).ConfigureAwait(true);
            Say(Timed("open again after Done: General", () => surface.OpenPage(Core.Settings.SettingsPageId.General, [])));
            host.SetEditMode(false);
            await Task.Delay(1500).ConfigureAwait(true);
            Measured(Measure("after Done again", host, view));
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

    /// <summary>A full, compacting collection whose finalizers have run.</summary>
    private static void Collect()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }

    private static double PrivateMb()
    {
        using var process = Process.GetCurrentProcess();
        return process.PrivateMemorySize64 / (1024.0 * 1024.0);
    }

    /// <summary>
    /// A full collection, then the process's figures and the census, checked against the rule: the
    /// views attached must be exactly the live widgets ([S-1880]) and no card of another view may be
    /// alive. A breach is printed as LEAK (and <c>Leak</c> is true).
    /// </summary>
    private static (string Line, bool Leak) Measure(string label, AppHost host, ViewState? shown = null, ViewState? alsoShown = null)
    {
        Collect();

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var mb = 1024.0 * 1024.0;
        var expected = (shown?.Widgets.Count ?? 0) + (alsoShown?.Widgets.Count ?? 0);
        var live = WidgetLiveness.Live(host.State, [DisplayId, SecondId], FeatureSet.None).Count;
        var cards = LiveCensus.LiveCards();

        // [S-2511] The system hub's one subscription is its own ([S-2503]), not a widget's.
        var system = AppServices.System;
        var hubTicks = system is { HoldsTicker: true } ? 1 : 0;
        var leak = !(cards == expected && LiveCensus.Attached == live && host.Ticker.SubscriptionCount - hubTicks <= live);
        var verdict = leak ? "  LEAK" : string.Empty;

        return ($"{label,-40} ws {process.WorkingSet64 / mb,7:0.0} MB  private {process.PrivateMemorySize64 / mb,7:0.0} MB  " +
            $"managed {GC.GetTotalMemory(forceFullCollection: false) / mb,6:0.0} MB (committed {GC.GetGCMemoryInfo().TotalCommittedBytes / mb:0.0})  cards alive {cards,4} (shown {expected})  " +
            $"views attached {LiveCensus.Attached,4} (live {live})  ticks {host.Ticker.SubscriptionCount,3}  " +
            $"system feeds {system?.FeedCount ?? 0,3} (wanted {system?.WantedCount ?? 0}, series {system?.SeriesCount ?? 0})  " +
            $"gallery sources {AppServices.Gallery?.SourceCount ?? 0,2} (lingering {AppServices.Gallery?.LingeringCount ?? 0}, pictures {AppServices.Gallery?.Pictures.Count ?? 0}, {(AppServices.Gallery?.Pictures.Bytes ?? 0) / mb:0.0} MB)  " +
            $"icons {AppIcons.Count,3}  {NativeHeaps.Describe()}  threads {process.Threads.Count,3}  handles {process.HandleCount,5}  " +
            $"nvml {LoadedCount(process, "nvml.dll")}{verdict}", leak);
    }

    /// <summary>[S-437] How many modules of that file name the process has loaded (the NVIDIA driver library: 0 when no GPU widget is shown).</summary>
    private static int LoadedCount(Process process, string fileName)
    {
        var count = 0;

        foreach (ProcessModule module in process.Modules)
        {
            using (module)
            {
                count += string.Equals(module.ModuleName, fileName, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            }
        }

        return count;
    }

    /// <summary>[S-6611] The GC's gen0 budget ceiling, which the embedded gen0 size lowers (the GC does not list that setting itself).</summary>
    private static string Gen0Budget()
        => GC.GetConfigurationVariables().TryGetValue("GCGen0MaxBudget", out var budget) && budget is IConvertible value
            ? $"{value.ToInt64(System.Globalization.CultureInfo.InvariantCulture) / (1024.0 * 1024.0):0.0} MB"
            : "unknown";

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var below in Descendants(child))
            {
                yield return below;
            }
        }
    }

    /// <summary>
    /// [S-6601] The long-running desktop: both busy views shown, measured every 30 s for
    /// <paramref name="soakSeconds"/>; then the showcase views switched through on the first display
    /// <paramref name="rounds"/> times while the second keeps its view; then both displays emptied.
    /// </summary>
    private static async Task SoakAsync(StandInDesktop desk, AppState state, ViewState galleries, ViewState feeds, ViewState empty, int soakSeconds, int rounds, int dwell, Action<(string Line, bool Leak)> say)
    {
        var host = desk.Host;
        host.ShowView(DisplayId, galleries.Id);
        host.ShowView(SecondId, feeds.Id);
        await Task.Delay(5000).ConfigureAwait(true);
        say(Measure("busy views shown", host, galleries, feeds));

        var watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < soakSeconds)
        {
            // a timing, not a wait: the soak is measured every 30 s.
            await Task.Delay(30_000).ConfigureAwait(true);
            say(Measure($"busy views after {watch.Elapsed.TotalSeconds:0} s", host, galleries, feeds));
        }

        var showcase = state.Views.Where(v => v != galleries && v != feeds && v != empty).ToList();
        for (var round = 1; round <= rounds && showcase.Count > 0; round++)
        {
            foreach (var view in showcase)
            {
                host.ShowView(DisplayId, view.Id);

                // a timing, not a wait: the view shown for the dwell.
                await Task.Delay(dwell).ConfigureAwait(true);
            }

            host.ShowView(DisplayId, galleries.Id);
            await Task.Delay(3000).ConfigureAwait(true);
            say(Measure($"showcase round {round}, busy views back", host, galleries, feeds));
        }

        host.ShowView(DisplayId, empty.Id);
        host.ShowView(SecondId, empty.Id);
        await Task.Delay(2000).ConfigureAwait(true);
        say(Measure("both displays empty", host));
    }

    /// <summary>
    /// [S-6601] The user's kind of desktop: seven galleries in every style over one picture folder on
    /// one display; on the other a video stream (when given), the visualizer, top processes and the
    /// system meters.
    /// </summary>
    private static (ViewState Galleries, ViewState Feeds) BusyViews(string pictures, string? stream)
    {
        var galleries = ViewState.Create("Busy galleries");
        string[] styles = ["slideshow", "grid", "scatter", "mosaic", "slideshow", "grid", "mosaic"];
        FracRect[] places =
        [
            new(0.02, 0.04, 0.30, 0.30), new(0.34, 0.04, 0.30, 0.45), new(0.66, 0.04, 0.32, 0.30),
            new(0.02, 0.38, 0.30, 0.30), new(0.66, 0.38, 0.32, 0.30), new(0.02, 0.72, 0.46, 0.25),
            new(0.50, 0.72, 0.48, 0.25),
        ];

        for (var i = 0; i < styles.Length; i++)
        {
            var widget = WidgetInstance.Create(new WidgetId("gallery"), places[i]);
            widget.Config = StateJson.Merged(widget.Config, $$"""{"path":"{{JsonEncodedText.Encode(pictures)}}","style":"{{styles[i]}}","slideshowSeconds":5}""");
            galleries.Widgets.Add(widget);
        }

        var feeds = ViewState.Create("Busy feeds");
        (WidgetId Kind, FracRect Rect)[] layout =
        [
            (new WidgetId("videoStream"), new FracRect(0.02, 0.04, 0.40, 0.40)),
            (new WidgetId("audioVisualizer"), new FracRect(0.44, 0.04, 0.30, 0.20)),
            (new WidgetId("topProcesses"), new FracRect(0.76, 0.04, 0.22, 0.60)),
            (new WidgetId("cpu"), new FracRect(0.44, 0.26, 0.14, 0.18)),
            (new WidgetId("memory"), new FracRect(0.60, 0.26, 0.14, 0.18)),
            (new WidgetId("network"), new FracRect(0.02, 0.48, 0.20, 0.18)),
            (new WidgetId("cpuSpeed"), new FracRect(0.24, 0.48, 0.24, 0.04)),
            (new WidgetId("screenTimeChart"), new FracRect(0.50, 0.48, 0.24, 0.30)),
            (new WidgetId("clock"), new FracRect(0.02, 0.70, 0.20, 0.14)),
        ];

        foreach (var (kind, rect) in layout)
        {
            var widget = WidgetInstance.Create(kind, rect);
            if (kind == new WidgetId("videoStream") && stream is not null)
            {
                widget.Config = StateJson.Merged(widget.Config, $$"""{"url":"{{JsonEncodedText.Encode(stream)}}"}""");
            }

            feeds.Widgets.Add(widget);
        }

        return (galleries, feeds);
    }

    /// <summary>[S-1885] An everyday desktop: eight widgets of the kinds people keep on it.</summary>
    private static ViewState OrdinaryView()
    {
        var view = ViewState.Create("Ordinary");
        (WidgetId Kind, FracRect Rect)[] layout =
        [
            (new WidgetId("clock"), new FracRect(0.02, 0.04, 0.18, 0.14)),
            (new WidgetId("cpu"), new FracRect(0.22, 0.04, 0.14, 0.16)),
            (new WidgetId("memory"), new FracRect(0.38, 0.04, 0.14, 0.16)),
            (new WidgetId("folder"), new FracRect(0.02, 0.22, 0.24, 0.40)),
            (new WidgetId("desktopItem"), new FracRect(0.28, 0.22, 0.18, 0.16)),
            (new WidgetId("notes"), new FracRect(0.48, 0.22, 0.22, 0.30)),
            (new WidgetId("music"), new FracRect(0.72, 0.04, 0.24, 0.18)),
            (new WidgetId("volume"), new FracRect(0.72, 0.24, 0.24, 0.16)),
        ];

        foreach (var (kind, rect) in layout)
        {
            view.Widgets.Add(WidgetInstance.Create(kind, rect));
        }

        return view;
    }
}
#endif
