#if DEBUG
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-11877] [S-11879] [S-12953] Debug only: <c>Themia.exe --check desk-select &lt;out.txt&gt; [--locked] [--live] [--twice]
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
/// only) and the last line is PASS or FAIL. [S-14310] Without <c>--live</c> it also checks the marks of several cards selected:
/// the selection's frame's four corners alone round them, none of their own, a card alone its own, through a locked move too.
/// [S-13046]..[S-13048] It moves icons one and several on the free desktop, into the fence and out of it, drawn mid-move: each
/// moving icon a ghost where it lands and [S-16460] no picture under the pointer while the move stays on the desktop; the
/// shell's drag a move becomes carries the held icon alone (one icon's square, no badge).
/// <c>--twice</c> adds a second display left of it; <c>--glass</c> draws Liquid glass.
/// </summary>
internal static partial class DeskSelectCheck
{
    private const string Scope = "desk-select-check";
    private const string DisplayId = "desk-select-check-display";

    private const ushort VkShift = 0x10;
    private const ushort VkControl = 0x11;
    private const ushort VkDown = 0x28;
    private const ushort VkA = 0x41;

    public static RenderCommand Command { get; } = new(
        RenderVerb.Check,
        "desk-select",
        "<out.txt> [--locked] [--twice] [--glass] [--icon-layout below|beside|icononly] [--live]",
        """
        the locked desktop's one selection through a real host and surface, PASS or FAIL [S-12954]: the host's gestures
        (Ctrl+press on every card, a band, a click, Ctrl+click and Shift+click on two desktop items of one name [S-5510]),
        the several cards' frame [S-14310], Delete outside edit mode [S-14320], a card's menu [S-14321]; an icon moved into the
        fence and one on the free desktop, drawn mid-move to <out>.move-fence.png / .move-fence-icon.png / .move-free.png
        [S-13046] [S-13047], two icons moved on the free desktop, out of the fence and into it, nothing under the pointer
        (<out>.move-several-free.png / .move-several-out.png / .move-several-fence.png) [S-13048] [S-16460], an icon placed from the Unplaced page
        (<out>.place-icon.png) [S-13708], icons moved onto the open Unplaced icons page and dragged back off it
        (<out>.unplace-fence.png / .unplace-free.png) [S-13713] [S-13715]; <out>.png the surface at the end
        --locked     Lock widgets on: the selection holds icons only [S-12953]
        --twice      a second display left of it, the user's twin on its view too [S-5540]
        --glass      Liquid glass
        --icon-layout below|beside|icononly   the desktop's icon layout the icons and ghosts are drawn in [S-13970]
        --live       real input instead (SendInput: the pointer, Ctrl, Shift, the arrows, Ctrl+A), the stand-in display on the
                     primary screen over every window: it moves the user's real mouse [S-11879]
        """,
        RunAsync);

    private static async Task<int> RunAsync(RenderRun run)
    {
        var outPath = run.PathArg(0);

        // [S-13970] The desktop's icon layout the icons (and the moves' ghosts, [S-13046]) are drawn in.
        var layout = DesktopIconLayout.Below;
        if (run.Option("--icon-layout") is { } asked && (!Enum.TryParse(asked, ignoreCase: true, out layout) || !Enum.IsDefined(layout)))
        {
            throw run.Usage($"--icon-layout {asked}: below, beside or icononly");
        }

        var (locked, live, twice, glass) = (run.Has("--locked"), run.Has("--live"), run.Has("--twice"), run.Has("--glass"));
        var report = new CheckReport(Scope, outPath);
        void Say(string line) => report.Say(line);
        StandInDesktop? desk = null;
        Win32.Point? cursor = null;

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
                : StandInDesktop.OffScreen(1920, 1080);
            desk = StandInDesktop.Create(Scope, state);
            var host = desk.Host;
            var surface = desk.Add(StandInDesktop.Display(DisplayId, bounds));
            StandInDesktop stand = desk;
            DesktopWindow? second = null;
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
                    : StandInDesktop.OffScreen(1920, 1080, left: -2000);
                second = desk.Add(StandInDesktop.Display(DisplayId + "-2", left));
                desk.Show(second);
            }

            // [S-14320] The host's gestures never take the foreground from the user's window (the picks now give the desktop the
            // keys as a press on empty desktop does): the surfaces only count the times they would have, their button the check's.
            if (!live)
            {
                desk.UseCheckButton(static () => false);
            }

            // The desk follows the host as at the app's start: the icons' presses reach it, the listing and state relay it ([S-5540]).
            host.StartDeskForCheck(start: true);
            Services.Files.HostDesktopItems.Instance.AdoptForCheck([.. twins.Select(key => new DesktopItem(key, "Twin (Check 1.0)", DesktopItemKind.Shortcut))]);
            desk.Show(surface);

            // The surface laid out, its intro ended and its icons drawn ([S-15902]).
            await stand.ReadyAsync("the surface").ConfigureAwait(true);

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
                report.Expect(ok, $"{step}: selected [{string.Join(",", picked)}], drawn [{string.Join(",", drawn)}]"
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

                // [S-5540] A key two displays show finds its widget where the user pressed it: F2 (the keys, no widget named)
                // opens the rename box on that display's icon.
                if (homeTwin is { } homeOf)
                {
                    string? RenamesAfterPressOn(string pressed)
                    {
                        host.Press(KeyOf("twinU"), ctrl: false, shift: false, pressed);
                        host.Release(KeyOf("twinU"));

                        // A press on a card ends the empty desktop's hold on the keys, as the real press does ([S-5550]).
                        host.DesktopKeysEnd(giveBack: false, keepActive: true);
                        var began = host.Key(WidgetKey.Rename, shift: false, widgetId: null);
                        var on = host.RenamingWidget;
                        host.CancelRename();
                        return began ? on : null;
                    }

                    var here = items[Index("twinU")].Widget.Id;
                    var onHome = RenamesAfterPressOn(homeOf);
                    var onHere = RenamesAfterPressOn(here);
                    var renameOk = onHome == homeOf && onHere == here;
                    report.Expect(renameOk, $"F2 on twinU after a press on the other display's copy: box on {(onHome == homeOf ? "that copy" : onHome ?? "none")}; after a press on this one's: {(onHere == here ? "this copy" : onHere ?? "none")}");
                }

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
                report.Expect(rangeOk, $"ctrl+press fence, shift+click iconB: selected [{string.Join(",", ranged)}] (the range, no fence)");
                host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                host.Key(WidgetKey.SelectAll, shift: false, items[Index("iconA")].Widget.Id);
                Expect("Ctrl+A", Allowed(names));

                // [S-14310] Several cards selected: the selection's frame's four corners alone round them (no hairline, no grip)
                // and none of their own; one card alone, icons beside it or not: its own corners and no frame. The count crosses
                // 1 and 2 both ways. A locked move of several carries the frame's corners with the block.
                DipRect CellOf(WidgetCard card)
                    => new(Microsoft.UI.Xaml.Controls.Canvas.GetLeft(card), Microsoft.UI.Xaml.Controls.Canvas.GetTop(card), card.Width, card.Height);
                bool Round(IReadOnlyList<WidgetCard> members)
                {
                    var box = shown.DebugFrame.Box;
                    var cells = members.Select(CellOf).ToList();
                    return Math.Abs(box.X - cells.Min(c => c.X)) < 1 && Math.Abs(box.Y - cells.Min(c => c.Y)) < 1
                        && Math.Abs(box.X + box.Width - cells.Max(c => c.X + c.Width)) < 1 && Math.Abs(box.Y + box.Height - cells.Max(c => c.Y + c.Height)) < 1;
                }

                void ExpectFrame(string step, IEnumerable<string> selectedNow)
                {
                    var allowed = Allowed(selectedNow);
                    var wantFramed = allowed.Where(cards.Contains).ToList();
                    wantFramed = wantFramed.Count >= SelectionRules.FrameMinimum ? wantFramed : [];
                    var wantMarked = names.Where(n => cards.Contains(n) && allowed.Contains(n) && !wantFramed.Contains(n)).ToList();
                    var framed = names.Where(n => shown.CardFor(items[Index(n)].Widget.Id) is { } card && shown.FramedWith(card) > 0).ToList();
                    var marked = names.Where(n => shown.CardFor(items[Index(n)].Widget.Id) is { MarksDrawn: true }).ToList();
                    var frame = shown.DebugFrame;
                    var members = framed.Select(n => shown.CardFor(items[Index(n)].Widget.Id)!).ToList();
                    var drawnRight = wantFramed.Count == 0 ? !frame.Shown : frame.Shown && frame.CornersOnly && Round(members);
                    var ok = framed.SequenceEqual(names.Where(wantFramed.Contains)) && marked.SequenceEqual(wantMarked) && drawnRight;
                    report.Expect(ok, $"{step}: framed [{string.Join(",", framed)}], own marks [{string.Join(",", marked)}], frame {(frame.Shown ? frame.CornersOnly ? "corners" : "edit look" : "none")}"
                        + (ok ? string.Empty : $", wanted framed [{string.Join(",", wantFramed)}] own marks [{string.Join(",", wantMarked)}]"));
                }

                ExpectFrame("Ctrl+A: the cards framed", Allowed(names));
                host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                Pick("clock");
                ExpectFrame("ctrl+press clock", ["clock"]);
                Pick("tasks");
                ExpectFrame("ctrl+press tasks with clock", ["clock", "tasks"]);
                Click("iconA", ctrl: true);
                ExpectFrame("ctrl+click iconA with clock and tasks", ["clock", "tasks", "iconA"]);
                if (!locked)
                {
                    var clockCard = surface.CardFor(items[Index("clock")].Widget.Id)!;
                    var tasksCard = surface.CardFor(items[Index("tasks")].Widget.Id)!;
                    surface.BeginMove(clockCard, clockCard.TransformToVisual((UIElement)clockCard.Parent).TransformPoint(new Point(clockCard.ActualWidth / 2, clockCard.ActualHeight / 2)));
                    surface.PreviewMove(clockCard, 0.03, 0.02);
                    surface.FlushMove();
                    var moveOk = !clockCard.MarksDrawn && !tasksCard.MarksDrawn && shown.DebugFrame is { Shown: true, CornersOnly: true } && Round([clockCard, tasksCard]);
                    report.Expect(moveOk, $"a locked move of clock and tasks: own marks clock {clockCard.MarksDrawn}, tasks {tasksCard.MarksDrawn}; frame's corners round them where they land {shown.DebugFrame.Shown && Round([clockCard, tasksCard])}");
                    surface.CancelMove(clockCard);
                    ExpectFrame("the move called off", ["clock", "tasks", "iconA"]);
                }

                Pick("tasks");
                ExpectFrame("ctrl+press tasks again (out)", ["clock", "iconA"]);
                host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                ExpectFrame("a press on empty desktop", []);

                // [S-13046] A desktop icon's move: the whole icon where it lands as its ghost, over every card (a fence included);
                // whole again where it landed. Drawn mid-move. [S-16460] Nothing else under the pointer (owner, 2026-11-23: "when
                // moving a folder on the desktop under the mouse there is an additional preview of the folder or item or items that
                // are moved. this should not be."). [S-13047] Over a fence and the icons it holds it stays the move (never handed to
                // the shell's drag, whose picture is another): no picture under the pointer all the way.
                async Task MoveAsync(string name, Func<WidgetCard, Point> to, string tag, Func<bool> landed, Func<WidgetCard, Point>? via = null)
                {
                    host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                    var held = surface.CardFor(items[Index(name)].Widget.Id)!;
                    var layer = (UIElement)held.Parent;
                    var press = held.TransformToVisual(layer).TransformPoint(new Point(held.ActualWidth / 2, held.ActualHeight * 0.3));
                    var target = to(held);
                    var others = items.Where(i => i.Widget.Id != held.Model.Id).Select(i => surface.CardFor(i.Widget.Id)).OfType<WidgetCard>().ToList();
                    bool Handed(Point at) => surface.DropTargetUnder(layer.TransformToVisual(surface.DebugRoot).TransformPoint(at), new HashSet<WidgetCard> { held });
                    surface.BeginMove(held, press);

                    // A stop on the way (a fence's title bar): no picture there either.
                    var handed = false;
                    var onTheWay = false;
                    if (via?.Invoke(held) is { } stop)
                    {
                        surface.PreviewMove(held, (stop.X - press.X) / metrics.Width, (stop.Y - press.Y) / metrics.Height);
                        surface.FlushMove();
                        onTheWay = surface.ShowsDragPicture;
                        handed = Handed(stop);
                    }

                    surface.PreviewMove(held, (target.X - press.X) / metrics.Width, (target.Y - press.Y) / metrics.Height);
                    surface.FlushMove();
                    await stand.ReadyAsync($"{name} held over its target").ConfigureAwait(true);
                    var ghost = Math.Abs(held.Opacity - Themia.Core.Widgets.SelectionLook.Landing) < 0.001;
                    var lifted = others.TrueForAll(o => Microsoft.UI.Xaml.Controls.Canvas.GetZIndex(held) > Microsoft.UI.Xaml.Controls.Canvas.GetZIndex(o));
                    var picture = surface.ShowsDragPicture || onTheWay;
                    handed |= Handed(target);
                    await RenderHarness.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, $".move-{tag}.png"), Scope).ConfigureAwait(true);
                    surface.CommitMove(held);
                    await stand.ReadyAsync($"{name} dropped").ConfigureAwait(true);
                    var after = surface.CardFor(items[Index(name)].Widget.Id);
                    var whole = after is { Opacity: 1 } && !surface.ShowsDragPicture;
                    var ok = ghost && lifted && !picture && !handed && whole && landed();
                    report.Expect(ok, $"move {name} ({tag}): ghost where it lands {ghost}, over every card {lifted}, a picture under the pointer {picture}"
                        + (via is null ? string.Empty : " (all the way)")
                        + $", handed to the shell's drag {handed}, whole after {whole}, landed {landed()}");
                }

                // [S-11878] A free desktop item takes a move over it as a drop target (the shell's drag onto a folder or an app):
                // what keeps the move over a fenced one is the fence ([S-13047]), not an icon that takes nothing.
                WidgetCard Card(string name) => surface.CardFor(items[Index(name)].Widget.Id)!;
                Point CentreOf(WidgetCard card) => card.TransformToVisual(surface.DebugRoot).TransformPoint(new Point(card.ActualWidth / 2, card.ActualHeight * 0.3));
                var freeTakes = surface.DropTargetUnder(CentreOf(Card("twinU")), new HashSet<WidgetCard> { Card("iconA") });
                report.Expect(freeTakes, $"a move over the free twinU is handed to the shell's drag: {freeTakes}");

                var fenceCard = surface.CardFor(items[Index("fence")].Widget.Id)!;
                var twinKey = KeyOf("twinP");
                await MoveAsync("twinP", held => fenceCard.TransformToVisual((UIElement)held.Parent).TransformPoint(new Point(fenceCard.ActualWidth * 0.25, fenceCard.ActualHeight * 0.55)), "fence",
                    () => Themia.Core.Desktop.DesktopFences.Holding(running.State.Views[0], twinKey) is not null).ConfigureAwait(true);

                // [S-13047] The regression: twinU moved onto twinP in the fence (an icon there that takes drops) was handed to the
                // shell's drag halfway in, its picture swapped; now it lands on twinP's cell by the move, the picture the same.
                var fenced = Card("twinP");
                var twinUKey = KeyOf("twinU");
                await MoveAsync("twinU", held => fenced.TransformToVisual((UIElement)held.Parent).TransformPoint(new Point(fenced.ActualWidth * 0.8, fenced.ActualHeight * 0.55)), "fence-icon",
                    () => Themia.Core.Desktop.DesktopFences.Holding(running.State.Views[0], twinUKey) is not null,
                    via: held => fenceCard.TransformToVisual((UIElement)held.Parent).TransformPoint(new Point(fenceCard.ActualWidth * 0.5, 12))).ConfigureAwait(true);
                var before = Model(Index("iconA")).Rect;
                await MoveAsync("iconA", held => new Point(metrics.Width * 0.79, metrics.Height * 0.16), "free", () => Model(Index("iconA")).Rect != before).ConfigureAwait(true);

                // [S-13048] A move of several desktop icons (the owner, 2026-11-20: "when i move around multiple desktop icons there
                // is an additional preview with the count showing over the mouse. this should not be. remove that."): where the block
                // lands every moving icon whole as its ghost, [S-16460] nothing under the pointer. On the free desktop, out of the
                // fence and into it; drawn mid-move.
                // Where the press grabbed the held icon, taken before the move starts: its icon's square and the press, the root's DIPs.
                (Rect Square, Point At) Grab(WidgetCard held, Point press)
                {
                    var box = ((Themia.Sdk.WinUI.IDesktopIconView)held.View).DragIconBox;
                    return (box.TransformToVisual(surface.DebugRoot).TransformBounds(new Rect(0, 0, box.ActualWidth, box.ActualHeight)),
                        ((UIElement)held.Parent).TransformToVisual(surface.DebugRoot).TransformPoint(press));
                }

                // The picture is the held icon alone: one icon's square at the display's scale (no stack behind it, no badge hanging
                // out of its corner), held where the icon was grabbed, nothing in it opaque (the icon is see-through; a badge is not).
                (bool Alone, string Said) HeldAlone(Themia.Core.Files.DragPicture? picture, (Rect Square, Point At) grab)
                {
                    if (picture is null)
                    {
                        return (false, "no picture");
                    }

                    var opaque = 0;
                    for (var i = 3; i < picture.Bgra.Length; i += 4)
                    {
                        opaque = Math.Max(opaque, picture.Bgra[i]);
                    }

                    var (square, at) = grab;
                    var side = square.Width * picture.PixelsPerDip;
                    var oneSquare = Math.Abs(picture.Width - side) <= 1 && Math.Abs(picture.Height - side) <= 1;
                    var (hotX, hotY) = picture.Anchor;
                    var grabbed = Math.Abs(hotX - Math.Clamp(at.X - square.X, 0, square.Width)) <= 1 && Math.Abs(hotY - Math.Clamp(at.Y - square.Y, 0, square.Width)) <= 1;
                    var noBadge = opaque <= Math.Ceiling(255 * SelectionLook.DragOpacity) + 1;
                    return (oneSquare && grabbed && noBadge,
                        $"{picture.Width}x{picture.Height} px, one icon's square of {side:0} px {oneSquare}, held where grabbed {grabbed}, most opaque pixel {opaque} so no badge {noBadge}");
                }

                async Task MoveSeveralAsync(string name, string[] along, Func<WidgetCard, Point> to, string tag, Func<bool> landed, Func<WidgetCard, Point>? via = null, string? viaTag = null)
                {
                    host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                    Click(name);
                    foreach (var other in along)
                    {
                        Click(other, ctrl: true);
                    }

                    var held = Card(name);
                    var moving = along.Prepend(name).Select(Card).ToList();
                    var press = held.TransformToVisual((UIElement)held.Parent).TransformPoint(new Point(held.ActualWidth / 2, held.ActualHeight * 0.3));
                    var target = to(held);
                    surface.BeginMove(held, press);

                    // [S-16461] A stop on the way (an icon's box in the fence before the pointer), drawn there too.
                    var onTheWay = false;
                    if (via?.Invoke(held) is { } stop)
                    {
                        surface.PreviewMove(held, (stop.X - press.X) / metrics.Width, (stop.Y - press.Y) / metrics.Height);
                        surface.FlushMove();
                        await Task.Delay(300).ConfigureAwait(true);
                        onTheWay = surface.ShowsDragPicture;
                        await RenderHarness.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, $".move-several-{viaTag ?? tag + "-via"}.png"), Scope).ConfigureAwait(true);
                    }

                    surface.PreviewMove(held, (target.X - press.X) / metrics.Width, (target.Y - press.Y) / metrics.Height);
                    surface.FlushMove();
                    await stand.ReadyAsync($"{name} and {along.Length} more held").ConfigureAwait(true);
                    var ghosts = moving.TrueForAll(c => Math.Abs(c.Opacity - SelectionLook.Landing) < 0.001);
                    var picture = surface.ShowsDragPicture || onTheWay;
                    await RenderHarness.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, $".move-several-{tag}.png"), Scope).ConfigureAwait(true);
                    surface.CommitMove(held);
                    await stand.ReadyAsync($"{name} and {along.Length} more dropped").ConfigureAwait(true);
                    var whole = moving.TrueForAll(c => surface.CardFor(c.Model.Id) is { Opacity: 1 }) && !surface.ShowsDragPicture;
                    var ok = !picture && ghosts && whole && landed();
                    report.Expect(ok, $"move {name} with {string.Join(",", along)} ({tag}): a picture under the pointer {picture}"
                        + $", every moving icon a ghost where it lands {ghosts}, whole after {whole}, landed {landed()}");
                }

                var freeBefore = (Model(Index("iconA")).Rect, Model(Index("iconB")).Rect);
                await MoveSeveralAsync("iconB", ["iconA"], held => new Point(metrics.Width * 0.92, metrics.Height * 0.45), "free",
                    () => Model(Index("iconA")).Rect != freeBefore.Item1 && Model(Index("iconB")).Rect != freeBefore.Item2).ConfigureAwait(true);
                bool Fenced(string name) => Themia.Core.Desktop.DesktopFences.Holding(running.State.Views[0], KeyOf(name)) is not null;
                await MoveSeveralAsync("twinU", ["twinP"], held => new Point(metrics.Width * 0.90, metrics.Height * 0.80), "out",
                    () => !Fenced("twinU") && !Fenced("twinP")).ConfigureAwait(true);
                await MoveSeveralAsync("twinP", ["twinU"], held => fenceCard.TransformToVisual((UIElement)held.Parent).TransformPoint(new Point(fenceCard.ActualWidth * 0.3, fenceCard.ActualHeight * 0.5)), "fence",
                    () => Fenced("twinP")).ConfigureAwait(true);


                // [S-14310] Edit mode, the same rule: two selected show no marks and no grips of their own and the frame in its
                // edit look (hairline, markers, grips) round them; every card not selected keeps its own; one selected again
                // (narrowed, or one of two taken out) shows its own and the frame goes.
                host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                host.SetEditMode(true);
                var viewId = running.State.Views[0].Id;
                WidgetCard CardOf(string name) => surface.CardFor(items[Index(name)].Widget.Id)!;
                void ExpectEdit(string step, string[] framedWanted)
                {
                    var frame = shown.DebugFrame;
                    var members = framedWanted.Select(CardOf).ToList();
                    var framed = names.Where(n => shown.FramedWith(CardOf(n)) > 0).ToList();
                    var ok = framed.SequenceEqual(framedWanted)
                        && members.TrueForAll(c => !c.MarksDrawn)
                        && cards.Where(n => !framedWanted.Contains(n)).All(n => CardOf(n).MarksDrawn)
                        && (framedWanted.Length == 0 ? !frame.Shown : frame.Shown && !frame.CornersOnly && Round(members));
                    report.Expect(ok, $"edit mode, {step}: framed [{string.Join(",", framed)}], own marks [{string.Join(",", cards.Where(n => CardOf(n).MarksDrawn))}], frame {(frame.Shown ? frame.CornersOnly ? "corners" : "edit look" : "none")}");
                }

                host.SelectMany(viewId, [CardOf("clock").Model.Id, CardOf("tasks").Model.Id]);
                ExpectEdit("clock and tasks selected", ["clock", "tasks"]);
                host.ToggleSelect(CardOf("tasks").Model.Id);
                ExpectEdit("tasks taken out", []);
                host.ToggleSelect(CardOf("notes").Model.Id);
                ExpectEdit("notes added", ["clock", "notes"]);
                host.Select(CardOf("clock").Model.Id);
                ExpectEdit("narrowed to clock", []);
                host.Select(null);
                host.SetEditMode(false);

                // [S-14320] Delete on widgets picked by hand outside edit mode (owner, 2026-11-20: "deleting widgets with delete does
                // not work in non edit mode without the lock enabled"): the pick hands the desktop the keys as a press on empty desktop
                // does (before, the press on a card left them with no one); Delete (and Shift+Delete) removes the cards and an own
                // icon, Lock widgets on the icon alone; no file is in it; Ctrl+Z puts them back where they were, with their config.
                bool Present(string name) => running.State.Views[0].Find(items[Index(name)].Widget.Id) is not null;
                string Shape(string name) => running.State.Views[0].Find(items[Index(name)].Widget.Id) is { } w
                    ? $"{w.Kind}@{w.Rect} {w.Config.GetType().Name}"
                    : "gone";
                foreach (var permanent in new[] { false, true })
                {
                    host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                    var emptyTakes = ReferenceEquals(host.DesktopKeysSurface, surface);
                    host.DesktopKeysEnd(giveBack: false, keepActive: true);
                    string[] chosen = ["clock", "notes", "iconB"];
                    var shapes = chosen.ToDictionary(n => n, Shape);
                    Pick("clock");
                    Pick("notes");
                    var pickTakes = ReferenceEquals(host.DesktopKeysSurface, surface);
                    Click("iconB", ctrl: true);
                    host.Key(permanent ? WidgetKey.PermanentDelete : WidgetKey.Delete, shift: permanent, widgetId: null);
                    var gone = chosen.Where(n => !Present(n)).ToList();
                    List<string> wanted = locked ? ["iconB"] : [.. chosen];
                    var kept = names.Except(chosen).All(Present);
                    host.Key(WidgetKey.Undo, shift: false, widgetId: null);
                    var back = chosen.All(n => Shape(n) == shapes[n]);
                    // The keys as an empty press gets them: an off-screen stand-in display may be refused the foreground (it reads
                    // as covered), and then neither has them; the check asks that the pick does what the press on empty desktop does.
                    var deleteOk = gone.SequenceEqual(wanted) && kept && back && (locked || pickTakes == emptyTakes);
                    report.Expect(deleteOk, $"ctrl+press clock and notes, ctrl+click iconB, {(permanent ? "Shift+Delete" : "Delete")}: gone [{string.Join(",", gone)}]"
                        + $", the rest kept {kept}, Ctrl+Z put them back as they were {back}, the desktop's keys after an empty press {emptyTakes}, after the picks {pickTakes}");
                }

                // [S-14321] A card's menu outside edit mode, host-level (built, never shown: its popup would stand on the user's
                // screen): the right-click puts the card alone in the desktop's one selection (Lock widgets on: none) and gives the
                // desktop the keys; its lines are the widget's, Delete (not while locked), then the desktop's background menu whole.
                host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                var emptyKeys = ReferenceEquals(host.DesktopKeysSurface, surface);
                host.DesktopKeysEnd(giveBack: false, keepActive: true);
                var notesCard = surface.CardFor(items[Index("notes")].Widget.Id)!;
                host.CardMenuOpening(surface, notesCard.Model);
                var menuAt = surface.CentreOf(notesCard);
                var cardLines = notesCard.CardMenu(menuAt).ConvertAll(e => e.Text);
                var deskLines = host.DesktopMenuLines(surface, menuAt).ConvertAll(e => e.Text);
                var held = names.Where(n => running.DeskSelects(Model(Index(n)))).ToList();
                var keysNow = ReferenceEquals(host.DesktopKeysSurface, surface);
                var hasDelete = cardLines.Contains(Str.Shell_Card_DeleteWidget.Text());
                // [S-15832] Widget settings, locked or not.
                var hasSettings = cardLines.Contains(Str.Shell_Card_WidgetSettings.Text());
                var endsWithDesk = deskLines.Count > 0 && cardLines.Count > deskLines.Count && cardLines.Skip(cardLines.Count - deskLines.Count).SequenceEqual(deskLines);
                var menuOk = held.SequenceEqual(locked ? [] : ["notes"]) && keysNow == emptyKeys && hasDelete != locked && hasSettings && endsWithDesk;
                report.Expect(menuOk, $"right-click notes: selected [{string.Join(",", held)}], the desktop's keys {keysNow} (an empty press: {emptyKeys}), Delete widget {hasDelete}, Widget settings {hasSettings}"
                    + $", ends with the desktop's {deskLines.Count} lines {endsWithDesk}: [{string.Join(" | ", cardLines)}]");
                host.ClearDeskSelection();

                // [S-13047] A move that becomes the shell's drag (off the desktop, onto a free folder) hands it its picture and its
                // press: the drag shows the held icon's picture, held where the icon was grabbed, its cards back home. [S-16460] The
                // move showed none under the pointer before. [S-13048] A move of two: the picture carried is the held icon alone.
                {
                    host.DesktopEmptyPressed(surface, empty, ctrl: false, shift: false);
                    Click("iconA");
                    Click("iconB", ctrl: true);
                    var heldIcon = Card("iconA");
                    var layer = (UIElement)heldIcon.Parent;
                    var press = heldIcon.TransformToVisual(layer).TransformPoint(new Point(heldIcon.ActualWidth / 2, heldIcon.ActualHeight * 0.3));
                    var grab = Grab(heldIcon, press);
                    var home = (heldIcon.LiveRect, Card("iconB").LiveRect);
                    surface.BeginMove(heldIcon, press);
                    surface.PreviewMove(heldIcon, -0.1, 0.1);
                    surface.FlushMove();
                    var shownBefore = surface.ShowsDragPicture;
                    surface.CancelMove(heldIcon);
                    var carried = surface.HandedMove is { Picture: not null };
                    var (alone, said) = HeldAlone(surface.HandedMove?.Picture, grab);
                    var back = (heldIcon.LiveRect, Card("iconB").LiveRect) == home;
                    var handOk = !shownBefore && carried && alone && back;
                    report.Expect(handOk, $"a move of iconA with iconB handed to the shell's drag: a picture under the pointer before {shownBefore}, its picture carried {carried}, the held icon alone {alone} ({said}), its cards back {back}");
                }

                // [S-13708] An icon dragged off the Unplaced icons page (the one placement session, edit mode): under the pointer
                // it is the icon as the desktop draws it, with its corners, beside the desktop's own in edit mode. Drawn, then given up.
                var loose = new DesktopItem(Path.Combine(Path.GetTempPath(), "themia-desk-select-check", "Placed from the page (Check).txt"), "Placed from the page (Check).txt", DesktopItemKind.File);
                host.BeginPlacement([loose], metrics);
                if (host.Placing is { } placing)
                {
                    surface.ShowGhosts(placing.Outlines(0.30, 0.80, metrics, surface.SizesNow));
                    await stand.ReadyAsync("the page's icon held").ConfigureAwait(true);
                    var drawnAsIcon = surface.IconGhostsShown;
                    await RenderHarness.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, ".place-icon.png"), Scope).ConfigureAwait(true);
                    host.CancelPlacement();
                    var gone = surface.IconGhostsShown == 0;
                    report.Expect(drawnAsIcon == 1 && gone, $"an icon placed from the page: drawn as the desktop draws it {drawnAsIcon}, gone with the placement {gone}");
                }
                else
                {
                    report.Fail("an icon placed from the page: no placement began");
                }

                // [S-13713] [S-13715] The Unplaced icons page open in edit mode stays open beside the desktop (owner, 2026-11-21: "it
                // should not close when i click outside of it, that way it should also be possible to move desktop icons back to it"):
                // a desktop item selected and moved onto it leaves the view, the page lit and the move's picture over it while it is
                // there, the page still open after (no page of the selection's over it); dragged off the page onto the free desktop it
                // is placed there again. twinP out of the fence it stands in, then twinU from the free desktop (drawn mid-drop:
                // <out>.unplace-fence.png, .unplace-free.png).
                host.SetEditMode(true);
                await stand.ReadyAsync("edit mode").ConfigureAwait(true);
                surface.OpenPage(Themia.Core.Settings.SettingsPageId.DesktopItems, []);
                await stand.ReadyAsync("the Unplaced icons page open").ConfigureAwait(true);
                var homeViewId = running.State.Views[0].Id;
                bool Listed(string key) => host.UnplacedDesktopItems(homeViewId).Any(i => DesktopItemKeys.Comparer.Equals(i.Key, key));
                bool OnView(string key) => Themia.Core.Desktop.DesktopViews.Shown(running.State.Views[0]).Contains(key);
                bool InFence(string key) => Themia.Core.Desktop.DesktopFences.Holding(running.State.Views[0], key) is not null;
                WidgetCard? CardOfKey(string key) => running.State.Views[0].Widgets.FirstOrDefault(w => w.Config is IDesktopItemConfig c && DesktopItemKeys.Comparer.Equals(c.Item, key)) is { } w
                    ? surface.CardFor(w.Id)
                    : null;

                async Task ToPageAsync(string name, string tag)
                {
                    var key = KeyOf(name);
                    var page = surface.OpenUnplacedPart;
                    var fencedBefore = InFence(key);
                    if (CardOfKey(key) is not { } held || page is null)
                    {
                        report.Fail($"{name} onto the page ({tag}): {(page is null ? "the page is not open" : $"{name} has no card")}");
                        return;
                    }

                    // A selection made on the desktop opens no page over this one.
                    host.Select(held.Model.Id);
                    var layer = (UIElement)held.Parent;
                    var press = held.TransformToVisual(layer).TransformPoint(new Point(held.ActualWidth / 2, held.ActualHeight * 0.3));
                    var pressAt = layer.TransformToVisual(surface.DebugRoot).TransformPoint(press);
                    var target = page.TransformToVisual(surface.DebugRoot).TransformPoint(new Point(page.ActualWidth * 0.6, Math.Max(8, page.ActualHeight * 0.5)));
                    var over = surface.OverUnplacedPage(target);
                    surface.BeginMove(held, press);
                    surface.PreviewMove(held, (target.X - pressAt.X) / metrics.Width, (target.Y - pressAt.Y) / metrics.Height);
                    surface.FlushMove();
                    await stand.ReadyAsync($"{name} held over the page").ConfigureAwait(true);
                    var lit = page.DropShown;
                    var picture = surface.ShowsDragPicture;
                    await RenderHarness.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, $".unplace-{tag}.png"), Scope).ConfigureAwait(true);
                    surface.CommitMove(held);
                    surface.PressEnded(held);
                    await stand.ReadyAsync($"{name} dropped on the page").ConfigureAwait(true);
                    var stays = surface.OpenUnplacedPart is { DropShown: false };
                    var all = running.State.Views[0].DesktopIcons == ViewDesktopIcons.All;
                    var ok = over && lit && picture && Listed(key) && !OnView(key) && !InFence(key) && stays && all;
                    report.Expect(ok, $"{name} moved onto the open page ({tag}, in a fence before {fencedBefore}): over it {over}, the page lit {lit}, the picture under the pointer {picture}"
                        + $", listed on the page {Listed(key)}, off the view {!OnView(key)}, the page still open and unlit {stays}, the view still All {all}");
                }

                async Task FromPageAsync(string name, string tag, double x, double y)
                {
                    var key = KeyOf(name);
                    surface.DebugUnplacedDrag([key]);
                    var placed = host.Placing is not null && host.CompletePlacement(surface, x, y);
                    await stand.ReadyAsync($"{name} placed from the page").ConfigureAwait(true);
                    var reopened = surface.OpenUnplacedPart is not null;
                    var ok = placed && !Listed(key) && OnView(key) && !InFence(key) && reopened;
                    report.Expect(ok, $"{name} dragged off the page onto the free desktop ({tag}): placed {placed}, off the page {!Listed(key)}, on the view {OnView(key)}, free {!InFence(key)}, the page open again {reopened}");
                }

                await ToPageAsync("twinP", "fence").ConfigureAwait(true);
                await FromPageAsync("twinP", "back", 0.78, 0.86).ConfigureAwait(true);
                await ToPageAsync("twinU", "free").ConfigureAwait(true);
                await FromPageAsync("twinU", "back", 0.70, 0.86).ConfigureAwait(true);
                host.SetEditMode(false);
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

                // --live only: real input through SendInput, paced as a hand moves (a pause after the button, a step a
                // frame-or-two apart, the double-click time between clicks). Times on purpose: the OS's input queue and its
                // double-click rule read them, there is no signal of ours to wait for ([S-15900]).
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

                    // a timing, not a wait: the next click lands past the double-click time, so two clicks never read as one double-click.
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
                report.Expect(movedOk, $"drag the selection by iconA: moved [{string.Join(",", moved)}]");
                ok = ok && await Click(empty).ConfigureAwait(true);
                Expect("click empty", []);
                if (!ok)
                {
                    report.Fail("live: stopped early");
                }
            }

            await RenderHarness.SaveAsync(surface.DebugRoot, Path.ChangeExtension(outPath, ".png"), Scope).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            report.Crash(ex);
        }
        finally
        {
            if (cursor is { } back)
            {
                Key(VkControl, up: true);
                Key(VkShift, up: true);
                _ = SetCursorPos(back.X, back.Y);
            }

            desk?.Host.StartDeskForCheck(start: false);
            desk?.Dispose();
        }

        return await report.FinishAsync().ConfigureAwait(true);
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
