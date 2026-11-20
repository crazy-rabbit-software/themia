#if DEBUG
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-11877] [S-11879] [S-12953] Debug only: <c>Themia.exe --desk-select-check &lt;out.txt&gt; [--locked] [--live] [--twice]
/// [--glass]</c>. The locked desktop's one selection through the app's own wiring: a real <see cref="AppHost"/> over a detached
/// store with its desk started as at the app's start, and a real <see cref="DesktopWindow"/> on a stand-in display whose view
/// holds cards of several sorts (a clock, a calendar, a list, a text widget, the weather, a fence), two own icons and two
/// desktop items of one name, the user's and the Public desktop's (a listing of them handed to the host; [S-5540]: with
/// <c>--twice</c> the user's is on the other display's view too, as the owner's Quartus on Home and Today).
/// Without <c>--live</c> the host's gestures are called as the presses call them (Ctrl+press on each card, a band over
/// everything; a click, Ctrl+click, Shift+click and a band on the two of one name). With <c>--live</c> the stand-in display is put on the primary screen over every window and driven with real
/// input (SendInput: the pointer, Ctrl, Shift, the arrows, Ctrl+A), so every press goes through XAML's routing as the user's:
/// Ctrl+click on every card and icon (first while another app has the foreground), the band, a click and Shift+click on icons,
/// Down, Ctrl+A, a click on one icon of a selection, and a drag of the selection by an icon. Before each press it checks the
/// point is its own window (else it stops) and puts the pointer back at the end; keys are sent only while its surface has the
/// foreground. Each step says what the selection holds against what Lock widgets wants (off: cards and icons alike; on: icons
/// only) and the last line is PASS or FAIL. <c>--twice</c> adds a second display left of it; <c>--glass</c> draws Liquid glass.
/// </summary>
internal static partial class DeskSelectCheck
{
    public const string Flag = "--desk-select-check";
    private const string Scope = "desk-select-check";
    private const string DisplayId = "desk-select-check-display";

    private const ushort VkShift = 0x10;
    private const ushort VkControl = 0x11;
    private const ushort VkDown = 0x28;
    private const ushort VkA = 0x41;

    public static bool TryRunFromCommandLine(Application app, string[] args)
    {
        var at = Array.IndexOf(args, Flag);
        if (at < 0)
        {
            return false;
        }

        if (at + 1 >= args.Length || DispatcherQueue.GetForCurrentThread() is null)
        {
            Log.Error(Scope, $"usage: {Flag} <out.txt> [--locked] [--live] [--twice] [--glass]");
            app.Exit();
            return true;
        }

        // [S-13970] The desktop's icon layout the icons (and the moves' ghosts, [S-13046]) are drawn in.
        var layoutAt = Array.IndexOf(args, "--icon-layout");
        var layout = DesktopIconLayout.Below;
        if (layoutAt >= 0 && (layoutAt + 1 >= args.Length || !Enum.TryParse(args[layoutAt + 1], ignoreCase: true, out layout) || !Enum.IsDefined(layout)))
        {
            Log.Error(Scope, $"usage: {Flag} <out.txt> [--icon-layout below|beside|icononly]");
            app.Exit();
            return true;
        }

        _ = RunAsync(app, Path.GetFullPath(args[at + 1]), args.Contains("--locked"), args.Contains("--live"), args.Contains("--twice"), args.Contains("--glass"), layout);
        return true;
    }

    private static async Task RunAsync(Application app, string outPath, bool locked, bool live, bool twice, bool glass, DesktopIconLayout layout)
    {
        var lines = new List<string>();
        var failures = 0;
        AppHost? host = null;
        DesktopWindow? surface = null;
        DesktopWindow? second = null;
        Win32.Point? cursor = null;

        void Say(string line)
        {
            Console.WriteLine(line);
            lines.Add(line);
        }

        Log.AddSink(static record => Console.WriteLine(LogFormat.Record(record)));
        try
        {
            var state = AppState.CreateDefault();
            state.Preferences.LockWidgets = locked;
            state.Preferences.DesktopIconLayout = layout;
            if (glass)
            {
                DesignPresets.Apply(state.Design, DesignPresetId.LiquidGlass);
            }

            var view = state.Views[0];
            view.Widgets.Clear();
            var items = new (string Name, WidgetInstance Widget)[]
            {
                ("clock", WidgetInstance.Create(new WidgetId("clock"), new FracRect(0.04, 0.05, 0.18, 0.14))),
                ("calendar", WidgetInstance.Create(new WidgetId("calendar"), new FracRect(0.04, 0.22, 0.24, 0.34))),
                ("tasks", WidgetInstance.Create(new WidgetId("tasks"), new FracRect(0.30, 0.05, 0.22, 0.30))),
                ("notes", WidgetInstance.Create(new WidgetId("notes"), new FracRect(0.30, 0.40, 0.22, 0.24))),
                ("weather", WidgetInstance.Create(new WidgetId("weatherNow"), new FracRect(0.55, 0.05, 0.18, 0.20))),
                ("fence", WidgetInstance.Create(new WidgetId("fence"), new FracRect(0.55, 0.32, 0.22, 0.30))),
                ("iconA", WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(0.86, 0.08, 0.07, 0.11))),
                ("iconB", WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(0.86, 0.30, 0.07, 0.11))),
                ("twinU", WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(0.86, 0.52, 0.07, 0.11))),
                ("twinP", WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(0.86, 0.66, 0.07, 0.11))),
            };

            // [S-5510] One file name on the user's desktop and on the Public one: two items Explorer shows side by side, two keys.
            const string Twin = "Twin (Check 1.0).lnk";
            var twins = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Twin),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), Twin),
            };
            ((IDesktopItemConfig)items[^2].Widget.Config).Item = twins[0];
            ((IDesktopItemConfig)items[^1].Widget.Config).Item = twins[1];
            view.Widgets.AddRange(items.Select(i => i.Widget));

            // [S-13046] The fence last in the view's paint order, as one made after its icons: an icon moved onto it stood under its body.
            var fenceWidget = items.First(i => i.Name == "fence").Widget;
            view.Widgets.Remove(fenceWidget);
            view.Widgets.Add(fenceWidget);
            state.ShowViewOn(DisplayId, view.Id);

            var bounds = live
                ? new Win32.Rect { Left = twice ? 1300 : 60, Top = 60, Right = (twice ? 1300 : 60) + 1200, Bottom = 60 + 800 }
                : new Win32.Rect { Left = -32000, Top = -32000, Right = -32000 + 1920, Bottom = -32000 + 1080 };
            var monitor = new MonitorTarget(DisplayId, IdResolved: true, "desk-select-check", bounds, bounds with { Bottom = bounds.Bottom - 48 }, IsPrimary: false);
            host = new AppHost(DispatcherQueue.GetForCurrentThread(), AppStateStore.Detached(Path.Combine(Path.GetTempPath(), "themia-desk-select-check", "state.json")), state);
            surface = new DesktopWindow(host, monitor);
            host.AdoptSurfaceForCheck(surface);
            string? homeTwin = null;
            if (twice)
            {
                var other = state.Views[1];
                other.Widgets.Clear();
                other.Widgets.Add(WidgetInstance.Create(new WidgetId("clock"), new FracRect(0.05, 0.05, 0.2, 0.15)));
                other.Widgets.Add(WidgetInstance.Create(new WidgetId("fence"), new FracRect(0.3, 0.2, 0.25, 0.3)));

                // [S-5540] The user's twin on this view too (the owner's Home beside Today): one key on two displays.
                var home = WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(0.7, 0.4, 0.07, 0.11));
                ((IDesktopItemConfig)home.Config).Item = twins[0];
                other.Widgets.Add(home);
                homeTwin = home.Id;
                state.ShowViewOn(DisplayId + "-2", other.Id);
                var left = live
                    ? new Win32.Rect { Left = 60, Top = 60, Right = 60 + 1200, Bottom = 60 + 800 }
                    : bounds with { Left = bounds.Left - 2000, Right = bounds.Right - 2000 };
                second = new DesktopWindow(host, new MonitorTarget(DisplayId + "-2", IdResolved: true, "desk-select-check-2", left, left with { Bottom = left.Bottom - 48 }, IsPrimary: false));
                host.AdoptSurfaceForCheck(second);
                second.ShowSurface();
            }

            // The desk follows the host as at the app's start: the icons' presses reach it, the listing and state relay it ([S-5540]).
            host.StartDeskForCheck(start: true);
            Services.Files.HostDesktopItems.Instance.AdoptForCheck([.. twins.Select(key => new DesktopItem(key, "Twin (Check 1.0)", DesktopItemKind.Shortcut))]);
            surface.ShowSurface();

            // A diagnostic's wait, not a correctness path: the surface lays out and the intro ends.
            await Task.Delay(2500).ConfigureAwait(true);

            var shown = surface;
            var running = host;
            var names = items.Select(it => it.Name).ToList();
            var icons = names.Where(n => n.StartsWith("icon", StringComparison.Ordinal) || n.StartsWith("twin", StringComparison.Ordinal)).ToList();
            var cards = names.Except(icons).ToList();
            WidgetInstance Model(int i) => running.State.Views[0].Find(items[i].Widget.Id) ?? items[i].Widget;
            int Index(string name) => names.IndexOf(name);

            // What Lock widgets lets the selection hold of what the gesture reaches ([S-12953]).
            // [S-13045] And a fence only while no icon is selected.
            List<string> Allowed(IEnumerable<string> reached)
            {
                List<string> allowed = [.. reached.Where(n => !locked || icons.Contains(n))];
                return allowed.Exists(icons.Contains) ? allowed.FindAll(n => n != "fence") : allowed;
            }

            void Expect(string step, IEnumerable<string>? wanted)
            {
                var picked = names.Where(n => running.DeskSelects(Model(Index(n)))).ToList();
                var drawn = names.Where(n => shown.CardFor(items[Index(n)].Widget.Id) is { } card && shown.IsSelected(card)).ToList();
                var ok = wanted is null || (picked.SequenceEqual(names.Where(wanted.Contains)) && drawn.SequenceEqual(picked));
                failures += ok ? 0 : 1;
                Say($"{(ok ? "ok  " : "FAIL")} {step}: selected [{string.Join(",", picked)}], drawn [{string.Join(",", drawn)}]"
                    + (ok || wanted is null ? string.Empty : $", wanted [{string.Join(",", names.Where(wanted.Contains))}]"));
            }

            Say($"desk-select-check: lock widgets {(locked ? "on" : "off")}, {(live ? "real input" : "the host's gestures")}, {view.Widgets.Count} widget(s), metrics {surface.Metrics.Width:0}x{surface.Metrics.Height:0}");
            var metrics = surface.Metrics;
            var start = new Point(metrics.Width * 0.02, metrics.Height * 0.02);
            var end = new Point(metrics.Width * 0.97, metrics.Height * 0.80);
            var empty = new Point(metrics.Width * 0.70, metrics.Height * 0.70);

            if (!live)
            {
                foreach (var name in cards)
                {
                    var picked = surface.CardFor(items[Index(name)].Widget.Id) is { } card && surface.DeskPick(card, ctrl: true, shift: false);
                    Say($"     ctrl+press {name}: picked {picked}");
                }

                Expect("after ctrl+press on every card", Allowed(cards));
                host.DesktopEmptyPressed(surface, start, ctrl: false, shift: false);
                Expect("after a press on empty desktop", []);
                host.DesktopBandBegin(ctrl: false, shift: false);
                host.DesktopBand(surface, start, end);
                Expect("band running", Allowed(names));
                host.DesktopBandEnd();
                Expect("band ended", Allowed(names));

                // [S-5510] [S-5540] The two items of one name, one per desktop folder: each its own key, selected together.
                // With --twice the user's twin is on the other display's view too (Home beside Today): a band and a range here
                // still take it where this display shows it.
                string KeyOf(string name) => DesktopItemKeys.Of((IDesktopItemConfig)Model(Index(name)).Config);
                void Click(string name, bool ctrl = false, bool shift = false)
                {
                    host.Press(KeyOf(name), ctrl, shift, items[Index(name)].Widget.Id);
                    host.Release(KeyOf(name));
                }

                Say($"     twins: keys distinct {!DesktopItemKeys.Comparer.Equals(KeyOf("twinU"), KeyOf("twinP"))}"
                    + (homeTwin is { } homeId ? $", twinU on the other display (left) too: drawn there {second?.CardFor(homeId) is not null && second.Draws(homeId)}" : string.Empty));
                host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                Say($"     twins' places in the desktop's order: twinU {host.PositionOf(KeyOf("twinU"))}, twinP {host.PositionOf(KeyOf("twinP"))}");
                Click("twinU");
                Expect("click twinU", ["twinU"]);
                Click("twinP", ctrl: true);
                Expect("ctrl+click twinP", ["twinU", "twinP"]);
                Click("twinP");
                Click("twinU", shift: true);
                Expect("click twinP, shift+click twinU", ["twinU", "twinP"]);
                host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                host.DesktopBandBegin(ctrl: false, shift: false);
                host.DesktopBand(surface, new Point(metrics.Width * 0.84, metrics.Height * 0.50), new Point(metrics.Width * 0.95, metrics.Height * 0.79));
                host.DesktopBandEnd();
                Expect("band over the twins", ["twinU", "twinP"]);

                // [S-13045] The icons before the fence, whatever the gesture.
                bool Pick(string name, bool ctrl = true, bool shift = false)
                    => surface.CardFor(items[Index(name)].Widget.Id) is { } picked && surface.DeskPick(picked, ctrl, shift);
                host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                Pick("fence");
                Expect("ctrl+press fence", Allowed(["fence"]));
                Click("iconA", ctrl: true);
                Expect("ctrl+click iconA", ["iconA"]);
                Pick("fence");
                Expect("ctrl+press fence with iconA", ["iconA"]);
                Pick("clock");
                Expect("ctrl+press clock with iconA", Allowed(["clock", "iconA"]));
                host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                Pick("fence");
                Click("iconB", shift: true);
                var ranged = names.Where(n => running.DeskSelects(Model(Index(n)))).ToList();
                var rangeOk = ranged.Contains("iconB") && !ranged.Contains("fence");
                failures += rangeOk ? 0 : 1;
                Say($"{(rangeOk ? "ok  " : "FAIL")} ctrl+press fence, shift+click iconB: selected [{string.Join(",", ranged)}] (the range, no fence)");
                host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                host.Key(WidgetKey.SelectAll, shift: false, items[Index("iconA")].Widget.Id);
                Expect("Ctrl+A", Allowed(names));

                // [S-13046] A desktop icon's move: Explorer's picture of it under the pointer, the whole icon where it lands as
                // its ghost, over every card (a fence included); whole again where it landed. Drawn mid-move.
                async Task MoveAsync(string name, Func<WidgetCard, Point> to, string tag, Func<bool> landed)
                {
                    host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                    var held = surface.CardFor(items[Index(name)].Widget.Id)!;
                    var layer = (UIElement)held.Parent;
                    var press = held.TransformToVisual(layer).TransformPoint(new Point(held.ActualWidth / 2, held.ActualHeight * 0.3));
                    var target = to(held);
                    var others = items.Where(i => i.Widget.Id != held.Model.Id).Select(i => surface.CardFor(i.Widget.Id)).OfType<WidgetCard>().ToList();
                    surface.BeginMove(held, press);
                    surface.PreviewMove(held, (target.X - press.X) / metrics.Width, (target.Y - press.Y) / metrics.Height);
                    surface.FlushMove();
                    await Task.Delay(500).ConfigureAwait(true);
                    var ghost = Math.Abs(held.Opacity - Themia.Core.Widgets.SelectionLook.Landing) < 0.001;
                    var lifted = others.TrueForAll(o => Microsoft.UI.Xaml.Controls.Canvas.GetZIndex(held) > Microsoft.UI.Xaml.Controls.Canvas.GetZIndex(o));
                    var picture = surface.ShowsDragPicture;
                    await RenderPng.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, $".move-{tag}.png")).ConfigureAwait(true);
                    surface.CommitMove(held);
                    await Task.Delay(400).ConfigureAwait(true);
                    var after = surface.CardFor(items[Index(name)].Widget.Id);
                    var whole = after is { Opacity: 1 } && !surface.ShowsDragPicture;
                    var ok = ghost && lifted && picture && whole && landed();
                    failures += ok ? 0 : 1;
                    Say($"{(ok ? "ok  " : "FAIL")} move {name} ({tag}): ghost where it lands {ghost}, over every card {lifted}, picture under the pointer {picture}, whole after {whole}, landed {landed()}");
                }

                var fenceCard = surface.CardFor(items[Index("fence")].Widget.Id)!;
                var twinKey = KeyOf("twinP");
                await MoveAsync("twinP", held => fenceCard.TransformToVisual((UIElement)held.Parent).TransformPoint(new Point(fenceCard.ActualWidth * 0.25, fenceCard.ActualHeight * 0.55)), "fence",
                    () => Themia.Core.Desktop.DesktopFences.Holding(running.State.Views[0], twinKey) is not null).ConfigureAwait(true);
                var before = Model(Index("iconA")).Rect;
                await MoveAsync("iconA", held => new Point(metrics.Width * 0.79, metrics.Height * 0.16), "free", () => Model(Index("iconA")).Rect != before).ConfigureAwait(true);
            }
            else
            {
                // Over every window for the run, out of the z-order controller's hands.
                DesktopZOrder.Forget(surface.Hwnd);
                _ = Win32.GetCursorPos(out var was);
                cursor = was;

                var scale = surface.BandScale;
                Win32.Point Screen(Point dip) => new() { X = bounds.Left + (int)Math.Round(dip.X * scale), Y = bounds.Top + (int)Math.Round(dip.Y * scale) };
                Point Centre(string name)
                {
                    var card = shown.CardFor(items[Index(name)].Widget.Id)!;
                    return card.TransformToVisual(shown.DebugRoot).TransformPoint(new Point(card.ActualWidth / 2, card.ActualHeight * 0.3));
                }

                bool Ours(Win32.Point at)
                {
                    // The surface's own layering puts it back under the windows when it loses the foreground: over them again.
                    _ = Win32.SetWindowPos(shown.Hwnd, Win32.HwndTopmost, 0, 0, 0, 0, Win32.SwpNoMove | Win32.SwpNoSize | Win32.SwpNoActivate);
                    var under = Win32.WindowFromPoint(at);
                    var root = under == 0 ? 0 : Win32.GetAncestor(under, Win32.GaRoot);
                    if (root != shown.Hwnd)
                    {
                        Say($"live: {at.X},{at.Y} is not this check's window ({root:X}); stopped");
                        return false;
                    }

                    return true;
                }

                async Task<bool> Press(Point from, Point to, ushort modifier, int steps)
                {
                    var a = Screen(from);
                    if (!Ours(a))
                    {
                        return false;
                    }

                    Move(a);
                    await Task.Delay(120).ConfigureAwait(true);
                    if (modifier != 0)
                    {
                        Key(modifier, up: false);
                    }

                    Button(up: false);
                    await Task.Delay(120).ConfigureAwait(true);
                    for (var step = 1; step <= steps; step++)
                    {
                        Move(Screen(new Point(from.X + ((to.X - from.X) * step / steps), from.Y + ((to.Y - from.Y) * step / steps))));
                        await Task.Delay(40).ConfigureAwait(true);
                    }

                    Button(up: true);
                    if (modifier != 0)
                    {
                        Key(modifier, up: true);
                    }

                    await Task.Delay(450).ConfigureAwait(true);
                    return true;
                }

                Task<bool> Click(Point at, ushort modifier = 0) => Press(at, at, modifier, 0);

                async Task<bool> Keys(ushort modifier, ushort key)
                {
                    if (Win32.GetForegroundWindow() != shown.Hwnd)
                    {
                        Say("live: the surface is not the foreground window; no key sent");
                        return false;
                    }

                    if (modifier != 0)
                    {
                        Key(modifier, up: false);
                    }

                    Key(key, up: false);
                    Key(key, up: true);
                    if (modifier != 0)
                    {
                        Key(modifier, up: true);
                    }

                    await Task.Delay(300).ConfigureAwait(true);
                    return true;
                }

                // [S-11879] Ctrl+click while another app has the foreground (the user was elsewhere).
                var foreground = Win32.GetForegroundWindow();
                var ok = await Click(Centre("clock"), VkControl).ConfigureAwait(true);
                Expect("ctrl+click clock, another app in front", Allowed(["clock"]));
                ok = ok && await Click(Centre("calendar"), VkControl).ConfigureAwait(true);
                Expect("ctrl+click calendar", Allowed(["clock", "calendar"]));
                ok = ok && await Click(Centre("iconA"), VkControl).ConfigureAwait(true);
                Expect("ctrl+click iconA", Allowed(["clock", "calendar", "iconA"]));
                ok = ok && await Click(empty).ConfigureAwait(true);
                Expect("click empty", []);

                // [S-5544] The band from another app.
                if (ok && foreground != 0 && foreground != shown.Hwnd && Win32.SetForegroundWindow(foreground))
                {
                    await Task.Delay(300).ConfigureAwait(true);
                    ok = await Press(start, end, 0, 10).ConfigureAwait(true);
                    Expect("band over everything, another app in front", Allowed(names));
                    ok = ok && await Click(empty).ConfigureAwait(true);
                }

                // [S-11879] [S-5540] Ctrl+click on every card and icon in turn.
                var reached = new List<string>();
                foreach (var name in names)
                {
                    reached.Add(name);
                    ok = ok && await Click(Centre(name), VkControl).ConfigureAwait(true);
                    Expect($"ctrl+click {name}", Allowed(reached));
                }

                ok = ok && await Click(empty).ConfigureAwait(true);
                Expect("click empty", []);
                ok = ok && await Press(start, end, 0, 10).ConfigureAwait(true);
                Expect("band over everything", Allowed(names));

                // [S-5540] [S-6620] Icons by a click, Shift+click, the arrows; a card's Shift+click (the range) while unlocked.
                ok = ok && await Click(empty).ConfigureAwait(true);
                ok = ok && await Click(Centre("iconA")).ConfigureAwait(true);
                Expect("click iconA", ["iconA"]);
                ok = ok && await Click(Centre("iconB"), VkShift).ConfigureAwait(true);
                Expect("shift+click iconB", ["iconA", "iconB"]);
                ok = ok && await Click(Centre("clock"), VkShift).ConfigureAwait(true);
                Expect("shift+click clock", locked ? ["iconA", "iconB"] : null);
                ok = ok && await Click(Centre("iconA")).ConfigureAwait(true);
                ok = ok && await Keys(0, VkDown).ConfigureAwait(true);
                Expect("click iconA, Down", ["iconB"]);

                // [S-6623] Ctrl+A: everything the selection may hold.
                ok = ok && await Click(empty).ConfigureAwait(true);
                ok = ok && await Keys(VkControl, VkA).ConfigureAwait(true);
                Expect("click empty, Ctrl+A", Allowed(names));

                // [S-6621] A click on one icon of a selection narrows to it on the release.
                ok = ok && await Click(Centre("iconA")).ConfigureAwait(true);
                Expect("click iconA in the selection", ["iconA"]);

                // [S-11879] [S-12950] The whole selection dragged by an icon: what moves.
                ok = ok && await Click(empty).ConfigureAwait(true);
                ok = ok && await Keys(VkControl, VkA).ConfigureAwait(true);
                var before = names.ToDictionary(n => n, n => Model(Index(n)).Rect);
                var held = Centre("iconA");
                ok = ok && await Press(held, new Point(held.X - (metrics.Width * 0.04), held.Y + (metrics.Height * 0.12)), 0, 12).ConfigureAwait(true);
                var moved = names.Where(n => Model(Index(n)).Rect != before[n]).ToList();
                var movedOk = moved.SequenceEqual(Allowed(names));
                failures += movedOk ? 0 : 1;
                Say($"{(movedOk ? "ok  " : "FAIL")} drag the selection by iconA: moved [{string.Join(",", moved)}]");
                ok = ok && await Click(empty).ConfigureAwait(true);
                Expect("click empty", []);
                if (!ok)
                {
                    failures++;
                    Say("FAIL live: stopped early");
                }
            }

            await RenderPng.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, ".png")).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the desk select check failed", ex);
            failures++;
            Say($"desk-select-check: failed: {ex.Message}");
        }
        finally
        {
            if (cursor is { } back)
            {
                Key(VkControl, up: true);
                Key(VkShift, up: true);
                _ = SetCursorPos(back.X, back.Y);
            }

            host?.StartDeskForCheck(start: false);
            second?.CloseSurface();
            surface?.CloseSurface();
            host?.Dispose();
            Say(failures == 0 ? "desk-select-check: PASS" : $"desk-select-check: FAIL ({failures})");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            await File.WriteAllLinesAsync(outPath, lines).ConfigureAwait(true);
            app.Exit();
        }
    }

    // ---- real input, --live only ----------------------------------------------------------------

    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public int Dx;
        [FieldOffset(12)] public int Dy;
        [FieldOffset(16)] public uint MouseData;
        [FieldOffset(20)] public uint MouseFlags;
        [FieldOffset(8)] public ushort Vk;
        [FieldOffset(10)] public ushort Scan;
        [FieldOffset(12)] public uint KeyFlags;
    }

    private static void Move(Win32.Point at)
    {
        // Absolute over the virtual desk: 0..65535 across it.
        var (left, top, width, height) = (GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79));
        var input = new Input
        {
            Type = InputMouse,
            Dx = (int)Math.Round((at.X - left) * 65535.0 / (width - 1)),
            Dy = (int)Math.Round((at.Y - top) * 65535.0 / (height - 1)),
            MouseFlags = 0x0001 | 0x8000 | 0x4000, // MOVE | ABSOLUTE | VIRTUALDESK
        };
        _ = SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private static void Button(bool up)
    {
        var input = new Input { Type = InputMouse, MouseFlags = up ? 0x0004u : 0x0002u };
        _ = SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private static void Key(ushort vk, bool up)
    {
        var input = new Input { Type = InputKeyboard, Vk = vk, KeyFlags = up ? 0x0002u : 0u };
        _ = SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    [LibraryImport("user32.dll")]
    private static partial uint SendInput(uint count, [In] Input[] inputs, int size);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);
}
#endif
