using System.Runtime.CompilerServices;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Series;
using Themia.Core.Settings;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Services;

/// <summary>
/// [S-15500]..[S-15532] The host's side of the repeaters: Core's rules (<see cref="Repeaters"/>) run in every change on each
/// view's display, a move let go lands its widgets in or out of one, the wheel scrolls one, and a surface asks what the
/// repeaters of its view draw.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>[S-15514] What each repeater repeats by and where its values come from, as last seen: a change puts it at its natural.</summary>
    private readonly ConditionalWeakTable<IRepeater, string> _repeaterNotes = [];

    /// <summary>
    /// [S-15500] [S-15503] [S-15514] [S-15515] Every repeater of every view kept whole and laid on the display that shows its
    /// view, in the change being made (<see cref="Mutate"/>); one whose source or direction the change set anew is put at its
    /// natural first. A view no display shows keeps its rects till it is shown.
    /// </summary>
    private void LayRepeaters(AppState state)
    {
        foreach (var view in state.Views)
        {
            if (!view.Widgets.Exists(w => w.Config is IRepeater))
            {
                continue;
            }

            var screen = ScreenOfView(view.Id) ?? new ScreenMetrics(0, 0);
            foreach (var on in Repeaters.On(view).ToList())
            {
                var note = Repeaters.SourceNote(on.Repeater);
                if (_repeaterNotes.TryGetValue(on.Repeater, out var was) && was != note)
                {
                    Repeaters.FitNatural(view, on, screen);
                }

                _repeaterNotes.AddOrUpdate(on.Repeater, note);
            }

            Repeaters.Lay(view, screen);
        }

        WatchLiveSeries(state);
    }

    /// <summary>[S-15510] The drives watched while a repeater reads a live source (the connected drives): a change redraws them.</summary>
    private IDisposable? _liveSeries;

    /// <summary>
    /// [S-15510] [S-15514] A repeater over a live source follows it: the drive service is watched while one of any view reads
    /// the connected drives, and every surface redraws its repeaters when the drives change (the card keeps its size; its rows
    /// follow, [S-15514]). No repeater reading one: no watch.
    /// </summary>
    private void WatchLiveSeries(AppState state)
    {
        var live = state.Views.Exists(v => Repeaters.On(v).Any(on => SeriesSources.For(on.Repeater) is { Live: true }));
        if (live && _liveSeries is null)
        {
            _liveSeries = Drives.DriveService.Shared.Watch(Ticker, () => StateChanged?.Invoke(StateChange.All));
        }
        else if (!live && _liveSeries is not null)
        {
            _liveSeries.Dispose();
            _liveSeries = null;
        }
    }

    /// <summary>[S-15502] What the repeaters of <paramref name="view"/> draw on a display of <paramref name="metrics"/>.</summary>
    internal static RepeaterPlan RepeaterPlanOf(ViewState view, ScreenMetrics metrics)
        => view.Widgets.Exists(w => w.Config is IRepeater) && metrics.IsUsable ? Repeaters.Plan(view, metrics) : RepeaterPlan.Empty;

    /// <summary>[S-15500] [S-15527] <paramref name="ids"/> and every widget a repeater among them holds: what a move carries and a delete takes.</summary>
    internal static List<string> WithHeld(ViewState view, IEnumerable<string> ids)
        => view.Widgets.Exists(w => w.Config is IRepeater) ? Repeaters.WithDescendants(view, ids) : [.. ids];

    /// <summary>
    /// [S-15515] [S-15532] The mouse wheel over a repeater on <paramref name="surface"/> at <paramref name="dip"/>: the innermost
    /// one under the point that can still scroll that way moves a row (Wrap: a line) each notch. Not an undo step: a look.
    /// False where none can.
    /// </summary>
    internal bool RepeaterWheel(DesktopWindow surface, Point dip, int delta)
    {
        var metrics = surface.Metrics;
        if (delta == 0 || !metrics.IsUsable)
        {
            return false;
        }

        var view = State.ViewForMonitor(surface.Monitor.Id);
        if (!view.Widgets.Exists(w => w.Config is IRepeater))
        {
            return false;
        }

        var (x, y) = (dip.X / metrics.Width, dip.Y / metrics.Height);
        var step = delta > 0 ? -1 : 1;
        var values = Repeaters.ValuesOf(view);
        var under = Repeaters.On(view)
            .Where(on => on.Widget.Rect is var r && x >= r.X && x < r.Right && y >= r.Y && y < r.Bottom)
            .OrderByDescending(on => Repeaters.Chain(view, on.Widget.Id).Count);
        foreach (var on in under)
        {
            var grid = Repeaters.Grid(view, on, metrics);
            var count = values.TryGetValue(on.Widget.Id, out var list) ? list.Count : 0;
            var to = Math.Clamp(on.Repeater.Scroll + step, 0, grid.MaxScroll(count));
            if (to != on.Repeater.Scroll)
            {
                var repeater = on.Repeater;
                Mutate(_ => repeater.Scroll = to, undo: null);
                return true;
            }
        }

        return false;
    }

    /// <summary>[S-15511] The types the slots of <paramref name="repeaterId"/>'s members (however deep) have: what its Repeat by offers.</summary>
    internal static IReadOnlyList<SeriesType> OfferedBy(ViewState view, string repeaterId)
        => [.. Repeaters.Descendants(view, repeaterId)
            .Select(view.Find)
            .OfType<WidgetInstance>()
            .SelectMany(w => SeriesSlots.Of(w.Config))
            .Select(s => s.Type)
            .Distinct()
            .Order()];

    /// <summary>
    /// [S-15512] [S-15529] A widget's lines as a container sets them (owner, 2026-11-21: "the setup should not be duplicated
    /// accross everything"; "settings like location should not be settable per widget"): a part has no line for what its
    /// container gives it, neither the place nor its day; the container's own page gains its parts' Add lines ([S-13914]).
    /// A widget no container holds keeps its lines as they are.
    /// </summary>
    private void RepeaterLines(ViewState view, WidgetInstance widget, Themia.Sdk.WinUI.WidgetMenuLines? lines)
    {
        if (lines is null || !view.Widgets.Exists(w => w.Config is IRepeater))
        {
            return;
        }

        if (widget.Config is IRepeater own)
        {
            PartLines(view, widget, own, lines);
            return;
        }

        if (Repeaters.Placer(view, widget, widget.Config) is not null)
        {
            lines.Lines.RemoveAll(l => l.Shares == SharedSettings.Place.Key);
        }

        foreach (var slot in SeriesSlots.Of(widget.Config).Where(s => Repeaters.IsBound(view, widget, s)))
        {
            lines.Lines.RemoveAll(l => l.Shares == slot.Shared.Key);
        }

        // No separator left at either end or twice in a row.
        for (var i = lines.Lines.Count - 1; i >= 0; i--)
        {
            if (lines.Lines[i] is SettingsSeparator && (i == 0 || i == lines.Lines.Count - 1 || lines.Lines[i - 1] is SettingsSeparator))
            {
                lines.Lines.RemoveAt(i);
            }
        }
    }

    /// <summary>[S-13914] The kinds a container's day takes (<see cref="IRepeater.Accepts"/>), in the catalog's order.</summary>
    internal static IReadOnlyList<WidgetDescriptor> PartKinds(IRepeater repeater)
        => [.. ModuleCatalog.Widgets.Where(d => repeater.Accepts(d.CreateDefault()))];

    /// <summary>
    /// [S-13917] A container's page: Edit layout, which closes the page and starts its layout editing on the display that shows
    /// it (the same as a click on it while it is selected). Its parts are added there, from the bento, never from the page.
    /// </summary>
    private void PartLines(ViewState view, WidgetInstance widget, IRepeater repeater, Themia.Sdk.WinUI.WidgetMenuLines lines)
    {
        _ = repeater;
        lines.Lines.Add(SettingsLine.Separator);
        lines.Lines.Add(lines.Command(Glyphs.Edit, Str.Series_EditLayout.Text(), () =>
        {
            if (_windows.FirstOrDefault(w => w.IsAlive && string.Equals(w.CurrentViewId, view.Id, StringComparison.Ordinal)) is { } surface
                && !_dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => surface.Compose(widget.Id)))
            {
                Log.Warn(Scope, "a container's layout editing could not start: the dispatcher refused the step");
            }
        }));
    }

    /// <summary>
    /// [S-13917] A part of <paramref name="kind"/> dragged from the bento and let go at (<paramref name="x"/>, <paramref name="y"/>)
    /// on <paramref name="surface"/>: over one of <paramref name="containerId"/>'s days it joins its day box at the same place
    /// within the day (its middle under the pointer, on whole cells), one undo step, and is selected; elsewhere nothing.
    /// </summary>
    internal void DropPart(DesktopWindow surface, string containerId, WidgetId kind, double x, double y)
    {
        var view = State.ViewForMonitor(surface.Monitor.Id);
        var screen = surface.Metrics;
        if (!screen.IsUsable || view.Find(containerId) is not { Config: IRepeater repeater } widget)
        {
            return;
        }

        var on = new RepeaterOn(widget, repeater);
        var grid = Repeaters.Grid(view, on, screen);
        var slot = grid.SlotAt(x, y);
        if (slot < 0)
        {
            return;
        }

        var (dx, dy) = grid.Offset(slot);
        var (cols, rows) = GridGeometry.GridDims(screen);
        var (w, h) = PartSize(kind, screen, Repeaters.DaySize(view, on, screen));
        var left = Math.Round((x - dx - (w / 2)) * cols) / cols;
        var top = Math.Round((y - dy - (h / 2)) * rows) / rows;
        var at = new FracRect(Math.Max(widget.Rect.X, left), Math.Max(widget.Rect.Y, top), w, h);
        WidgetInstance? part = null;
        Mutate(_ => part = AddPart(view, widget, repeater, kind, at), undo: UndoAction.Add);
        if (part is not null && !_dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => SelectMany(view.Id, [part.Id])))
        {
            Log.Warn(Scope, "a new part could not be selected: the dispatcher refused the step");
        }
    }

    /// <summary>
    /// [S-13914] A new part's size in whole grid cells: a single metric at its least (a day is a compact row of them), anything
    /// else at its natural, held to the day box (<paramref name="day"/>, in cells) and never under its own least. What the
    /// shelf shows a part at and what it lands with.
    /// </summary>
    internal static (int W, int H) PartCells(WidgetId kind, ScreenMetrics screen, (int W, int H) day)
    {
        var descriptor = ModuleCatalog.Of(kind);
        var fresh = descriptor.CreateDefault();
        var sizing = descriptor.Sizing(fresh);
        var size = fresh is IMetricGraph ? sizing.Min : sizing.Natural;
        var (cols, rows) = GridGeometry.GridDims(screen);
        int Cells(double dip, double least, double across, int lines, int most)
            => Math.Max((int)Math.Ceiling((least / across * lines) - 1e-6), Math.Min(most, (int)Math.Ceiling((dip / across * lines) - 1e-6)));
        return (Cells(size.Width, sizing.Min.Width, screen.Width, cols, day.W), Cells(size.Height, sizing.Min.Height, screen.Height, rows, day.H));
    }

    /// <summary>[S-13914] <see cref="PartCells"/> as fractions of the screen.</summary>
    private static (double W, double H) PartSize(WidgetId kind, ScreenMetrics screen, (int W, int H) day)
    {
        var (w, h) = PartCells(kind, screen, day);
        var (cols, rows) = GridGeometry.GridDims(screen);
        return (w / (double)cols, h / (double)rows);
    }

    /// <summary>
    /// [S-13914] A part of <paramref name="kind"/> put in <paramref name="widget"/>'s day box: after its last part (beside it in
    /// rows, under it in columns and a grid), at its kind's natural size in whole grid cells. Null where no display shows it.
    /// </summary>
    private WidgetInstance? AddPart(ViewState view, WidgetInstance widget, IRepeater repeater, WidgetId kind, FracRect? at = null)
    {
        var screen = ScreenOfView(view.Id) ?? new ScreenMetrics(0, 0);
        if (!screen.IsUsable)
        {
            return null;
        }

        var template = Repeaters.Template(view, new RepeaterOn(widget, repeater));
        var (w, h) = PartSize(kind, screen, Repeaters.DaySize(view, new RepeaterOn(widget, repeater), screen));
        var box = widget.Rect;
        var (x, y) = at is { } spot
            ? (spot.X, spot.Y)
            : template.Count == 0
                ? (box.X, box.Y)
                : repeater.Direction == RepeatDirection.Down
                    ? (template.Max(m => m.Rect.Right), box.Y)
                    : (box.X, template.Max(m => m.Rect.Bottom));
        var part = WidgetInstance.Create(kind, new FracRect(x, y, w, h));
        view.Widgets.Add(part);
        repeater.Members.Add(part.Id);
        return part;
    }

    /// <summary>[S-13912] A container's day box set to <paramref name="width"/> × <paramref name="height"/> grid cells: one undo step.</summary>
    internal void SetDayBox(DesktopWindow surface, string id, int width, int height)
    {
        var view = State.ViewForMonitor(surface.Monitor.Id);
        var screen = surface.Metrics;
        if (view.Find(id) is { Config: IRepeater repeater } widget && screen.IsUsable)
        {
            // [S-13916] Its parts scale with it, and its tied side follows.
            Mutate(_ => Repeaters.Resize(view, new RepeaterOn(widget, repeater), width, height, screen), undo: UndoAction.Resize);
        }
    }

    /// <summary>[S-13915] A forecast placed bare starts with its first parts.</summary>
    private void SeedParts(ViewState view, IEnumerable<WidgetInstance> added)
    {
        if (ScreenOfView(view.Id) is not { IsUsable: true } screen)
        {
            return;
        }

        foreach (var widget in added.Where(w => w.Config is IRepeater { Members.Count: 0 }).ToList())
        {
            Repeaters.Seed(view, new RepeaterOn(widget, (IRepeater)widget.Config), screen);
        }
    }

    /// <summary>A slot's type's name, for a slot whose kind has no line of its own for it.</summary>
    private static string SlotName(SeriesSlot slot) => (slot.Type switch
    {
        SeriesType.Drive => Str.Series_Type_Drive,
        SeriesType.Place => Str.Series_Type_Place,
        _ => Str.Series_Type_Day,
    }).Text();

    /// <summary>The settle's step for the repeaters, logged and left as they were should it throw.</summary>
    private void SettleRepeaters()
    {
        try
        {
            LayRepeaters(State);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "laying the repeaters threw; they stay as they were", ex);
        }
    }
}
