using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;

namespace Themia.App.Services;

/// <summary>
/// Notices, undo and the start-up guard: the host's side of [S-880]..[S-893].
/// <list type="bullet">
/// <item>[S-880] Notices go to the display under the pointer (the one the user is looking at).</item>
/// <item>[S-884] The undo history sits beside <see cref="Mutate"/>, the single write path: every
/// mutation is committed to it, and so is the end of a drag or resize, which writes geometry in place.</item>
/// <item>[S-890] The crash-loop guard counts the start before the surfaces are built and clears the
/// count once they have laid out; a start after repeated failures pauses every widget.</item>
/// <item>[S-892] Reset all settings: the current ones kept beside the state file, then the first-run desktop.</item>
/// </list>
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>Notices that offer to undo replace one another, and the "Undid …" that follows them.</summary>
    private const string UndoNoticeKey = "undo";

    private LayoutHistory? _history;
    private BootGuard? _bootGuard;

    /// <summary>[S-890] This start is a safe one: widgets are paused until the user starts them.</summary>
    public bool SafeMode { get; private set; }

    // -----------------------------------------------------------------------------------------
    // Start-up
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-890] Count this start and connect the notice pipeline; before any surface exists.</summary>
    private void BeginStartGuard()
    {
        Notices.Connect(_dispatcher, RouteNotice);

        _bootGuard = new BootGuard(Path.Combine(Path.GetDirectoryName(_store.Path) ?? string.Empty, BootGuard.FileName));
        SafeMode = _bootGuard.Begin();
        WidgetCard.StartPaused = SafeMode;
    }

    /// <summary>
    /// [S-884]/[S-890] The surfaces exist: the undo history starts from the desktop as built, and
    /// the start counts as good once every surface has laid its cards out — or, in safe mode, says
    /// why nothing is running.
    /// </summary>
    private void AfterSurfacesBuilt()
    {
        _history = new LayoutHistory(State);

        if (SafeMode)
        {
            Notices.Show(new Notice(NoticeKind.Warning, Str.Shell_Notice_PausedTitle.Text())
            {
                Message = Str.Shell_Notice_PausedBody.Text(),
                Action = new NoticeAction(Str.Shell_Notice_StartWidgets.Text(), StartPausedWidgets),
                Sticky = true,
            });
            return;
        }

        WhenSurfacesLaidOut(() => _bootGuard?.MarkStable());
    }

    /// <summary>[S-890] Run <paramref name="then"/> once every surface has loaded and laid out its cards.</summary>
    private void WhenSurfacesLaidOut(Action then)
    {
        var waiting = _windows.Count;
        if (waiting == 0)
        {
            then();
            return;
        }

        foreach (var window in _windows)
        {
            window.WhenLaidOut(() =>
            {
                if (--waiting == 0)
                {
                    then();
                }
            });
        }
    }

    /// <summary>[S-890] The user asked: start every paused widget, and count the start as good if they come up.</summary>
    private void StartPausedWidgets()
    {
        WidgetCard.StartPaused = false;
        foreach (var window in _windows)
        {
            window.StartPausedCards();
        }

        // Attach runs synchronously in StartPausedCards: reaching here means none of them took the
        // process down, so the next start is an ordinary one.
        _bootGuard?.MarkStable();
        SafeMode = false;
        Log.Info(Scope, "safe mode ended: widgets started");
    }

    // -----------------------------------------------------------------------------------------
    // Notices
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-880] The display the user is looking at: the one under the pointer, else the first one up.</summary>
    private void RouteNotice(Notice notice)
    {
        var surface = SurfaceUnderCursor() is { IsAlive: true } under ? under : _windows.FirstOrDefault(w => w.IsAlive);
        surface?.ShowNotice(notice);
    }

    // -----------------------------------------------------------------------------------------
    // Undo
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-884] A drag or resize ended: its geometry is already on the model. Record it and save.</summary>
    public void CommitGeometry(UndoAction action)
    {
        _history?.Commit(State, action);
        _store.SaveSoon(State);
    }

    /// <summary>[S-887] The system changed the layout (a view made for a new display): adopt it, do not offer it as an undo.</summary>
    private void AdoptLayout() => _history?.Rebase(State);

    /// <summary>[S-885] Ctrl+Z.</summary>
    public void Undo() => Step(undo: true);

    /// <summary>[S-885] Ctrl+Y or Ctrl+Shift+Z.</summary>
    public void Redo() => Step(undo: false);

    private void Step(bool undo)
    {
        if (_history is null)
        {
            return;
        }

        // A ghost on screen is the step in progress: backing out of it is what Ctrl+Z means then.
        if (undo && Placing is not null)
        {
            CancelPlacement();
            return;
        }

        var action = undo ? _history.Undo(State) : _history.Redo(State);
        if (action is not { } done)
        {
            Notices.Show(new Notice(NoticeKind.Info, undo ? Str.Shell_Undo_NothingToUndo.Text() : Str.Shell_Undo_NothingToRedo.Text()) { Key = UndoNoticeKey });
            return;
        }

        // A view taken away by the undo leaves its display without one: give it an existing one, as
        // at startup ([S-1705]), without making that an undo step of its own.
        DisplayPlan.AssignViews(State, DisplayViewKeys());
        _history.Rebase(State);

        _store.SaveSoon(State);
        ChangeSelection(Selection.Prune(State));
        StateChanged?.Invoke();

        var version = _history.Version;
        Notices.Show(new Notice(NoticeKind.Info, undo ? UndoText.Undid(done) : UndoText.Redid(done))
        {
            Key = UndoNoticeKey,
            Action = new NoticeAction(undo ? Str.Shell_Undo_Redo.Text() : Str.Shell_Undo_Undo.Text(), () => StepIfCurrent(version, undo: !undo)),
        });
    }

    /// <summary>
    /// [S-881] Tell the user what a destructive command did and offer to take it back. The offer is
    /// good only while nothing else has changed the layout ([S-885] <see cref="LayoutHistory.Version"/>).
    /// </summary>
    private void OfferUndo(string title)
    {
        if (_history is null)
        {
            return;
        }

        var version = _history.Version;
        Notices.Show(new Notice(NoticeKind.Info, title)
        {
            Key = UndoNoticeKey,
            Action = new NoticeAction(Str.Shell_Undo_Undo.Text(), () => StepIfCurrent(version, undo: true)),
        });
    }

    private void StepIfCurrent(long version, bool undo)
    {
        if (_history?.Version != version)
        {
            Notices.Show(new Notice(NoticeKind.Info, Str.Shell_Undo_Stale.Text())
            {
                Message = Str.Shell_Undo_StaleBody.Format(("keys", KeyChordText.Format(KeyChord.Ctrl(ChordKey.Z)))),
                Key = UndoNoticeKey,
            });
            return;
        }

        Step(undo);
    }

    // -----------------------------------------------------------------------------------------
    // Reset
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-892] Return every setting to a fresh profile's: the first-run desktop ([S-893]), default
    /// appearance and preferences. The current settings are kept beside the state file first, and
    /// if they cannot be, nothing is reset. Accounts, notes, tasks and habits are their own files
    /// and are left alone. Returns null on success, otherwise why nothing changed.
    /// </summary>
    public string? ResetAll()
    {
        if (ReplaceState(AppState.CreateDefault(), SettingsTransfer.ResetOccasion) is { } problem)
        {
            return problem;
        }

        Notices.Show(new Notice(NoticeKind.Success, Str.Shell_Notice_ResetTitle.Text())
        {
            Message = Str.Shell_Notice_ResetBody.Text(),
        });
        return null;
    }
}
