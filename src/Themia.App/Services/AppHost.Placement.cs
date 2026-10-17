using Themia.App.Interop;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.App.Services;

/// <summary>
/// [S-1587] The placement session: one or many widgets following the pointer across every display
/// until a click drops them. Everything that produces or moves widgets goes through it — Add widget,
/// paste, Duplicate, Move — so none of them drops a widget at a spot the user did not point at.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>What is being placed, while the user is choosing where it goes; otherwise null.</summary>
    public PlacementPayload? Placing { get; private set; }

    /// <summary>Raised when placement starts, ends or is cancelled.</summary>
    public event Action? PlacementChanged;

    /// <summary>[S-1594] Escape while placing, wherever the keyboard is. Created on the first placement, disposed with the host.</summary>
    private PlacementKeyWatcher? _placementKeys;

    /// <summary>[S-421] Start placing a new widget of <paramref name="kind"/> at its default size.</summary>
    public void BeginPlacement(WidgetId kind)
    {
        var definition = ModuleCatalog.Of(kind);
        BeginPlacement(PlacementPayload.ForNew(kind, definition.DefaultWidth, definition.DefaultHeight));
    }

    /// <summary>
    /// [S-421]/[S-1587] Start placing. Nothing is added, removed or moved yet: a ghost of the payload
    /// follows the pointer across every display and the change happens only once the user clicks.
    /// <para>
    /// The settings window is deliberately not opened. Adding a widget and configuring it are two
    /// decisions, and a window appearing over the very spot the user was about to click is the
    /// fastest way to lose the first one.
    /// </para>
    /// </summary>
    public void BeginPlacement(PlacementPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        // [S-1065] Refused before the ghost appears, not after the user has aimed it.
        if (!AllowCreate(payload.Creates))
        {
            return;
        }

        SetEditMode(true);
        Select(null);
        Placing = payload;
        PlacementKeys()?.Arm();
        PlacementChanged?.Invoke();
        Log.Info(Scope, $"placing: {payload.Mode} of {payload.Count} widget(s)");
    }

    /// <summary>
    /// Abandon placement without changing anything — a cut stays on the clipboard, a moved widget
    /// stays where it was ([S-1590]). Safe to call when nothing is being placed.
    /// </summary>
    public void CancelPlacement()
    {
        if (Placing is null)
        {
            return;
        }

        Placing = null;
        _placementKeys?.Disarm();
        PlacementChanged?.Invoke();
    }

    /// <summary>
    /// [S-422]/[S-1586] Land the payload on the display it was dropped on, in that display's view, as
    /// its ghost showed it with the pointer at (<paramref name="xFrac"/>, <paramref name="yFrac"/>).
    /// Returns whether anything landed.
    /// </summary>
    public bool CompletePlacement(DesktopWindow surface, double xFrac, double yFrac, bool snap)
    {
        ArgumentNullException.ThrowIfNull(surface);

        if (Placing is not { } payload)
        {
            return false;
        }

        var plan = payload.Plan(xFrac, yFrac, surface.Metrics, snap);

        // Placement ends before the mutation lands, so the relayout it triggers already sees the
        // surface back in its ordinary edit state rather than drawing a ghost over the new cards.
        Placing = null;
        _placementKeys?.Disarm();
        PlacementChanged?.Invoke();

        if (!Land(plan, surface.CurrentViewId, UndoActions.ForPlacement(payload.Mode)))
        {
            return false;
        }

        Clipboard.OnPlaced(payload);
        Log.Info(Scope, $"placed: {payload.Mode} of {payload.Count} widget(s) on {surface.Monitor.GdiName}");
        return true;
    }

    /// <summary>
    /// Apply a plan to a view in one mutation and select what landed. Shared by a placement click and
    /// a drag handed to another display ([S-1591]), so both land by exactly the same rules.
    /// </summary>
    public bool Land(PastePlan plan, string viewId, UndoAction undo = UndoAction.Move)
    {
        ArgumentNullException.ThrowIfNull(plan);

        // [S-1065] Checked again on landing: a paste on another display may have used the room, and
        // a move whose originals were deleted meanwhile adds them back. All or nothing.
        if (!AllowPaste(plan))
        {
            return false;
        }

        Mutate(state => state.ApplyPlacement(plan, viewId), undo: undo);

        var landedIn = State.FindView(viewId)?.Id ?? State.ActiveView().Id;
        SelectMany(landedIn, plan.Roots);
        return true;
    }

    private PlacementKeyWatcher? PlacementKeys()
    {
        if (_placementKeys is null && !_disposed)
        {
            try
            {
                _placementKeys = new PlacementKeyWatcher(_dispatcher, CancelPlacement);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "could not start watching for Escape; a placement cancels by a right-click, or Escape on a surface that has the keyboard", ex);
            }
        }

        return _placementKeys;
    }

    private void StopPlacementKeys()
    {
        _placementKeys?.Dispose();
        _placementKeys = null;
    }
}
