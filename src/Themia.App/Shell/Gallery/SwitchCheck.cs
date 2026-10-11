#if DEBUG
using System.Diagnostics;
using Microsoft.UI.Dispatching;
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
using Themia.Core.Storage;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1883] Debug only: <c>Themia.exe --switch-check &lt;rounds&gt; [&lt;out.txt&gt;] [--ordinary | --show &lt;view&gt;] [--per-view] [--dwell ms]</c>
/// measures what switching views costs and whether it leaks.
/// <para>
/// A real <see cref="AppHost"/> over a detached state store (nothing is saved; the user's state,
/// accounts and caches are never read) and a real <see cref="DesktopWindow"/> on a stand-in display
/// parked off-screen, so the switch runs the surface's own path: <see cref="AppHost.ShowView"/>, the
/// relayout, card detach and the intro. The state holds the showcase views ([S-1810]) and one empty
/// view. Each round shows every view for <c>--dwell</c> ms, then the empty one, forces a full
/// collection and prints working set, private bytes, the managed heap, the cards still alive
/// ([S-1884]), the views attached against the live widgets ([S-1880]), the ticker's subscriptions,
/// threads and handles; a breach of the rule is marked LEAK. After round 1 the figures must stay
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
    private const string Flag = "--switch-check";
    private const string DisplayId = "switch-check-display";

    /// <summary>The stand-in display, in physical pixels, far outside every real one.</summary>
    private const int Width = 1920;
    private const int Height = 1080;
    private const int Left = -32000;
    private const int Top = -32000;

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || !int.TryParse(args[at + 1], out var rounds) || rounds < 1
            || DispatcherQueue.GetForCurrentThread() is not { } dispatcher)
        {
            Log.Error(Scope, "usage: --switch-check <rounds> [<out.txt>] [--ordinary | --show <view>] [--per-view] [--dwell ms]");
            app.Exit();
            return true;
        }

        var outPath = at + 2 < args.Length && !args[at + 2].StartsWith("--", StringComparison.Ordinal)
            ? Path.GetFullPath(args[at + 2])
            : null;
        var ordinary = args.Contains("--ordinary");
        var dwellAt = Array.IndexOf(args, "--dwell");
        var dwell = dwellAt >= 0 && dwellAt + 1 < args.Length && int.TryParse(args[dwellAt + 1], out var d) && d >= 0 ? d : 250;

        var showAt = Array.IndexOf(args, "--show");
        var show = showAt >= 0 && showAt + 1 < args.Length ? args[showAt + 1] : null;

        _ = RunAsync(app, dispatcher, rounds, outPath, ordinary, dwell, args.Contains("--per-view"), show);
        return true;
    }

    private static async Task RunAsync(Application app, DispatcherQueue dispatcher, int rounds, string? outPath, bool ordinary, int dwell, bool byView, string? show)
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
            var statePath = Path.Combine(Path.GetTempPath(), "themia-switch-check", "state.json");
            var monitor = StandIn();
            var screen = new ScreenMetrics(Width, Height) { Allowed = GridGeometry.AllowedArea(monitor.Info.Bounds, monitor.Info.WorkArea) };

            var empty = ViewState.Create("Empty");
            var state = new AppState { Views = [empty], ActiveViewId = empty.Id };

            if (ordinary)
            {
                state.Views.Insert(0, OrdinaryView());
            }
            else
            {
                var kinds = WidgetCatalog.All.Select(k => new ShowcaseKind(k.Kind, k.Label, k.Glyph)).ToList();
                ShowcaseViews.Replace(state, ShowcaseViews.Build(kinds, screen, state.Design.WidgetGapPx));
            }

            state.ShowViewOn(DisplayId, empty.Id);

            host = new AppHost(dispatcher, AppStateStore.Detached(statePath), state);
            surface = new DesktopWindow(host, monitor);
            surface.ShowSurface();
            await Task.Delay(1000).ConfigureAwait(true);

            Say($"switch-check: {state.Views.Count} views, {state.Views.Sum(v => v.Widgets.Count)} widgets, dwell {dwell} ms");
            Say(Measure("start (empty view)", host));

            if (ordinary || show is not null)
            {
                var view = ordinary ? state.Views[0] : state.Views.First(v => string.Equals(v.Name, show, StringComparison.OrdinalIgnoreCase));
                host.ShowView(DisplayId, view.Id);
                await Task.Delay(Math.Max(dwell, 3000)).ConfigureAwait(true);
                Say(Measure($"'{view.Name}' shown ({view.Widgets.Count} widgets)", host, view));
                if (outPath is not null)
                {
                    await RenderPng.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, ".png")).ConfigureAwait(true);

                    // Every list scrolled to its end: what a scroll builds ([S-1887]) is drawn too.
                    var scrolled = 0;
                    foreach (var scroller in Descendants(surface.DebugRoot).OfType<ScrollViewer>())
                    {
                        scrolled += scroller.ChangeView(null, scroller.ScrollableHeight, null, disableAnimation: true) ? 1 : 0;
                    }

                    await Task.Delay(1500).ConfigureAwait(true);
                    await RenderPng.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, ".scrolled.png")).ConfigureAwait(true);
                    Say(Measure($"'{view.Name}' {scrolled} list(s) scrolled to the end", host, view));
                }

                await Task.Delay(5000).ConfigureAwait(true);
                Say(Measure($"'{view.Name}' after 5 s more", host, view));
                host.ShowView(DisplayId, empty.Id);
                await Task.Delay(1000).ConfigureAwait(true);
                Say(Measure("empty view again", host));

                // More rounds: this one view and the empty one, in turn.
                for (var round = 2; round <= rounds; round++)
                {
                    host.ShowView(DisplayId, view.Id);
                    await Task.Delay(dwell).ConfigureAwait(true);
                    host.ShowView(DisplayId, empty.Id);
                    await Task.Delay(500).ConfigureAwait(true);
                    Say(Measure($"round {round}", host));
                }

                return;
            }

            // --per-view: what each view costs while shown, over the empty view, averaged over rounds 2..n.
            var perView = new Dictionary<string, double>(StringComparer.Ordinal);
            var before = PrivateMb();

            for (var round = 1; round <= rounds; round++)
            {
                foreach (var view in state.Views.ToList())
                {
                    host.ShowView(DisplayId, view.Id);
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
                Say(Measure($"round {round}", host));
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
            Log.Error(Scope, "the switch check failed", ex);
            Say($"switch-check: failed: {ex.Message}");
        }
        finally
        {
            surface?.CloseSurface();
            host?.Dispose();

            if (outPath is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                await File.WriteAllLinesAsync(outPath, lines).ConfigureAwait(true);
            }

            app.Exit();
        }
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
    /// alive. A breach is printed as LEAK.
    /// </summary>
    private static string Measure(string label, AppHost host, ViewState? shown = null)
    {
        Collect();

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var mb = 1024.0 * 1024.0;
        var expected = shown?.Widgets.Count ?? 0;
        var live = WidgetLiveness.Live(host.State, [DisplayId], AccountFeatures.None).Count;
        var cards = LiveCensus.LiveCards();

        // [S-2511] The system hub's one subscription is its lingering feeds' ([S-2505]), not a widget's.
        var system = AppServices.System;
        var hubTicks = system is { HoldsTicker: true } ? 1 : 0;
        var verdict = cards == expected && LiveCensus.Attached == live && host.Ticker.SubscriptionCount - hubTicks <= live ? string.Empty : "  LEAK";

        return $"{label,-40} ws {process.WorkingSet64 / mb,7:0.0} MB  private {process.PrivateMemorySize64 / mb,7:0.0} MB  " +
            $"managed {GC.GetTotalMemory(forceFullCollection: false) / mb,6:0.0} MB  cards alive {cards,4} (shown {expected})  " +
            $"views attached {LiveCensus.Attached,4} (live {live})  ticks {host.Ticker.SubscriptionCount,3}  " +
            $"system feeds {system?.FeedCount ?? 0,3} (lingering {system?.LingeringCount ?? 0}, sources {system?.SourceCount ?? 0})  " +
            $"threads {process.Threads.Count,3}  handles {process.HandleCount,5}{verdict}";
    }

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

    /// <summary>One display nobody can see: no real monitor is anywhere near (-32000, -32000).</summary>
    private static MonitorTarget StandIn()
    {
        var bounds = new Win32.Rect { Left = Left, Top = Top, Right = Left + Width, Bottom = Top + Height };
        var work = bounds with { Bottom = bounds.Bottom - 48 };
        return new MonitorTarget(DisplayId, IdResolved: true, "switch-check", bounds, work, IsPrimary: false);
    }

    /// <summary>[S-1885] An everyday desktop: eight widgets of the kinds people keep on it.</summary>
    private static ViewState OrdinaryView()
    {
        var view = ViewState.Create("Ordinary");
        (WidgetKind Kind, FracRect Rect)[] layout =
        [
            (WidgetKind.Clock, new FracRect(0.02, 0.04, 0.18, 0.14)),
            (WidgetKind.Cpu, new FracRect(0.22, 0.04, 0.14, 0.16)),
            (WidgetKind.Memory, new FracRect(0.38, 0.04, 0.14, 0.16)),
            (WidgetKind.Folder, new FracRect(0.02, 0.22, 0.24, 0.40)),
            (WidgetKind.Shortcuts, new FracRect(0.28, 0.22, 0.18, 0.16)),
            (WidgetKind.Notes, new FracRect(0.48, 0.22, 0.22, 0.30)),
            (WidgetKind.Music, new FracRect(0.72, 0.04, 0.24, 0.18)),
            (WidgetKind.Volume, new FracRect(0.72, 0.24, 0.24, 0.16)),
        ];

        foreach (var (kind, rect) in layout)
        {
            view.Widgets.Add(WidgetInstance.Create(kind, rect));
        }

        return view;
    }
}
#endif
