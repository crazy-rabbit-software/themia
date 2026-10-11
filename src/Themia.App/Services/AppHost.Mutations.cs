using Microsoft.UI.Dispatching;
using Themia.App.Interop;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;

namespace Themia.App.Services;

/// <summary>
/// Mutations — the single write path — and the commands built on it: edit mode, views, import,
/// and opening the settings window.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>
    /// Apply a change and persist it. Every mutation goes through here.
    /// <para>
    /// [S-884] And so does the undo history: the change is committed to it, which records a step
    /// named <paramref name="undo"/> when the arrangement of the desktop changed and nothing otherwise.
    /// </para>
    /// </summary>
    public void Mutate(Action<AppState> change, bool relayout = true, UndoAction undo = UndoAction.Change)
    {
        ArgumentNullException.ThrowIfNull(change);

        try
        {
            change(State);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a state mutation threw; state may be partially applied", ex);
        }

        _history?.Commit(State, undo);
        _store.SaveSoon(State);

        // [S-1583] A deleted or moved widget leaves the selection with the change that removed it.
        ChangeSelection(Selection.Prune(State));

        if (relayout)
        {
            StateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Persist geometry mid-drag without re-running layout: the dragged card is already following
    /// the pointer, so raising <see cref="StateChanged"/> here would fight it.
    /// </summary>
    public void SaveGeometryOnly() => _store.SaveSoon(State);

    public void SetEditMode(bool enabled)
    {
        if (_editMode == enabled)
        {
            return;
        }

        _editMode = enabled;

        // A ghost has nowhere to land outside edit mode: the surface stops taking clicks.
        if (!enabled)
        {
            CancelPlacement();
        }

        EditModeChanged?.Invoke(enabled);

        if (!enabled)
        {
            _store.SaveSoon(State);
        }

        Log.Info(Scope, enabled ? "entered edit mode" : "left edit mode");
    }

    /// <summary>[S-19] Show a view on one display. See <see cref="AppState.ShowViewOn"/>.</summary>
    public void ShowView(string monitorId, string viewId)
        => Mutate(state => state.ShowViewOn(monitorId, viewId));

    /// <summary>Where the state file lives, for the About page to show and open.</summary>
    public string StatePath => _store.Path;

    /// <summary>
    /// [S-497] Replace the whole state with an imported one, keeping what is there now beside the
    /// state file first. Returns null on success, otherwise why nothing was changed.
    /// <para>
    /// The backup is the in-memory state, not a copy of the file: a debounced save may not have
    /// landed yet, and "what you have now" is what is on screen. If the backup cannot be written the
    /// import does not happen — replacing the user's layout with nothing to go back to is exactly the
    /// loss the backup exists to prevent.
    /// </para>
    /// </summary>
    public string? ImportState(AppState incoming) => ReplaceState(incoming, SettingsTransfer.ImportOccasion);

    /// <summary>
    /// Replace the whole state, keeping the current one beside the state file under
    /// <paramref name="occasion"/>'s name first: an import ([S-497]) or a reset ([S-892]). The undo
    /// history starts again: there is no step from one whole profile to another.
    /// </summary>
    private string? ReplaceState(AppState incoming, string occasion)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        var backup = SettingsTransfer.BackupPath(_store.Path, DateTime.Now, occasion);
        if (SettingsTransfer.WriteFile(State, backup) is { } problem)
        {
            Log.Error(Scope, $"{occasion} cancelled: could not keep the current settings at {backup}");
            return Str.Shell_Notice_BackupFailed.Format(("reason", problem));
        }

        Select(null);
        Clipboard.Clear();

        Mutate(state =>
        {
            SettingsTransfer.Apply(state, incoming);

            // Displays named in the file that are not connected keep their assignment ([S-18]);
            // the ones that are connected and unnamed get an existing view, as at startup ([S-1705]).
            DisplayPlan.AssignViews(state, DisplayViewKeys());
        });

        // The imported file carries its own per-display switches ([S-845]).
        ReconcileDisplays();
        _history?.Clear(State);

        Log.Info(Scope, $"replaced the settings ({occasion}); the previous ones are at {backup}");
        return null;
    }

    /// <summary>
    /// Open the settings window on a particular page, reusing the one window if it is already up.
    /// A second settings window would let the same widget be edited in two places at once.
    /// </summary>
    public void ShowConfig(ConfigTarget target)
    {
        if (!HasSettings(target))
        {
            return;
        }

        if (_config is null)
        {
            _config = new ConfigWindow(this);
            _config.Closed += () => _config = null;
            _config.Activate();
        }

        _config.Navigate(target);
    }

    public void ShowSettings() => ShowConfig(new ConfigTarget.App());

    /// <summary>[S-1745] Whether the settings window is open.</summary>
    public bool ConfigOpen => _config is not null;

    /// <summary>
    /// [S-1745] Show <paramref name="target"/> in the settings window if it is open, without bringing
    /// it to the front; when it is closed nothing happens (a click never opens it).
    /// </summary>
    public void FollowConfig(ConfigTarget target)
    {
        if (HasSettings(target))
        {
            _config?.Follow(target);
        }
    }

    /// <summary>
    /// [S-1750] Whether <paramref name="target"/> has a page worth opening: a widget whose page would
    /// be empty has none, so every way in (Enter, a click while the window is open, any caller)
    /// leaves the window as it is. Everything else, a vanished widget included, opens as before.
    /// </summary>
    public bool HasSettings(ConfigTarget target)
        => target is not ConfigTarget.Widget { WidgetId: var id }
            || Find(id) is not { Widget: var widget }
            || ConfigPages.HasWidgetSettings(widget.Kind);

    /// <summary>Delete a view and everything in it, repairing anything that pointed at it.</summary>
    public void DeleteView(string viewId)
    {
        if (State.Views.Count <= 1)
        {
            return; // the last view cannot go: a desktop with no view has nowhere to put a widget
        }

        // [S-902] One change: the view goes, and every reference to it is repaired with it. A display
        // left without an assignment is given an existing view again ([S-1705]) rather than
        // following whatever view happens to be active from then on.
        var name = State.FindView(viewId)?.Name;
        Mutate(state =>
        {
            if (ViewOps.Delete(state, viewId))
            {
                DisplayPlan.AssignViews(state, DisplayViewKeys());
            }
        }, undo: UndoAction.DeleteView);

        // [S-881] No confirmation: the notice offers the way back.
        if (name is not null && State.FindView(viewId) is null)
        {
            OfferUndo(Str.Shell_Undo_DeletedView.Format(("name", name)));
        }
    }

    /// <summary>
    /// [S-900] Duplicate a view and show the copy on <paramref name="monitorId"/> (when given), as
    /// the original did. Within the licence: the copy is one view and all of its widgets.
    /// </summary>
    public void DuplicateView(string viewId, string? monitorId)
    {
        if (State.FindView(viewId) is not { } source || !AllowCreate(source.Widgets.Count, 1))
        {
            return;
        }

        Mutate(state =>
        {
            if (ViewOps.Duplicate(state, viewId) is { } copy && monitorId is not null)
            {
                state.ShowViewOn(monitorId, copy.Id);
            }
        }, undo: UndoAction.DuplicateView);
    }

    /// <summary>
    /// A new, empty view at the end of the order, named "View n" — from the edit toolbar's View menu
    /// (shown on that display) and the Views page's Add view ([S-1832], shown nowhere yet: a new
    /// empty view would blank the screen behind the settings window). Within the licence ([S-1065]);
    /// a refusal says why in a notice and returns null.
    /// </summary>
    public string? AddView(string? monitorId)
    {
        if (!AllowCreate(0, 1))
        {
            return null;
        }

        string? id = null;
        Mutate(state =>
        {
            var created = ViewState.Create(Str.App_ViewN.Format(("n", state.Views.Count + 1)));
            state.Views.Add(created);
            id = created.Id;

            if (monitorId is not null)
            {
                state.ShowViewOn(monitorId, created.Id);
            }
        }, undo: UndoAction.AddView);

        return id;
    }

    /// <summary>[S-901] Move a view into another's place in the order.</summary>
    public void ReorderView(string draggedId, string targetId)
        => Mutate(state => ViewOps.Reorder(state.Views, draggedId, targetId), undo: UndoAction.ReorderView);

    /// <summary>[S-901] Move a view one place up (-1) or down (+1).</summary>
    public void MoveView(string viewId, int delta)
        => Mutate(state => ViewOps.MoveBy(state.Views, viewId, delta), undo: UndoAction.ReorderView);
}
