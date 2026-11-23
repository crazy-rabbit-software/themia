using Microsoft.UI.Dispatching;
using Themia.App.Interop;
using Themia.App.Shell;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;

namespace Themia.App.Services;

/// <summary>[S-15662] What a gesture lands for one widget: its rect, its config (null: as it stands).</summary>
internal readonly record struct Landing(string Id, FracRect? Rect = null, WidgetConfig? Config = null);

/// <summary>
/// Mutations — the single write path — and the commands built on it: edit mode, views, import,
/// and opening the settings menu for a second start ([S-66]).
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>
    /// Apply a change and persist it. Every mutation goes through here.
    /// <para>
    /// [S-884] And so does the undo history: the change is committed to it, which records a step
    /// named <paramref name="undo"/> when the arrangement of the desktop changed and nothing otherwise.
    /// [S-5520] A null <paramref name="undo"/> is a change the system made, not the user (the desktop's
    /// files changed): adopted without a step. [S-5597] <paramref name="effect"/>: what the step did outside the
    /// state (a desktop delete's files), carried by it.
    /// </para>
    /// <para>
    /// [S-889] A change that throws is taken back whole by the writer (no step, nothing saved): see <see cref="TakenBack"/>.
    /// Returns false then.
    /// </para>
    /// </summary>
    public bool Mutate(Action<AppState> change, bool relayout = true, UndoAction? undo = UndoAction.Change, IUndoEffect? effect = null)
    {
        ArgumentNullException.ThrowIfNull(change);

        // [S-15666] What the change asks of the fences is looked up afresh; what was looked up before it is forgotten after.
        _writing++;
        bool written;
        try
        {
            written = _writer.Write(
                state =>
                {
                    change(state);

                    // [S-15668] The change may show another view on a display: every surface's metrics are made again.
                    ForgetMetrics();

                    // [S-13022] [S-13023] Fences that show by a rule show it after every change, in the same step.
                    CollectFences(state);
                },
                undo,
                effect,
                SettleChange);
        }
        finally
        {
            _writing--;
            ForgetFences();
        }

        if (!written)
        {
            TakenBack();
            return false;
        }

        // [S-1583] A deleted or moved widget leaves the selection with the change that removed it.
        ChangeSelection(Selection.Prune(State));

        // [S-15660] The surfaces draw what the writes changed since they last drew (a change that asked for no relayout
        // waits in the writer for the next).
        if (relayout)
        {
            StateChanged?.Invoke(_writer.TakeChange());
        }

        return true;
    }

    /// <summary>
    /// [S-15662] The one way a gesture lands: the widgets of <paramref name="landings"/> take their rects and configs inside one
    /// change (<see cref="Mutate"/>, one undo step named <paramref name="undo"/>: a whole drag, a resize, a run of held arrows,
    /// [S-1972]), and the desktop icons among them land in a fence or out of one by where the held one lands ([S-13041], [S-16461],
    /// <see cref="DesktopFences.Land"/>) on <paramref name="surface"/>'s view; the surfaces draw what changed ([S-15660]: the
    /// moved cards laid again, a config applied again only where it changed). A card shows its gesture in its live rect alone
    /// until here; nothing writes the state outside this. A null <paramref name="surface"/> (a widget's page): the widgets
    /// wherever they are, no fence landing. [S-16461] <paramref name="held"/> is the widget the pointer held: its landing
    /// decides for every icon of the gesture whether they land in a fence (none: the first icon in reading order, the keys').
    /// </summary>
    internal bool Land(DesktopWindow? surface, IReadOnlyList<Landing> landings, UndoAction undo, string? held = null)
    {
        ArgumentNullException.ThrowIfNull(landings);
        var viewId = surface?.CurrentViewId;
        var metrics = surface?.Metrics;
        return Mutate(
            state =>
            {
                var view = viewId is null ? null : state.FindView(viewId);
                foreach (var landing in landings)
                {
                    if ((view is not null ? view.Find(landing.Id) : Find(landing.Id)?.Widget) is not { } widget)
                    {
                        continue;
                    }

                    if (landing.Rect is { } rect)
                    {
                        widget.Rect = rect;
                    }

                    if (landing.Config is { } config)
                    {
                        widget.Config = config;
                    }
                }

                if (view is not null && metrics is { } screen && view.Widgets.Exists(w => w.Config is IHoldsDesktopItems))
                {
                    DesktopFences.Land(view, [.. landings.Select(l => l.Id)], held, screen, DeskIconSize(screen));
                }
            },
            undo: undo);
    }

    /// <summary>
    /// [S-889] A change threw and the writer took it back whole. What the change put in force outside the state follows
    /// the state again (the desktop's icon size and layout, the sizings noted), the user is told, and every surface draws
    /// the state as it is: a card the change took away and the restore put back is a card again.
    /// </summary>
    private void TakenBack()
    {
        DesktopCell.Apply(State.Preferences.DesktopIconSize);
        DesktopCell.Apply(State.Preferences.DesktopIconLayout);
        _sizings.NoteAll(State);

        Notices.Error(Str.Shell_Notice_ChangeTakenBack.Text(), Str.Shell_Notice_ChangeTakenBackBody.Text());

        ChangeSelection(Selection.Prune(State));
        ForgetMetrics();

        // [S-15660] The writer took it back: drawn whole.
        StateChanged?.Invoke(_writer.TakeChange());
    }

    /// <summary>
    /// What follows every change in the same step, before its commit. True when the desktop's icons followed another
    /// icon size or layout, a setting no undo takes back: the change is adopted without a step.
    /// </summary>
    private bool SettleChange()
    {
        // [S-5593] [S-13972] Another desktop icon size or layout is in force before the sizings are read: every icon
        // refits below. The icons follow a setting, which no undo takes back: adopted without a step.
        var adopted = ApplyDesktopIconLook();

        // [S-2447] A config the change gave another sizing refits its widget in this same mutation;
        // [S-854] on a display whose metrics have not settled, once they have.
        _sizings.Refit(State, SurfacesNow());

        // [S-11892] Every card on its display's grid lines: an edge drifted off its line by less than a DIP put on it.
        SettleOnGrid();

        // [S-855] [S-6608] Every widget in reach of the area every display showing its view holds, and what a change of that
        // area (a view shown on another display, a taskbar moved or grown) would hide behind a taskbar moved clear, in the
        // same step ([S-854]: on settled metrics only); a widget the user hung over a taskbar stays there.
        SettleOnWorkAreas();

        // [S-13040] [S-13042] Every fence laid on its display's icon grid, its icons on its cells, in the same step.
        try
        {
            LayFences(State);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "laying the fences threw; their icons stay where they were", ex);
        }

        return adopted;
    }

    /// <summary>The sizing each widget had at the last mutation, by instance: what a config change is compared with ([S-2447]).</summary>
    private readonly SizingNotes _sizings = new();

    /// <summary>[S-11892] The cards of every shown view whose edges drifted off their display's grid lines put on them (<see cref="GridSettle"/>), loudly.</summary>
    private void SettleOnGrid()
    {
        if (GridSettle.Apply(State, SurfacesNow()) is { Count: > 0 } notes)
        {
            Log.Warn(Scope, $"{notes.Count} widget(s) off their display's grid lines by less than a DIP, put on them: {string.Join("; ", notes)}");
        }
    }

    /// <summary>[S-855] The widgets of every shown view settled on the area the displays showing it hold (<see cref="WorkAreaSettle"/>).</summary>
    private void SettleOnWorkAreas()
    {
        if (WorkAreaSettle.Apply(State, SurfacesNow()) is var moved and > 0)
        {
            Log.Info(Scope, $"{moved} widget(s) moved in reach, or clear of a taskbar a change of the work area would hide them behind");
        }
    }

    /// <summary>
    /// [S-15668] Every surface's kept metrics forgotten: what they are made of (which view each display shows, the displays
    /// and their work areas) may have changed.
    /// </summary>
    private void ForgetMetrics()
    {
        foreach (var window in _windows)
        {
            window.ForgetMetrics();
        }
    }

    /// <summary>
    /// [S-855] The area new geometry of <paramref name="viewId"/> may use: the part of the screen every live display
    /// showing it holds in its work area (<paramref name="own"/>, the asking display's, among them), so what is placed,
    /// moved or resized on one display is never under another's taskbar.
    /// </summary>
    internal FracRect SharedAllowed(string viewId, FracRect own)
        => GridGeometry.SharedAllowed(_windows
            .Where(w => w.IsAlive && string.Equals(w.CurrentViewId, viewId, StringComparison.Ordinal))
            .Select(w => w.OwnAllowed)
            .Append(own));

    /// <summary>
    /// [S-854] The live surfaces, the primary first, as what writes geometry sees them: each with whether
    /// its metrics have settled.
    /// </summary>
    private List<SurfaceMetrics> SurfacesNow()
        => [.. _windows.Where(w => w.IsAlive).OrderByDescending(w => w.Monitor.IsPrimary).Select(w => w.Surface)];

    /// <summary>
    /// [S-854] A surface's metrics settled (its first layout at the real scale, or a display pass after a
    /// scale change): once every live surface has, what waited for real metrics runs on them: the refits
    /// a config change left waiting ([S-2447]), a read of the desktop that came in before ([S-5520]), and
    /// the desktop settled across the displays and onto the grid ([S-5560], [S-5589]), and a card that drifted off its
    /// display's grid lines put on them ([S-11892]).
    /// </summary>
    internal void SettleSurfaces()
    {
        if (_disposed || !SurfaceMetrics.AllSettled(SurfacesNow()))
        {
            return;
        }

        // [S-13040] A fence is laid on its display's icon grid at the real metrics (a start, another resolution or scale);
        // [S-855] a widget a change of the work area would hide behind a taskbar moves clear of it; [S-11892] a card off its
        // display's grid lines by less than a DIP is put on them.
        if (_sizings.Waits(State) || State.Views.Exists(v => v.Widgets.Exists(w => w.Config is IHoldsDesktopItems))
            || WorkAreaSettle.Waits(State, SurfacesNow()) || GridSettle.Waits(State, SurfacesNow()))
        {
            Mutate(_ => { }, undo: null);
        }

        if (_listingWaits is { } renames)
        {
            _listingWaits = null;
            OnDesktopListed(Files.HostDesktopItems.Instance.Items, renames);
            return;
        }

        SettleDesktopDisplays();
    }

    /// <summary>
    /// [S-41] Save a change made in place outside <see cref="Mutate"/> (a widget's own config, an alarm that switched
    /// itself off), without re-running layout: the card already shows it.
    /// </summary>
    public void SaveSoon() => _writer.SaveSoon();

    public void SetEditMode(bool enabled)
    {
        if (_editMode == enabled)
        {
            return;
        }

        _editMode = enabled;

        // [S-11908] A card being arranged lights nothing in the others.
        if (AppServices.Links is { } links)
        {
            links.Quiet = enabled;
        }

        // A ghost has nowhere to land outside edit mode: the surface stops taking clicks.
        if (!enabled)
        {
            CancelPlacement();
        }

        EditModeChanged?.Invoke(enabled);

        if (!enabled)
        {
            _writer.SaveSoon();
        }

        Log.Info(Scope, enabled ? "entered edit mode" : "left edit mode");
    }

    /// <summary>
    /// [S-1998] The add menu from the keyboard (Ctrl+Alt+Shift+W anywhere, Ctrl+N in edit mode): edit
    /// mode on, a placement in progress dropped, and the menu opened on the display under the
    /// pointer, as a paste lands there ([S-74]). One path for both keys.
    /// </summary>
    public void OpenAddMenu()
    {
        if ((SurfaceUnderCursor() ?? _windows.FirstOrDefault()) is not { } surface)
        {
            Log.Warn(Scope, "no display to open the add menu on");
            return;
        }

        SetEditMode(true);

        if (Placing is not null)
        {
            CancelPlacement();
        }

        surface.OpenAddMenuFromKeys();
    }

    /// <summary>
    /// [S-66] Themia started again by hand: edit mode on (it stays on when it was), a placement in
    /// progress dropped, and the settings menu open at its first level on the main display (the
    /// primary monitor's), the keyboard in it. Never for a start at sign-in (<see cref="Core.Shell.SecondStart"/>).
    /// </summary>
    public void OpenSettingsOnStart()
    {
        if ((_windows.Find(w => w.Monitor.IsPrimary) ?? _windows.FirstOrDefault()) is not { } surface)
        {
            Log.Warn(Scope, "no display to open the settings menu on");
            return;
        }

        SetEditMode(true);

        if (Placing is not null)
        {
            CancelPlacement();
        }

        surface.OpenSettingsOnStart();
    }

    /// <summary>[S-19] Show a view on one display. See <see cref="AppState.ShowViewOn"/>.</summary>
    public void ShowView(string monitorId, string viewId)
        => Mutate(state => state.ShowViewOn(monitorId, viewId));

    /// <summary>Where the state file lives, for settings › Back up &amp; reset to show and open.</summary>
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
    public string? ImportState(AppState incoming, out string backup) => ReplaceState(incoming, SettingsTransfer.ImportOccasion, out backup);

    /// <summary>
    /// Replace the whole state, keeping the current one beside the state file under
    /// <paramref name="occasion"/>'s name first (<paramref name="backup"/>): an import ([S-497]) or a reset ([S-892]). The undo
    /// history starts again: there is no step from one whole profile to another.
    /// </summary>
    private string? ReplaceState(AppState incoming, string occasion, out string backup)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        backup = SettingsTransfer.BackupPath(_store.Path, DateTime.Now, occasion);
        if (SettingsTransfer.WriteFile(State, backup) is { } problem)
        {
            Log.Error(Scope, $"{occasion} cancelled: could not keep the current settings at {backup}");
            return Str.Shell_Notice_BackupFailed.Format(("reason", problem));
        }

        Select(null);
        Clipboard.Clear();

        // [S-5593] [S-13970] The incoming views were laid out at the incoming icon size and layout: they are put in force
        // as they are, so the mutation below fits nothing from the old box to theirs.
        DesktopCell.Apply(incoming.Preferences.DesktopIconSize);
        DesktopCell.Apply(incoming.Preferences.DesktopIconLayout);

        var replaced = Mutate(state =>
        {
            SettingsTransfer.Apply(state, incoming);

            // Displays named in the file that are not connected keep their assignment ([S-18]);
            // the ones that are connected and unnamed get an existing view, as at startup ([S-1705]).
            DisplayPlan.AssignViews(state, DisplayViewKeys());

            // [S-498] [S-5522] The desktop's memory came with the views: the desktop as it is now is read against it in
            // the same step (a reset's empty known set lays every icon on the fresh views, as a new state does).
            ReconcileDesktopAfterStep();
        });

        // [S-889] The replacement threw and was taken back: the settings are the ones there were.
        if (!replaced)
        {
            Log.Error(Scope, $"{occasion} failed and was taken back; the settings are as they were");
            return Str.Shell_Notice_ChangeTakenBackBody.Text();
        }

        // The imported file carries its own per-display switches ([S-845]).
        ReconcileDisplays();
        _history?.Clear(State);
        Log.Info(Scope, $"replaced the settings ({occasion}); the previous ones are at {backup}");
        return null;
    }

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
    /// [S-3541] A new, empty view at the end of the order, named "View n", shown on the display whose
    /// View menu asked for it. Within the licence ([S-1065]); a refusal says why in a notice and
    /// returns null.
    /// </summary>
    public string? AddView(string monitorId)
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
            state.ShowViewOn(monitorId, created.Id);
        }, undo: UndoAction.AddView);

        return id;
    }

    /// <summary>[S-901] Move a view into another's place in the order.</summary>
    public void ReorderView(string draggedId, string targetId)
        => Mutate(state => ViewOps.Reorder(state.Views, draggedId, targetId), undo: UndoAction.ReorderView);
}
