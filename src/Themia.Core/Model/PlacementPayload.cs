using Themia.Core.Geometry;
using Themia.Sdk;

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
public readonly record struct GhostOutline(FracRect Rect, WidgetId Kind);

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
    /// [S-2455] The groups the added widgets are in: for a copy, new groups with fresh ids and the
    /// same names; for a move, the same groups. Added to the target view where it lacks them.
    /// </summary>
    public IReadOnlyList<WidgetGroup> Groups { get; init; } = [];

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

    /// <summary>[S-2455] The groups the payload holds two or more members of; the snapshots name only these.</summary>
    private readonly List<WidgetGroup> _groups;

    private readonly double _newWidthPx;
    private readonly double _newHeightPx;

    /// <summary>[S-2471] The ready-made group a <see cref="PlacementMode.Create"/> payload places, or null.</summary>
    private readonly ReadyMadeGroup? _readyMade;

    private PlacementPayload(PlacementMode mode, WidgetId? kind, List<WidgetInstance> widgets, List<string> roots, double widthPx, double heightPx, List<WidgetGroup>? groups = null, ReadyMadeGroup? readyMade = null)
    {
        _readyMade = readyMade;
        Mode = mode;
        NewKind = kind;
        _widgets = widgets;
        _roots = roots;
        _groups = groups ?? [];
        _newWidthPx = widthPx;
        _newHeightPx = heightPx;
    }

    public PlacementMode Mode { get; }

    /// <summary>The kind a <see cref="PlacementMode.Create"/> payload makes; null otherwise.</summary>
    public WidgetId? NewKind { get; }

    /// <summary>[S-2471] The ready-made group a <see cref="PlacementMode.Create"/> payload places; null otherwise.</summary>
    public ReadyMadeGroup? ReadyMade => _readyMade;

    /// <summary>
    /// [S-77] The widget the pointer holds, for a drag handed to another display: its centre goes to
    /// the pointer and the rest keep their offsets. Null centres the whole block ([S-74]).
    /// </summary>
    public string? AnchorId { get; init; }

    /// <summary>The snapshots (empty for a new widget).</summary>
    public IReadOnlyList<WidgetInstance> Widgets => _widgets;

    /// <summary>[S-2455] The groups the payload carries (two or more of their members in it).</summary>
    public IReadOnlyList<WidgetGroup> Groups => _groups;

    /// <summary>The ids of the snapshots, in view order (empty for a new widget).</summary>
    public IReadOnlyList<string> RootIds => _roots;

    /// <summary>How many widgets the user picked: one for a new widget, otherwise the snapshots.</summary>
    public int Count => _readyMade is { } recipe ? recipe.Widgets.Count : Mode == PlacementMode.Create ? 1 : _roots.Count;

    /// <summary>[S-1065] How many widgets starting this session would create, for the gate before the ghost appears.</summary>
    public int Creates => Mode switch
    {
        PlacementMode.Create => _readyMade?.Widgets.Count ?? 1,
        PlacementMode.Copy => _widgets.Count,
        _ => 0,
    };

    /// <summary>The kind of the only root, for the hint ("Click to place Clock"); null for several.</summary>
    public WidgetId? SingleKind => NewKind ?? (_roots.Count == 1 ? Find(_roots[0])?.Kind : null);

    /// <summary>Whether a widget is one of the originals this payload was taken from.</summary>
    public bool Holds(string? id) => id is not null && _widgets.Exists(w => string.Equals(w.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// [S-2471] A ready-made group: every one of its widgets new, as one block at the boxes the recipe
    /// gives them, landing as one new group named after it. One placement, one undo step.
    /// </summary>
    public static PlacementPayload ForReadyMade(ReadyMadeGroup recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return new(PlacementMode.Create, null, [], [], 0, 0, readyMade: recipe);
    }

    /// <summary>[S-421] A new widget of <paramref name="kind"/> at its default size in pixels.</summary>
    public static PlacementPayload ForNew(WidgetId kind, double widthPx, double heightPx)
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

        // [S-2455] A group travels with the payload when two or more of its members do; a member taken
        // alone (an entered one) travels as a lone widget.
        var groups = roots
            .Select(id => view.Find(id)!.Group)
            .OfType<string>()
            .GroupBy(g => g, StringComparer.Ordinal)
            .Where(g => g.Count() >= 2)
            .Select(g => view.FindGroup(g.Key))
            .OfType<WidgetGroup>()
            .Select(g => g.Copy())
            .ToList();
        var kept = new HashSet<string>(groups.Select(g => g.Id), StringComparer.Ordinal);

        var widgets = roots
            .Select(id => view.Find(id)!)
            .Select(w =>
            {
                var snapshot = w.Clone();
                snapshot.Id = w.Id;
                snapshot.Group = w.Group is { } g && kept.Contains(g) ? g : null;
                return snapshot;
            })
            .ToList();

        return new PlacementPayload(mode, null, widgets, roots, 0, 0, groups)
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

        if (_readyMade is { } recipe)
        {
            var (boxes, delta) = ReadyMadeLayout(recipe, xFrac, yFrac, screen, snapEnabled);
            return [.. recipe.Widgets.Select((w, i) => new GhostOutline(BlockGeometry.Apply(boxes[i], delta, screen), new WidgetId(w.Kind)))];
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

        if (_readyMade is { } recipe)
        {
            // [S-2471] Built where the ghost was: each widget's rect is its box moved by the block's translation.
            var (_, delta) = ReadyMadeLayout(recipe, xFrac, yFrac, screen, snapEnabled);
            if (ReadyMadeGroups.Build(recipe, screen) is not var (widgets, group))
            {
                return new PastePlan([], [], []);
            }

            foreach (var widget in widgets)
            {
                widget.Rect = BlockGeometry.Apply(widget.Rect, delta, screen);
            }

            return new PastePlan(widgets, [.. widgets.Select(w => w.Id)], []) { Groups = [group] };
        }

        var (_, at) = Layout(xFrac, yFrac, screen, snapEnabled);
        var move = Mode == PlacementMode.Move;

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var w in _widgets)
        {
            ids[w.Id] = move ? w.Id : Guid.NewGuid().ToString("n");
        }

        // [S-2455] A copy of a group is a new group (fresh id, same name) every time; a move keeps it.
        var groups = move
            ? _groups.ToDictionary(g => g.Id, g => g.Copy(), StringComparer.Ordinal)
            : WidgetGroups.FreshCopies(_groups);

        var added = _widgets.Select(w =>
        {
            var copy = w.Clone();
            copy.Id = ids[w.Id];
            copy.Group = w.Group is { } g && groups.TryGetValue(g, out var landed) ? landed.Id : null;
            copy.Rect = BlockGeometry.Apply(w.Rect, at, screen);
            return copy;
        }).ToList();

        return new PastePlan(added, [.. _roots.Select(r => ids[r])], move ? [.. _roots] : []) { Groups = [.. groups.Values] };
    }

    /// <summary>The widgets the ghost draws and the block's translation: the whole payload moves by that one translation.</summary>
    private (List<WidgetInstance> Drawn, BlockGeometry.Delta At) Layout(double xFrac, double yFrac, ScreenMetrics screen, bool snapEnabled)
    {
        var rects = _widgets.Select(w => w.Rect).ToList();
        var anchor = AnchorId is not null && Find(AnchorId) is { } held ? held.Rect : BlockGeometry.Bounds(rects);
        return (_widgets, BlockGeometry.PlaceAt(rects, anchor, xFrac, yFrac, screen, snapEnabled));
    }

    /// <summary>[S-2471] A ready-made group: its boxes on this display, the block's translation for the point.</summary>
    private static (List<FracRect> Boxes, BlockGeometry.Delta At) ReadyMadeLayout(ReadyMadeGroup recipe, double xFrac, double yFrac, ScreenMetrics screen, bool snapEnabled)
    {
        var boxes = recipe.Widgets.Select(w => ReadyMadeGroups.Rect(w.Box, screen)).ToList();
        return (boxes, BlockGeometry.PlaceAt(boxes, BlockGeometry.Bounds(boxes), xFrac, yFrac, screen, snapEnabled));
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
