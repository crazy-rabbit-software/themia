using Themia.App.Services.Files;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk;

namespace Themia.App.Services;

/// <summary>
/// [S-5597] Ctrl+Z after a desktop delete: the items the delete sent to the Recycle Bin are put back from it to their
/// paths, as Explorer's own Undo Delete, and their icons stand where they stood (the step's layout brings them back,
/// <see cref="DesktopReconcile.Plan"/> keeps them till the items are listed again); Ctrl+Y recycles them again. The
/// step is the undo log's own ([S-885]), its files its <see cref="DesktopDeleteStep"/>; the rules are Core's
/// (<see cref="DesktopDeleteUndo"/>), the bin the host's (<see cref="RecycleBinItems"/>).
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>How many reads of the desktop a delete's record or an undo's wait outlives without its items showing up.</summary>
    private const int DesktopUndoReads = 3;

    private const string RestoreNoticeKey = "desktop-restore";

    /// <summary>[S-5597] A delete asked for and not yet seen in a read of the desktop, and how many reads it has left.</summary>
    private (DesktopDeleteStep Step, int Reads)? _pendingDelete;

    /// <summary>[S-5597] The keys an undo is putting back from the bin, not listed yet; null while none.</summary>
    private HashSet<string>? _restoring;

    private int _restoringReads;

    /// <summary>[S-5597] A redo's items the shell did not recycle again: known no more, so the next reconcile gives them their icons back.</summary>
    private List<string>? _survivors;

    /// <summary>[S-5597] The undo's or redo's file side declined the step (the shell's delete was cancelled): no notice of a step.</summary>
    private bool _effectRefused;

    /// <summary>[S-5597] A delete of <paramref name="icons"/>' files is asked for now: the record its step will carry.</summary>
    private void ExpectDesktopDelete(IReadOnlyList<DeskIcon> icons, bool permanent)
    {
        _pendingDelete = permanent
            ? null
            : DesktopDeleteUndo.Record([.. icons.Where(i => i.Kind == DeskIconKind.Item).Select(i => new DeletedDesktopItem(i.Key, i.Value))], DateTimeOffset.UtcNow) is { } step
                ? (step, DesktopUndoReads)
                : null;
    }

    /// <summary>
    /// [S-5597] The part of the waiting delete that <paramref name="plan"/> sees gone: the record of the step that takes
    /// their icons away, or null (it is no step of the user's then).
    /// </summary>
    private DesktopDeleteStep? DeletedBy(DesktopPlan plan)
    {
        if (_pendingDelete is not { } pending)
        {
            return null;
        }

        var gone = State.Views.SelectMany(v => v.Widgets)
            .Where(w => plan.Gone.Contains(w.Id))
            .Select(w => (w.Config as IDesktopItemConfig)?.Item)
            .OfType<string>()
            .ToHashSet(DesktopItemKeys.Comparer);
        var deleted = pending.Step.Items.Where(i => gone.Contains(i.Key)).ToList();
        if (deleted.Count == 0)
        {
            _pendingDelete = pending.Reads > 1 ? (pending.Step, pending.Reads - 1) : null;
            return null;
        }

        _pendingDelete = null;
        Log.Info(Scope, $"{deleted.Count} desktop item(s) went to the Recycle Bin: one undo step");
        return new DesktopDeleteStep(deleted, pending.Step.DeletedAt);
    }

    /// <summary>[S-5597] The keys still being put back for this read of the desktop; those it lists have come.</summary>
    private HashSet<string>? Restoring(IReadOnlyList<DesktopItem> listing)
    {
        if (_restoring is not { } waiting)
        {
            return null;
        }

        waiting.ExceptWith(listing.Select(i => i.Key));
        if (waiting.Count == 0 || --_restoringReads < 0)
        {
            if (waiting.Count > 0)
            {
                Log.Warn(Scope, $"{waiting.Count} item(s) put back from the Recycle Bin did not show up on the desktop");
            }

            _restoring = null;
            return null;
        }

        return waiting;
    }

    /// <summary>[S-5597] A step's file side, before its layout comes back. False declines the step.</summary>
    private bool RunUndoEffect(IUndoEffect effect, bool undo)
    {
        if (effect is not DesktopDeleteStep step)
        {
            return true;
        }

        try
        {
            return undo ? PutBack(step) : RecycleAgain(step);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a desktop delete's files could not be undone or redone", ex);
            return true;
        }
    }

    /// <summary>
    /// [S-5597] Undo: each item the bin holds put back to its path through the bin's Restore; one whose copy is gone or
    /// whose path is taken is left, and said. The step is always taken: what was not put back loses its icon again.
    /// </summary>
    private bool PutBack(DesktopDeleteStep step)
    {
        var plan = DesktopDeleteUndo.Plan(step, RecycleBinItems.List(), Exists);
        var putting = new HashSet<string>(DesktopItemKeys.Comparer);
        var left = plan.Skipped.Count;
        foreach (var (item, from) in plan.Restore)
        {
            if (RecycleBinItems.Restore(from.Id, DeskOwner))
            {
                putting.Add(item.Key);
            }
            else
            {
                left++;
            }
        }

        Log.Info(Scope, $"undo of a desktop delete: {putting.Count} item(s) put back, {left} left ({string.Join(", ", plan.Skipped.Select(s => s.Why))})");
        if (putting.Count > 0)
        {
            (_restoring ??= new HashSet<string>(DesktopItemKeys.Comparer)).UnionWith(putting);
            _restoringReads = DesktopUndoReads;
        }

        if (left > 0)
        {
            Notices.Show(new Notice(NoticeKind.Warning, Str.Desktop_Restore_Skipped.Plural(left))
            {
                Message = Str.Desktop_Restore_SkippedBody.Text(),
                Key = RestoreNoticeKey,
            });
        }

        return true;
    }

    /// <summary>
    /// [S-5597] Redo: the items that are back at their paths recycled again through the shell's delete, as the first
    /// time. Declined where the shell deleted none of them (its prompt cancelled); one it left keeps its icon.
    /// </summary>
    private bool RecycleAgain(DesktopDeleteStep step)
    {
        var there = step.Items.Where(i => Exists(i.Path)).ToList();
        if (there.Count == 0)
        {
            return true;
        }

        step.DeletedAt = DateTimeOffset.UtcNow;
        if (!ShellContextMenu.InvokeVerb([.. there.Select(i => i.Path)], "delete", DeskOwner))
        {
            Log.Warn(Scope, "the shell did not delete the desktop items again; the redo is not taken");
            _effectRefused = true;
            return false;
        }

        var survivors = there.Where(i => Exists(i.Path)).Select(i => i.Key).ToList();
        if (survivors.Count == there.Count)
        {
            Log.Info(Scope, "the desktop items were not deleted again (the prompt was cancelled); the redo is not taken");
            _effectRefused = true;
            return false;
        }

        _survivors = survivors.Count > 0 ? survivors : null;
        return true;
    }

    /// <summary>[S-5597] A redo's survivors known no more: the reconcile after the step gives them their icons back.</summary>
    private void ForgetSurvivors()
    {
        if (_survivors is not { } survivors)
        {
            return;
        }

        _survivors = null;
        var gone = survivors.ToHashSet(DesktopItemKeys.Comparer);
        State.Desktop.Known = [.. State.Desktop.Known.Where(key => !gone.Contains(key))];
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
