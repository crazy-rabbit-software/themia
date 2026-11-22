#if DEBUG
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-15526] [S-13917] Debug only: <c>Themia.exe --check forecast-edit &lt;out.txt&gt;</c>. A weather forecast's three states
/// through the app's own wiring: a real <see cref="AppHost"/> over a detached store and a real <see cref="DesktopWindow"/> on
/// a stand-in display whose view holds a new forecast and a clock. The surface's gestures are called as the cards' presses
/// call them: on the locked desktop and in edit mode a press on a part picks the forecast; a second click starts its layout
/// editing (nothing selected, the bento shown, the forecast held still); there a part is picked, a part dragged from the
/// bento joins the day, a part dragged out of the day is removed, the day box resized resizes the forecast and its parts
/// still touch; ending it selects the forecast again. Each step says what it found, the pictures stand beside the text
/// (<c>.edit.png</c>, <c>.layout.png</c>, <c>.part.png</c>, <c>.added.png</c>, <c>.resized.png</c>, <c>.done.png</c>) and the
/// last line is PASS or FAIL.
/// </summary>
internal static class ForecastEditCheck
{
    private const string Scope = "forecast-edit-check";
    private const string DisplayId = "forecast-edit-check-display";

    public static RenderCommand Command { get; } = new(
        RenderVerb.Check,
        "forecast-edit",
        "<out.txt>",
        """
        a weather forecast's three states through a real host and surface: locked and edit mode (one widget), its layout
        editing (parts picked, the bento, a part dragged in and out, the day box), PASS or FAIL, pictures beside the text
        (<out>.edit.png, .layout.png, .part.png, .added.png, .resized.png, .done.png) [S-15526]
        """,
        RunAsync);

    private static async Task<int> RunAsync(RenderRun run)
    {
        var outPath = run.PathArg(0);
        var report = new CheckReport(Scope, outPath);
        StandInDesktop? desk = null;

        void Check(string what, bool ok, string found) => report.Expect(ok, $"{what}: {found}");

        Log.AddSink(static record => Console.WriteLine(LogFormat.Record(record)));
        try
        {
            var state = AppState.CreateDefault();
            var view = state.Views[0];
            view.Widgets.Clear();
            var screen = new ScreenMetrics(1920, 1080);
            var forecast = WidgetInstance.Create(new WidgetId("weatherDays"), new FracRect(0.1, 0.15, 0.2, 0.2));
            ((IPlaced)forecast.Config).UseDeviceLocation = false;
            ((IPlaced)forecast.Config).Latitude = 47.3769;
            ((IPlaced)forecast.Config).Longitude = 8.5417;
            ((IPlaced)forecast.Config).PlaceName = "Zurich";
            var clock = WidgetInstance.Create(new WidgetId("clock"), new FracRect(0.6, 0.15, 0.18, 0.14));
            view.Widgets.Add(forecast);
            view.Widgets.Add(clock);
            state.ShowViewOn(DisplayId, view.Id);

            desk = StandInDesktop.Create(Scope, state);
            var host = desk.Host;
            desk.UseCheckButton(static () => false);
            var surface = desk.Add(StandInDesktop.Display(DisplayId, StandInDesktop.OffScreen(1920, 1080)));
            host.StartDeskForCheck(start: true);
            desk.Show(surface);

            // A diagnostic's wait, not a correctness path: the surface lays out and the intro ends.
            await Task.Delay(2500).ConfigureAwait(true);

            var shown = surface;
            var running = host;
            var live = running.State.Views[0];

            // Its first parts on the surface's own grid, as a placement seeds them.
            screen = shown.Metrics;
            running.Mutate(_ =>
            {
                var placed = live.Find(forecast.Id)!;
                placed.Rect = GridGeometry.Snap(placed.Rect, screen);
                Repeaters.Seed(live, new RepeaterOn(placed, (IRepeater)placed.Config), screen);
            });
            await Task.Delay(800).ConfigureAwait(true);
            var id = forecast.Id;
            IRepeater Live() => (IRepeater)live.Find(id)!.Config;
            string Selected() => running.Selection.IsEmpty ? "(nothing)" : string.Join(",", running.Selection.Ids.Select(Name));
            string Name(string widget) => widget == id ? "forecast" : widget == clock.Id ? "clock" : live.Find(widget)?.Kind.Value ?? widget;
            WidgetCard Card(string widget) => shown.CardFor(widget) ?? throw new InvalidOperationException($"no card for {Name(widget)}");
            void Click(WidgetCard card)
            {
                shown.RequestPress(card, onHandle: false);
                shown.RequestClick(card);
                shown.PressEnded(card);
            }

            async Task Shot(string tag)
            {
                await Task.Delay(400).ConfigureAwait(true);
                await RenderHarness.SaveAsync(shown.DebugRoot, Path.ChangeExtension(outPath, $".{tag}.png"), Scope).ConfigureAwait(true);
            }

            var parts = Live().Members.ToList();
            Check("a new forecast has its five parts and a week of days", parts.Count == 5 && shown.CopyCards.Count() == 5 * 6, $"{parts.Count} parts, {shown.CopyCards.Count()} cards of other days");
            Check("it has no background", live.Find(id)!.Background.Kind == FillKind.Transparent, $"{live.Find(id)!.Background.Kind}");

            // ---- the locked desktop: a press on a part is the forecast's
            Check("locked: a press on a part is the forecast's", shown.DebugComposing is null && Stands(shown, Card(parts[0])) == id, $"stands for {Name(Stands(shown, Card(parts[0])))}");
            Check("locked: a press on another day is the forecast's", Stands(shown, shown.CopyCards.First()) == id, $"stands for {Name(Stands(shown, shown.CopyCards.First()))}");

            // ---- edit mode: the forecast is one widget
            running.SetEditMode(true);
            await Task.Delay(600).ConfigureAwait(true);
            Click(Card(parts[1]));
            Check("edit mode: a click on a part selects the forecast", Selected() == "forecast" && shown.DebugComposing is null, Selected());
            Check("edit mode: its page is offered, not its layout", !shown.DebugPaletteShown, shown.DebugPaletteShown ? "bento shown" : "no bento");
            await Shot("edit").ConfigureAwait(true);

            Click(Card(clock.Id));
            Click(shown.CopyCards.First());
            Check("edit mode: a click on another day selects the forecast too", Selected() == "forecast" && shown.DebugComposing is null, Selected());

            // ---- the second click: its layout
            Click(Card(parts[1]));
            Check("a second click starts its layout editing", shown.DebugComposing == id, shown.DebugComposing is null ? "not editing" : "editing");
            Check("layout: nothing is selected", running.Selection.IsEmpty, Selected());
            Check("layout: the bento is shown and its page is closed", shown.DebugPaletteShown && !shown.DebugFlyoutOpen, $"bento {shown.DebugPaletteShown}, page {shown.DebugFlyoutOpen}");
            Check("layout: the desktop is darkened", shown.DebugDimmed, shown.DebugDimmed ? "smoke" : "no smoke");
            await Shot("layout").ConfigureAwait(true);

            // ---- a part is picked; the forecast and its other days hold still
            Click(Card(parts[1]));
            Check("layout: a click on a part selects that part", Selected() == "weatherSky" && shown.DebugComposing == id, Selected());
            Check("layout: its page stays closed", !shown.DebugFlyoutOpen, shown.DebugFlyoutOpen ? "open" : "closed");
            await Shot("part").ConfigureAwait(true);

            Click(Card(parts[3]));
            Check("layout: another part takes the selection", Selected() == "weatherRange", Selected());
            Click(shown.CopyCards.First());
            Check("layout: a click on another day lets go of the part and keeps editing", running.Selection.IsEmpty && shown.DebugComposing == id, Selected());

            var before = live.Find(id)!.Rect;
            var held = Card(id);
            shown.RequestPress(held, onHandle: false);
            shown.BeginMove(held, new Point(0, 0));
            shown.PreviewMove(held, 0.1, 0.1);
            shown.CommitMove(held);
            shown.PressEnded(held);
            Check("layout: the forecast itself does not move", live.Find(id)!.Rect == before && shown.DebugComposing == id, $"{live.Find(id)!.Rect.X:F3},{live.Find(id)!.Rect.Y:F3}");

            // ---- a part moved inside its day follows on every day; dragged out, it is removed
            var sky = Card(parts[1]);
            var skyAt = live.Find(parts[1])!.Rect;
            shown.RequestPress(sky, onHandle: false);
            shown.BeginMove(sky, new Point(0, 0));
            shown.PreviewMove(sky, 0, 1.0 / GridGeometry.GridDims(screen).Rows);
            shown.CommitMove(sky);
            shown.PressEnded(sky);
            var skyNow = live.Find(parts[1])?.Rect;
            Check("layout: a part moves on the day", skyNow is { } moved && moved.Y > skyAt.Y && Live().Members.Contains(parts[1]), skyNow is { } m ? $"y {skyAt.Y:F3} -> {m.Y:F3}" : "gone");
            Check("layout: the day grew to hold it and every other day follows", Live().DayHeight > 4 && shown.CopyCards.All(c => c.Model.Rect.H > 0), $"day {Live().DayWidth} x {Live().DayHeight} cells");

            // ---- a part from the bento joins the day it is let go on
            var day3 = Repeaters.Grid(live, new RepeaterOn(live.Find(id)!, Live()), screen).Slot(2);
            running.DropPart(shown, id, new WidgetId("weatherWind"), day3.X + (day3.W / 2), day3.Y + (day3.H / 2));
            await Task.Delay(300).ConfigureAwait(true);
            var wind = Live().Members.FirstOrDefault(p => live.Find(p)?.Kind.Value == "weatherWind");
            Check("layout: a part dragged from the bento onto a day joins it", wind is not null && Live().Members.Count == 6, $"{Live().Members.Count} parts");
            Check("layout: the new part is selected", wind is not null && Selected() == "weatherWind", Selected());
            running.DropPart(shown, id, new WidgetId("weatherUv"), 0.9, 0.9);
            Check("layout: a part let go off the forecast adds nothing", Live().Members.Count == 6, $"{Live().Members.Count} parts");
            await Shot("added").ConfigureAwait(true);

            if (wind is not null)
            {
                var out1 = Card(wind);
                shown.RequestPress(out1, onHandle: false);
                shown.BeginMove(out1, new Point(0, 0));
                shown.PreviewMove(out1, 0.5, 0.5);
                shown.CommitMove(out1);
                shown.PressEnded(out1);
                Check("layout: a part dragged out of the day is removed", !Live().Members.Contains(wind) && live.Find(wind) is null && shown.DebugComposing == id, live.Find(wind) is null ? "removed" : "still a widget");
            }

            // ---- the day box and the forecast's width are one, and parts that touch still touch
            var (cols, _) = GridGeometry.GridDims(screen);
            var wide = Live().DayWidth + 10;
            running.SetDayBox(shown, id, wide, Live().DayHeight);
            var row = Live().Members.Select(p => live.Find(p)!).OrderBy(p => p.Rect.X).ToList();
            var touching = Enumerable.Range(0, row.Count - 1).All(i => row[i + 1].Rect.X - row[i].Rect.Right < 1e-9);
            var box = live.Find(id)!.Rect;
            Check("the day box resized resizes the forecast", Math.Abs((box.W * cols) - Live().DayWidth) < 1e-6 && Live().DayWidth > wide - 10, $"day {Live().DayWidth} cells, forecast {box.W * cols:F1} cells");
            Check("no gap opens between parts that touched", touching, string.Join(" ", row.Select(p => $"{p.Rect.X * cols:F1}..{p.Rect.Right * cols:F1}")));
            await Shot("resized").ConfigureAwait(true);

            // ---- out again: a widget once more
            shown.EndCompose();
            Check("ending it selects the forecast again", shown.DebugComposing is null && Selected() == "forecast", Selected());
            Check("the bento and the smoke are gone", !shown.DebugPaletteShown && !shown.DebugDimmed, $"bento {shown.DebugPaletteShown}, smoke {shown.DebugDimmed}");
            shown.Compose(id);
            running.SetEditMode(false);
            await Task.Delay(400).ConfigureAwait(true);
            Check("leaving edit mode ends a layout's editing", shown.DebugComposing is null && !shown.DebugPaletteShown, shown.DebugComposing is null ? "ended" : "still editing");
            await Shot("done").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            report.Crash(ex);
        }
        finally
        {
            desk?.Host.StartDeskForCheck(start: false);
            desk?.Dispose();
        }

        return await report.FinishAsync().ConfigureAwait(true);
    }

    /// <summary>The widget a press on <paramref name="card"/> stands for on <paramref name="surface"/>.</summary>
    private static string Stands(DesktopWindow surface, WidgetCard card) => surface.DebugStands(card);
}
#endif
