using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>One widget as an undo snapshot sees it: where it is and what it is, plus its config by reference.</summary>
public sealed record WidgetShape(
    string Id,
    WidgetId Kind,
    double X,
    double Y,
    double W,
    double H,
    string? Label,
    FillChoice Background,
    HeaderPlacement Header,
    WidgetConfig Config)
{
    /// <summary>[S-6590] The widget's background image, undone with its background.</summary>
    public string? BackgroundImage { get; init; }

    public static WidgetShape Of(WidgetInstance widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return new(widget.Id, widget.Kind, widget.X, widget.Y, widget.W, widget.H, widget.Label,
            widget.Background.Clone(), widget.Header, widget.Config) { BackgroundImage = widget.BackgroundImage };
    }

    /// <summary>Everything but the config: see <see cref="LayoutShape"/> for why content is not compared.</summary>
    public bool SameLayout(WidgetShape other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Id == other.Id && Kind == other.Kind
            && X.Equals(other.X) && Y.Equals(other.Y) && W.Equals(other.W) && H.Equals(other.H)
            && Label == other.Label && Background.SameAs(other.Background) && Header == other.Header
            && string.Equals(BackgroundImage, other.BackgroundImage, StringComparison.Ordinal);
    }
}

/// <summary>One view as an undo snapshot sees it, its widgets in z-order.</summary>
public sealed record ViewShape(string Id, string Name, string? Icon, IReadOnlyList<WidgetShape> Widgets)
{
    /// <summary>[S-3810] The icon's colour, or null for the theme's.</summary>
    public string? Color { get; init; }
}

/// <summary>
/// [S-886] The part of the state an undo restores: the views, their names, icons and order, and every
/// widget's kind, geometry, z-order, header, background and title.
/// <para>
/// <b>Not</b> the widgets' content. A config is the widget's own data — the text of a note, a
/// timer's state — and is changed by using the widget, not by arranging the
/// desktop. Undoing a move must not also take back the sentence typed into a note since, so a
/// restore keeps every surviving widget's <i>current</i> config. A snapshot still holds each config
/// by reference (no copy), which is exactly what a widget that the undo brings back needs: a
/// deleted widget's config object is no longer reachable from the state, so nothing changed it
/// after it was deleted.
/// </para>
/// <para>
/// Not the display assignments either: showing another view is navigating, not editing, and is not
/// undone. The assignments are captured only so a view an undo brings back returns to the displays
/// that were showing it.
/// </para>
/// </summary>
public sealed class LayoutShape
{
    private LayoutShape(IReadOnlyList<ViewShape> views, IReadOnlyDictionary<string, string> monitorViews)
    {
        Views = views;
        MonitorViews = monitorViews;
    }

    public IReadOnlyList<ViewShape> Views { get; }

    /// <summary>Display to view, as captured; read only for views a restore brings back.</summary>
    public IReadOnlyDictionary<string, string> MonitorViews { get; }

    public static LayoutShape Capture(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var views = new List<ViewShape>(state.Views.Count);
        foreach (var view in state.Views)
        {
            var widgets = new WidgetShape[view.Widgets.Count];
            for (var i = 0; i < widgets.Length; i++)
            {
                widgets[i] = WidgetShape.Of(view.Widgets[i]);
            }

            views.Add(new ViewShape(view.Id, view.Name, view.Icon, widgets) { Color = view.Color });
        }

        return new LayoutShape(views, new Dictionary<string, string>(state.MonitorViews, StringComparer.Ordinal));
    }

    /// <summary>Whether the two arrange the desktop identically (configs and display assignments aside).</summary>
    public bool SameLayout(LayoutShape other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Views.Count != other.Views.Count)
        {
            return false;
        }

        for (var v = 0; v < Views.Count; v++)
        {
            var mine = Views[v];
            var theirs = other.Views[v];
            if (mine.Id != theirs.Id || mine.Name != theirs.Name || mine.Icon != theirs.Icon || mine.Color != theirs.Color
                || mine.Widgets.Count != theirs.Widgets.Count)
            {
                return false;
            }

            for (var w = 0; w < mine.Widgets.Count; w++)
            {
                if (!mine.Widgets[w].SameLayout(theirs.Widgets[w]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Make <paramref name="state"/> arranged as this snapshot is.
    /// <para>
    /// Objects are reused by id: a view or widget that survives keeps its instance (and so its
    /// card on screen, which the surface matches by reference) and its current config; one that
    /// comes back is rebuilt from the snapshot. Every reference to a view that goes is repaired as
    /// a delete repairs it ([S-902]): no display, active view, new-display default or view bar is
    /// left pointing at nothing.
    /// </para>
    /// </summary>
    public void RestoreInto(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var viewsById = state.Views.ToDictionary(v => v.Id, StringComparer.Ordinal);
        var widgetsById = new Dictionary<string, WidgetInstance>(StringComparer.Ordinal);
        foreach (var widget in state.Views.SelectMany(v => v.Widgets))
        {
            widgetsById.TryAdd(widget.Id, widget);
        }

        var restored = new List<ViewState>(Views.Count);
        var returning = new HashSet<string>(StringComparer.Ordinal);

        foreach (var shape in Views)
        {
            if (!viewsById.TryGetValue(shape.Id, out var view))
            {
                view = new ViewState { Id = shape.Id, Name = shape.Name };
                returning.Add(shape.Id);
            }

            view.Name = shape.Name;
            view.Icon = shape.Icon;
            view.Color = shape.Color;

            var widgets = new List<WidgetInstance>(shape.Widgets.Count);
            foreach (var w in shape.Widgets)
            {
                if (widgetsById.TryGetValue(w.Id, out var widget) && widget.Kind == w.Kind)
                {
                    widget.X = w.X;
                    widget.Y = w.Y;
                    widget.W = w.W;
                    widget.H = w.H;
                    widget.Label = w.Label;
                    widget.Background = w.Background.Clone();
                    widget.BackgroundImage = w.BackgroundImage;
                    widget.Header = w.Header;
                }
                else
                {
                    widget = new WidgetInstance
                    {
                        Id = w.Id,
                        X = w.X,
                        Y = w.Y,
                        W = w.W,
                        H = w.H,
                        Label = w.Label,
                        Background = w.Background.Clone(),
                        BackgroundImage = w.BackgroundImage,
                        Header = w.Header,
                        Config = w.Config,
                    };
                }

                widgets.Add(widget);
            }

            view.Widgets = widgets;
            restored.Add(view);
        }

        state.Views = restored;

        var exists = new HashSet<string>(restored.Select(v => v.Id), StringComparer.Ordinal);

        // A view the undo brings back goes back on the displays that showed it.
        foreach (var (monitor, viewId) in MonitorViews)
        {
            if (returning.Contains(viewId))
            {
                state.MonitorViews[monitor] = viewId;
            }
        }

        foreach (var monitor in state.MonitorViews.Where(kv => !exists.Contains(kv.Value)).Select(kv => kv.Key).ToList())
        {
            state.MonitorViews.Remove(monitor);
        }

        if (state.ActiveViewId is { } active && !exists.Contains(active))
        {
            state.ActiveViewId = restored.Count > 0 ? restored[0].Id : null;
        }

        if (state.DefaultViewForNewDisplays is { } fallback && !exists.Contains(fallback))
        {
            state.DefaultViewForNewDisplays = null;
        }

        foreach (var bar in state.ViewBars.Values)
        {
            bar?.VisibleViews.RemoveAll(id => !exists.Contains(id));
        }
    }
}

/// <summary>
/// [S-884]..[S-887] The desktop's undo history, kept beside the single write path.
/// <para>
/// The host calls <see cref="Commit"/> after every change it makes; the history compares the
/// layout with the one it last saw (the <i>baseline</i>) and records a step only when the
/// arrangement really changed. A change that does not move anything — a design slider, a widget's
/// settings, a view switch — records nothing, so nothing has to be classified as "undoable" up
/// front and no feature can forget to record itself.
/// </para>
/// <para>
/// A change made without a commit (a geometry write that skipped it, a display's view created by
/// the system) is "drift". Drift is folded into the next recorded step, or recorded on its own
/// before an undo — never silently lost under a restore, which would take back a change the user
/// can still see. <see cref="Rebase"/> adopts drift the user did not make (a view created for a
/// new display) without making it undoable.
/// </para>
/// </summary>
public sealed class LayoutHistory
{
    private readonly UndoLog<LayoutShape> _log;
    private LayoutShape _baseline;

    public LayoutHistory(AppState state, int capacity = UndoLog<LayoutShape>.DefaultCapacity)
    {
        ArgumentNullException.ThrowIfNull(state);
        _log = new UndoLog<LayoutShape>(capacity);
        _baseline = LayoutShape.Capture(state);
    }

    public bool CanUndo => _log.CanUndo;

    public bool CanRedo => _log.CanRedo;

    public int UndoCount => _log.UndoCount;

    /// <summary>
    /// Bumped by every recorded step, undo, redo and clear. An "Undo" button on a notice keeps the
    /// version it was offered at and acts only while it is unchanged: after anything else happened,
    /// the newest step is no longer the one the notice named.
    /// </summary>
    public long Version { get; private set; }

    /// <summary>
    /// The state just changed: record a step when the arrangement differs from the baseline.
    /// Returns whether a step was recorded.
    /// </summary>
    public bool Commit(AppState state, UndoAction action)
    {
        ArgumentNullException.ThrowIfNull(state);

        var now = LayoutShape.Capture(state);
        var changed = !now.SameLayout(_baseline);
        if (changed)
        {
            _log.Record(action, _baseline);
            Version++;
        }

        // Even when nothing moved: the new snapshot holds the current config references.
        _baseline = now;
        return changed;
    }

    /// <summary>Adopt the current arrangement without recording it: a change the system made, not the user.</summary>
    public void Rebase(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _baseline = LayoutShape.Capture(state);
    }

    /// <summary>Forget the history and start again from <paramref name="state"/> (an import, a reset).</summary>
    public void Clear(AppState state)
    {
        _log.Clear();
        Version++;
        Rebase(state);
    }

    /// <summary>[S-885] Take back the newest step. Returns what it was, or null when there is nothing to undo.</summary>
    public UndoAction? Undo(AppState state) => Step(state, undo: true);

    /// <summary>[S-885] Put back the newest undone step. Returns what it was, or null when there is nothing to redo.</summary>
    public UndoAction? Redo(AppState state) => Step(state, undo: false);

    private UndoAction? Step(AppState state, bool undo)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Drift first, so the step taken is the newest change the user can see. Recording it also
        // forgets the redo side, which is right: redo after a further change would restore over it.
        Commit(state, UndoAction.Change);

        var ok = undo
            ? _log.TryUndo(_baseline, out var step)
            : _log.TryRedo(_baseline, out step);

        if (!ok)
        {
            return null;
        }

        step.State.RestoreInto(state);
        _baseline = LayoutShape.Capture(state);
        Version++;
        return step.Action;
    }
}
