using Themia.Core.Geometry;

namespace Themia.Core.Model;

/// <summary>What a placement does when it lands.</summary>
public enum PlacementMode
{
    /// <summary>A new widget of one kind, from the picker ([S-421]).</summary>
    Create = 0,

    /// <summary>Fresh copies with fresh ids: paste of a copy, Duplicate ([S-1589]).</summary>
    Copy = 1,

    /// <summary>The same widgets, same ids, somewhere else: paste of a cut, Move ([S-1590]).</summary>
    Move = 2,
}

/// <summary>One outline of a placement ghost: where a widget will be drawn, and what it is.</summary>
public readonly record struct GhostOutline(FracRect Rect, WidgetKind Kind);

/// <summary>
/// What a placement landing does, worked out before anything is mutated ([S-75]).
/// </summary>
/// <param name="Added">
/// The widgets to put into the target view, in order. For a copy, fresh instances with fresh ids;
/// for a move, snapshots carrying the originals' ids — <see cref="WidgetTransfer.ApplyPlacement"/>
/// moves the live original in their place when it still exists.
/// </param>
/// <param name="Roots">The ids of the placed widgets: what is selected afterwards.</param>
/// <param name="RemoveIds">
/// For a move, the originals to take out of whatever view they are in; empty for a copy or a new widget.
/// </param>
public sealed record PastePlan(
    IReadOnlyList<WidgetInstance> Added,
    IReadOnlyList<string> Roots,
    IReadOnlyList<string> RemoveIds)
{
    /// <summary>
    /// [S-1065] How many widgets the plan adds on balance: what it adds, less what it only moves
    /// (an added id that is one of the originals it removes). A move therefore adds nothing; a move
    /// whose originals were deleted meanwhile puts them back, and counts.
    /// </summary>
    public int NetAdded(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var removed = WidgetTransfer.RemovalBlock(state, RemoveIds);
        return Added.Count(w => !removed.Contains(w.Id));
    }
}

/// <summary>
/// [S-1587]..[S-1590] The one thing a placement session carries: a new widget of one kind, or a
/// snapshot of one or many existing widgets with their configs and their layout relative to each
/// other. Every command that produces or moves widgets — Add widget, paste, Duplicate, Move, a drag
/// handed to another display — goes through it, so each of them shows the same ghost and lands by
/// the same rules.
/// <para>
/// The snapshot is taken when the session starts (or when a copy is taken, [S-72]), so the ghost
/// shows what was picked even if the originals change while it is being aimed. A move lands the
/// live originals, not the snapshot, whenever they still exist.
/// </para>
/// </summary>
public sealed class PlacementPayload
{
    /// <summary>Snapshots with their original ids, in view order.</summary>
    private readonly List<WidgetInstance> _widgets;

    private readonly List<string> _roots;

    private readonly double _newWidthPx;
    private readonly double _newHeightPx;

    private PlacementPayload(PlacementMode mode, WidgetKind? kind, List<WidgetInstance> widgets, List<string> roots, double widthPx, double heightPx)
    {
        Mode = mode;
        NewKind = kind;
        _widgets = widgets;
        _roots = roots;
        _newWidthPx = widthPx;
        _newHeightPx = heightPx;
    }

    public PlacementMode Mode { get; }

    /// <summary>The kind a <see cref="PlacementMode.Create"/> payload makes; null otherwise.</summary>
    public WidgetKind? NewKind { get; }

    /// <summary>
    /// [S-77] The widget the pointer holds, for a drag handed to another display: its centre goes to
    /// the pointer and the rest keep their offsets. Null centres the whole block ([S-74]).
    /// </summary>
    public string? AnchorId { get; init; }

    /// <summary>The snapshots (empty for a new widget).</summary>
    public IReadOnlyList<WidgetInstance> Widgets => _widgets;

    /// <summary>The ids of the snapshots, in view order (empty for a new widget).</summary>
    public IReadOnlyList<string> RootIds => _roots;

    /// <summary>How many widgets the user picked: one for a new widget, otherwise the snapshots.</summary>
    public int Count => Mode == PlacementMode.Create ? 1 : _roots.Count;

    /// <summary>[S-1065] How many widgets starting this session would create, for the gate before the ghost appears.</summary>
    public int Creates => Mode switch
    {
        PlacementMode.Create => 1,
        PlacementMode.Copy => _widgets.Count,
        _ => 0,
    };

    /// <summary>The kind of the only root, for the hint ("Click to place Clock"); null for several.</summary>
    public WidgetKind? SingleKind => NewKind ?? (_roots.Count == 1 ? Find(_roots[0])?.Kind : null);

    /// <summary>Whether a widget is one of the originals this payload was taken from.</summary>
    public bool Holds(string? id) => id is not null && _widgets.Exists(w => string.Equals(w.Id, id, StringComparison.Ordinal));

    /// <summary>[S-421] A new widget of <paramref name="kind"/> at its default size in pixels.</summary>
    public static PlacementPayload ForNew(WidgetKind kind, double widthPx, double heightPx)
        => new(PlacementMode.Create, kind, [], [], widthPx, heightPx);

    /// <summary>
    /// [S-1588] A snapshot of the picked widgets of one view, in view order, with their original ids.
    /// Null when none of the ids is in the view.
    /// </summary>
    public static PlacementPayload? FromView(ViewState view, IEnumerable<string> ids, PlacementMode mode, string? anchorId = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(ids);

        if (mode == PlacementMode.Create)
        {
            throw new ArgumentException("a payload taken from a view copies or moves; it does not create", nameof(mode));
        }

        var roots = SelectionRules.InView(view, ids);
        if (roots.Count == 0)
        {
            return null;
        }

        var widgets = roots
            .Select(id => view.Find(id)!)
            .Select(w =>
            {
                var snapshot = w.Clone();
                snapshot.Id = w.Id;
                return snapshot;
            })
            .ToList();

        return new PlacementPayload(mode, null, widgets, roots, 0, 0)
        {
            AnchorId = anchorId is not null && roots.Contains(anchorId) ? anchorId : null,
        };
    }

    /// <summary>[S-423]/[S-1585] What the ghost draws with the pointer at a point of a display.</summary>
    public IReadOnlyList<GhostOutline> Outlines(double xFrac, double yFrac, ScreenMetrics screen, bool snapEnabled)
    {
        if (NewKind is { } kind)
        {
            var (rect, delta) = NewLayout(xFrac, yFrac, screen, snapEnabled);
            return [new GhostOutline(BlockGeometry.Apply(rect, delta, screen), kind)];
        }

        var (drawn, at) = Layout(xFrac, yFrac, screen, snapEnabled);
        return [.. drawn.Select(w => new GhostOutline(BlockGeometry.Apply(w.Rect, at, screen), w.Kind))];
    }

    /// <summary>
    /// [S-422]/[S-1586] What landing with the pointer at a point of a display would do. Planned from
    /// the same layout as <see cref="Outlines"/>, so the widgets land exactly where the ghost was.
    /// A copy gets fresh ids every time, so one copy can be placed again and again.
    /// </summary>
    public PastePlan Plan(double xFrac, double yFrac, ScreenMetrics screen, bool snapEnabled)
    {
        if (NewKind is { } kind)
        {
            var (rect, delta) = NewLayout(xFrac, yFrac, screen, snapEnabled);
            var widget = WidgetInstance.Create(kind, BlockGeometry.Apply(rect, delta, screen));
            return new PastePlan([widget], [widget.Id], []);
        }

        var (_, at) = Layout(xFrac, yFrac, screen, snapEnabled);
        var move = Mode == PlacementMode.Move;

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var w in _widgets)
        {
            ids[w.Id] = move ? w.Id : Guid.NewGuid().ToString("n");
        }

        var added = _widgets.Select(w =>
        {
            var copy = w.Clone();
            copy.Id = ids[w.Id];
            copy.Rect = BlockGeometry.Apply(w.Rect, at, screen);
            return copy;
        }).ToList();

        return new PastePlan(added, [.. _roots.Select(r => ids[r])], move ? [.. _roots] : []);
    }

    /// <summary>The widgets the ghost draws and the block's translation: the whole payload moves by that one translation.</summary>
    private (List<WidgetInstance> Drawn, BlockGeometry.Delta At) Layout(double xFrac, double yFrac, ScreenMetrics screen, bool snapEnabled)
    {
        var rects = _widgets.Select(w => w.Rect).ToList();
        var anchor = AnchorId is not null && Find(AnchorId) is { } held ? held.Rect : BlockGeometry.Bounds(rects);
        return (_widgets, BlockGeometry.PlaceAt(rects, anchor, xFrac, yFrac, screen, snapEnabled));
    }

    /// <summary>[S-11] A new widget: its default size on this display (snapped when the aid is on), centred on the point.</summary>
    private (FracRect Rect, BlockGeometry.Delta At) NewLayout(double xFrac, double yFrac, ScreenMetrics screen, bool snapEnabled)
    {
        var rect = FracRect.FromPixels(0, 0, _newWidthPx, _newHeightPx, screen);
        if (snapEnabled)
        {
            var snapped = GridGeometry.Snap(rect, screen);
            rect = rect with { W = snapped.W, H = snapped.H };
        }

        return (rect, BlockGeometry.PlaceAt([rect], rect, xFrac, yFrac, screen, snapEnabled));
    }

    private WidgetInstance? Find(string? id)
        => id is null ? null : _widgets.Find(w => string.Equals(w.Id, id, StringComparison.Ordinal));
}
