using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>[S-13102] Which file operation on the desktop a step of the undo log took, as Explorer's Undo names it.</summary>
public enum DesktopFileUndoKind
{
    /// <summary>Files copied onto the desktop (a paste of a copy, a drop that copied). Undo sends the copies to the Recycle Bin.</summary>
    Copy,

    /// <summary>Files moved onto the desktop (a paste of a cut, a drop that moved). Undo moves them back where they came from.</summary>
    Move,

    /// <summary>A folder made with New › Folder or Ctrl+Shift+N. Undo sends it to the Recycle Bin.</summary>
    NewFolder,

    /// <summary>A shortcut made (Paste shortcut, a drop that linked). Undo sends it to the Recycle Bin.</summary>
    Shortcut,

    /// <summary>One item renamed (F2, the menu's Rename). Undo gives it its old name.</summary>
    Rename,

    /// <summary>A file made from one of New ›'s templates. Undo sends it to the Recycle Bin; it is not made again.</summary>
    NewFile,
}

/// <summary>[S-13103] What the background menu's Undo line says it takes back ("Undo Rename"), as Explorer's.</summary>
public enum DesktopUndoName
{
    Copy = DesktopFileUndoKind.Copy,
    Move = DesktopFileUndoKind.Move,
    NewFolder = DesktopFileUndoKind.NewFolder,
    Shortcut = DesktopFileUndoKind.Shortcut,
    Rename = DesktopFileUndoKind.Rename,
    NewFile = DesktopFileUndoKind.NewFile,
    Delete,
}

/// <summary>[S-13102] One file a step changed: where it was (<paramref name="From"/>: a copy's or a move's source, a rename's old path; null for what was made) and where it is (<paramref name="To"/>).</summary>
public readonly record struct DesktopFileChange(string? From, string To);

/// <summary>
/// [S-13102] What a step of the undo log did to the desktop's files, carried with the step both ways ([S-5597]'s
/// <see cref="IUndoEffect"/>): its kind and the files it changed. An undo takes the files back (<see cref="DesktopFileUndo.Undo"/>),
/// a redo does it again (<see cref="DesktopFileUndo.Redo"/>).
/// </summary>
public sealed record DesktopFileStep(DesktopFileUndoKind Kind, IReadOnlyList<DesktopFileChange> Changes) : IUndoEffect;

/// <summary>[S-13102] One shell operation an undo or a redo runs.</summary>
public enum DesktopFileOp
{
    /// <summary>To the Recycle Bin (never for good: the step's undo of a copy can itself be undone from the bin).</summary>
    Recycle,

    /// <summary>Into <see cref="DesktopFileAction.Folder"/>, its name kept.</summary>
    MoveInto,

    /// <summary>Copied into <see cref="DesktopFileAction.Folder"/>.</summary>
    CopyInto,

    /// <summary>Its name becomes <see cref="DesktopFileAction.Name"/>.</summary>
    Rename,

    /// <summary>A folder made at <see cref="DesktopFileAction.Path"/>.</summary>
    MakeFolder,
}

/// <summary>[S-13102] One operation: on <paramref name="Path"/>, into <paramref name="Folder"/> or as <paramref name="Name"/>.</summary>
public readonly record struct DesktopFileAction(DesktopFileOp Op, string Path, string? Folder = null, string? Name = null);

/// <summary>
/// [S-13102] What an undo or a redo runs: the operations, the desktop items it will bring (to stand where the step's
/// layout puts them till they are listed, as [S-5597]'s), and how many changes it had to leave (a file gone, a place taken:
/// never over something).
/// </summary>
public sealed record DesktopFileUndoPlan(IReadOnlyList<DesktopFileAction> Actions, IReadOnlyList<string> Expected, int Left);

/// <summary>
/// [S-13102] Explorer's Undo for the desktop's own file operations, as pure rules (the owner, 2026-11-19: "removing and
/// undoing deletions, and copy pasting onto the desktop should all be possible without noticing a difference to normal
/// windows"). Explorer's undo lives in Explorer's process, out of reach, so the desktop's undo log ([S-884]) carries a
/// step's files itself: a delete ([S-5597]), and copy, move, new folder, shortcut and rename here. Each undo and redo is a
/// shell operation through the host's file operations (their own prompts and conflict dialogs); a change whose file is
/// gone, or whose place is taken now, is left and said, never forced.
/// </summary>
public static class DesktopFileUndo
{
    /// <summary>
    /// [S-13102] The step of a command whose arrivals were seen: <paramref name="arrived"/> (their keys, the full paths on
    /// the desktop) matched to <paramref name="sources"/> by file name (case aside; an arrival named by none is made, not
    /// copied, and has no source). Null when nothing arrived.
    /// </summary>
    public static DesktopFileStep? Record(DesktopFileUndoKind kind, IReadOnlyList<string> arrived, IReadOnlyList<string> sources)
    {
        ArgumentNullException.ThrowIfNull(arrived);
        ArgumentNullException.ThrowIfNull(sources);
        var files = arrived.Where(DesktopItemKeys.IsValid).Where(key => !DesktopItemKeys.IsShellItem(key)).Distinct(DesktopItemKeys.Comparer).ToList();
        if (files.Count == 0)
        {
            return null;
        }

        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            byName.TryAdd(DesktopRename.FileName(source), source);
        }

        var changes = files.Select(to => new DesktopFileChange(
            kind is DesktopFileUndoKind.Copy or DesktopFileUndoKind.Move && byName.TryGetValue(DesktopRename.FileName(to), out var from) ? from : null,
            to)).ToList();

        // A move whose arrival came from no source is not a move Themia can take back: it stays a step of nothing.
        if (kind == DesktopFileUndoKind.Move)
        {
            changes = [.. changes.Where(c => c.From is not null)];
        }

        return changes.Count == 0 ? null : new DesktopFileStep(kind, changes);
    }

    /// <summary>[S-13102] The step of one rename the shell carried out: <paramref name="before"/> to <paramref name="after"/>.</summary>
    public static DesktopFileStep? Renamed(string before, string after)
        => DesktopItemKeys.IsValid(before) && DesktopItemKeys.IsValid(after) && !DesktopItemKeys.Comparer.Equals(before, after)
            ? new DesktopFileStep(DesktopFileUndoKind.Rename, [new DesktopFileChange(before, after)])
            : null;

    /// <summary>
    /// [S-13102] Undo: a copy, a new folder or a shortcut goes to the Recycle Bin; a move goes back into the folder it came
    /// from (left when something there has its name now); a rename takes its old name back (left when the old name is taken).
    /// A change whose file is not there is left. <paramref name="exists"/> answers whether a path is there now.
    /// </summary>
    public static DesktopFileUndoPlan Undo(DesktopFileStep step, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(exists);
        var actions = new List<DesktopFileAction>();
        var expected = new List<string>();
        var left = 0;
        foreach (var change in step.Changes)
        {
            if (!exists(change.To))
            {
                left++;
                continue;
            }

            switch (step.Kind)
            {
                case DesktopFileUndoKind.Copy or DesktopFileUndoKind.NewFolder or DesktopFileUndoKind.NewFile or DesktopFileUndoKind.Shortcut:
                    actions.Add(new DesktopFileAction(DesktopFileOp.Recycle, change.To));
                    break;
                case DesktopFileUndoKind.Move when change.From is { } from && Folder(from) is { } folder:
                    if (exists(Path.Combine(folder, DesktopRename.FileName(change.To))))
                    {
                        left++;
                        break;
                    }

                    actions.Add(new DesktopFileAction(DesktopFileOp.MoveInto, change.To, Folder: folder));
                    if (IsOnDesktop(from, change.To))
                    {
                        expected.Add(from);
                    }

                    break;
                case DesktopFileUndoKind.Rename when change.From is { } old:
                    if (exists(old) && !DesktopItemKeys.Comparer.Equals(old, change.To))
                    {
                        left++;
                        break;
                    }

                    actions.Add(new DesktopFileAction(DesktopFileOp.Rename, change.To, Name: DesktopRename.FileName(old)));
                    expected.Add(old);
                    break;
                default:
                    left++;
                    break;
            }
        }

        return new DesktopFileUndoPlan(actions, expected, left);
    }

    /// <summary>
    /// [S-13102] Redo: the copy made again from its source, the move made again, the folder made again (empty, under its
    /// name), the rename made again; a shortcut or a file from a template, whose making was the shell's, is not made again
    /// (left). A
    /// change whose place on the desktop is taken now, or whose source is gone, is left.
    /// </summary>
    public static DesktopFileUndoPlan Redo(DesktopFileStep step, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(exists);
        var actions = new List<DesktopFileAction>();
        var expected = new List<string>();
        var left = 0;
        foreach (var change in step.Changes)
        {
            if (exists(change.To) || Folder(change.To) is not { } desktop)
            {
                left++;
                continue;
            }

            switch (step.Kind)
            {
                case DesktopFileUndoKind.Copy when change.From is { } source && exists(source):
                    actions.Add(new DesktopFileAction(DesktopFileOp.CopyInto, source, Folder: desktop));
                    expected.Add(change.To);
                    break;
                case DesktopFileUndoKind.Move when change.From is { } from && Folder(from) is { } folder
                    && exists(Path.Combine(folder, DesktopRename.FileName(change.To))):
                    actions.Add(new DesktopFileAction(DesktopFileOp.MoveInto, Path.Combine(folder, DesktopRename.FileName(change.To)), Folder: desktop));
                    expected.Add(change.To);
                    break;
                case DesktopFileUndoKind.NewFolder:
                    actions.Add(new DesktopFileAction(DesktopFileOp.MakeFolder, change.To));
                    expected.Add(change.To);
                    break;
                case DesktopFileUndoKind.Rename when change.From is { } old && exists(old):
                    actions.Add(new DesktopFileAction(DesktopFileOp.Rename, old, Name: DesktopRename.FileName(change.To)));
                    expected.Add(change.To);
                    break;
                default:
                    left++;
                    break;
            }
        }

        return new DesktopFileUndoPlan(actions, expected, left);
    }

    /// <summary>
    /// [S-13103] What the background menu's Undo line names: the step Ctrl+Z would take back, while it is one of the
    /// desktop's file steps (a delete, [S-5597], or one of these); null otherwise (an arrangement's step is the edit bar's
    /// history, which Explorer's menu has no line for).
    /// </summary>
    public static DesktopUndoName? Offered(UndoAction? next, IUndoEffect? effect) => next switch
    {
        UndoAction.DeleteDesktopItems => DesktopUndoName.Delete,
        UndoAction.DesktopFiles when effect is DesktopFileStep step => (DesktopUndoName)(int)step.Kind,
        _ => null,
    };

    private static string? Folder(string path)
    {
        var folder = Path.GetDirectoryName(path.TrimEnd('\\', '/'));
        return string.IsNullOrEmpty(folder) ? null : folder;
    }

    private static bool IsOnDesktop(string path, string desktopPath)
        => string.Equals(Folder(path), Folder(desktopPath), StringComparison.OrdinalIgnoreCase);
}
