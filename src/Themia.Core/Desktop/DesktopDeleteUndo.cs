using Themia.Core.Model;

namespace Themia.Core.Desktop;

/// <summary>[S-5597] A desktop item a delete sent to the Recycle Bin: its key on the desktop and its file's path.</summary>
public sealed record DeletedDesktopItem(string Key, string Path);

/// <summary>
/// [S-5597] The undo record of a desktop delete (Delete, the menu's Delete), carried by its step of the undo log
/// (<see cref="LayoutHistory"/>; the step's layout holds the icons where they stood, in every view): which items went,
/// and from when the Recycle Bin holds them. A redo recycles them again and moves <see cref="DeletedAt"/> on, so the
/// next undo looks for the copies that redo made.
/// </summary>
public sealed class DesktopDeleteStep(IReadOnlyList<DeletedDesktopItem> items, DateTimeOffset deletedAt) : IUndoEffect
{
    public IReadOnlyList<DeletedDesktopItem> Items { get; } = items ?? throw new ArgumentNullException(nameof(items));

    /// <summary>When the delete was asked for (just before the shell's delete ran).</summary>
    public DateTimeOffset DeletedAt { get; set; } = deletedAt;
}

/// <summary>[S-5597] Why an item of a desktop delete is not put back.</summary>
public enum RestoreSkip
{
    /// <summary>The Recycle Bin no longer holds it (emptied, restored by hand, deleted for good).</summary>
    NotInBin,

    /// <summary>Something else stands at its path now: it is left in the bin, never put over it.</summary>
    PathTaken,
}

/// <summary>[S-5597] What an undo of a desktop delete does: the items it puts back from which recycled copies, and the ones it leaves.</summary>
public sealed record DesktopRestorePlan(
    IReadOnlyList<(DeletedDesktopItem Item, RecycledItem From)> Restore,
    IReadOnlyList<(DeletedDesktopItem Item, RestoreSkip Why)> Skipped);

/// <summary>
/// [S-5597] Undoing a desktop delete as Explorer's own Undo Delete does: each item put back from the Recycle Bin to its
/// original path. Pure rules: which recycled copy is the one this delete made, and what is left where it cannot be put
/// back.
/// </summary>
public static class DesktopDeleteUndo
{
    /// <summary>
    /// [S-5597] How much earlier than the delete was asked for the bin may stamp it: the bin's clock is the file
    /// system's, read on another thread.
    /// </summary>
    public static readonly TimeSpan Slack = TimeSpan.FromSeconds(5);

    /// <summary>
    /// [S-5597] The record of a delete of <paramref name="items"/> asked for at <paramref name="at"/>, or null where none
    /// went to the bin.
    /// </summary>
    public static DesktopDeleteStep? Record(IReadOnlyList<DeletedDesktopItem> items, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(items);
        var distinct = items.Where(i => !string.IsNullOrWhiteSpace(i.Path)).DistinctBy(i => Normal(i.Path), StringComparer.OrdinalIgnoreCase).ToList();
        return distinct.Count == 0 ? null : new DesktopDeleteStep(distinct, at);
    }

    /// <summary>
    /// [S-5597] The recycled copy this delete made of <paramref name="item"/>: deleted from its path (case aside) no
    /// earlier than <paramref name="at"/> less <see cref="Slack"/>, the earliest such (a later delete of a file made
    /// again at that path is another's); null where the bin has none.
    /// </summary>
    public static RecycledItem? Match(DeletedDesktopItem item, DateTimeOffset at, IReadOnlyList<RecycledItem> bin)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(bin);
        var path = Normal(item.Path);
        return bin
            .Where(b => string.Equals(Normal(b.OriginalPath), path, StringComparison.OrdinalIgnoreCase) && b.DeletedAt >= at - Slack)
            .OrderBy(b => b.DeletedAt)
            .FirstOrDefault();
    }

    /// <summary>
    /// [S-5597] What an undo of <paramref name="step"/> puts back from <paramref name="bin"/>: each item whose copy the
    /// bin holds (<see cref="Match"/>) and whose path is free (<paramref name="exists"/>: something stands there); the
    /// rest left, each with why. Never a copy put over something, never one copy twice.
    /// </summary>
    public static DesktopRestorePlan Plan(DesktopDeleteStep step, IReadOnlyList<RecycledItem> bin, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(bin);
        ArgumentNullException.ThrowIfNull(exists);

        var restore = new List<(DeletedDesktopItem, RecycledItem)>();
        var skipped = new List<(DeletedDesktopItem, RestoreSkip)>();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in step.Items)
        {
            if (Match(item, step.DeletedAt, [.. bin.Where(b => !taken.Contains(b.Id))]) is not { } from)
            {
                skipped.Add((item, RestoreSkip.NotInBin));
            }
            else if (exists(item.Path))
            {
                skipped.Add((item, RestoreSkip.PathTaken));
            }
            else
            {
                taken.Add(from.Id);
                restore.Add((item, from));
            }
        }

        return new DesktopRestorePlan(restore, skipped);
    }

    private static string Normal(string path) => path.TrimEnd('\\', '/');
}
