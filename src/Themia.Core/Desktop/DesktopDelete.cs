using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-14320] One selected widget as Delete sees it: its id and, for a desktop icon (either source), the icon as the
/// desktop's commands see it (<see cref="DeskIcon"/>); null for a widget card (a clock, a list, a fence).
/// </summary>
public readonly record struct DeskDeleting(string WidgetId, DeskIcon? Icon);

/// <summary>
/// [S-14320] What one Delete does: the desktop items' files handed to the shell's own delete (the Recycle Bin, or for good
/// with Shift, the shell's prompt as the user set it up), and the widgets taken off the desktop (one undoable step of the
/// one history, their places and config kept by the step). Never both for one item: a card stands for no file, and a
/// desktop item's widget goes with its file, by the listing.
/// </summary>
public sealed record DeskRemoval(IReadOnlyList<string> Files, IReadOnlyList<string> Widgets)
{
    /// <summary>Whether it does nothing.</summary>
    public bool IsEmpty => Files.Count == 0 && Widgets.Count == 0;
}

/// <summary>
/// [S-14320] [S-5524] [S-5583] [S-12950] What Delete (and Shift+Delete, and a menu's Delete line) does to which selected
/// items, given edit mode and Lock widgets (owner, 2026-11-20: "deleting widgets with delete does not work in non edit mode
/// without the lock enabled"). Pure: the host carries it out.
/// <list type="bullet">
/// <item>Edit mode ([S-5524]): every selected widget leaves this view, a desktop item's too (its file stays; it is listed as
/// unplaced); no file is touched.</item>
/// <item>Outside edit mode, the desktop's one selection ([S-11877]): the desktop items' files go to the shell's delete and the
/// own icons' widgets are removed (<see cref="DesktopCommands.Delete"/>, the icons' one rule, [S-5583]); the widget cards are
/// removed too while Lock widgets is off. With Lock widgets on no card is removed (a locked card is in no selection anyway,
/// [S-12953]): icons alone, as before. Shift+Delete is the same for widgets (they have no Recycle Bin; the step is the way
/// back); only the files' delete is for good.</item>
/// </list>
/// </summary>
public static class DesktopDelete
{
    /// <summary>The removal Delete makes of <paramref name="selection"/>.</summary>
    public static DeskRemoval Of(IReadOnlyList<DeskDeleting> selection, bool editMode, bool lockWidgets)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (editMode)
        {
            return new DeskRemoval([], [.. selection.Select(s => s.WidgetId).Distinct(StringComparer.Ordinal)]);
        }

        var icons = DesktopCommands.Delete([.. selection.Select(s => s.Icon).OfType<DeskIcon>()]);
        var own = new HashSet<string>(icons.OwnKeys, DesktopItemKeys.Comparer);
        List<string> widgets = [.. selection
            .Where(s => s.Icon is { } icon ? own.Contains(icon.Key) : !lockWidgets)
            .Select(s => s.WidgetId)
            .Distinct(StringComparer.Ordinal)];
        return new DeskRemoval(icons.Files, widgets);
    }

    /// <summary>
    /// [S-14321] How the card menu's Delete line reads for <paramref name="removal"/>: <see cref="DeleteWords.Delete"/>
    /// (Explorer's word) while it hands a file to the shell's delete, else "Delete widget" for one widget and "Delete
    /// N widgets" for more; <see cref="DeleteWords.None"/> when it would do nothing (no line).
    /// </summary>
    public static DeleteWords Words(DeskRemoval removal)
    {
        ArgumentNullException.ThrowIfNull(removal);
        return removal switch
        {
            { Files.Count: > 0 } => DeleteWords.Delete,
            { Widgets.Count: 1 } => DeleteWords.OneWidget,
            { Widgets.Count: > 1 } => DeleteWords.Widgets,
            _ => DeleteWords.None,
        };
    }
}

/// <summary>[S-14321] The words of a Delete line.</summary>
public enum DeleteWords
{
    /// <summary>No line: Delete would do nothing.</summary>
    None,

    /// <summary>"Delete": a file goes to the shell's delete (Explorer's own word).</summary>
    Delete,

    /// <summary>"Delete widget".</summary>
    OneWidget,

    /// <summary>"Delete N widgets".</summary>
    Widgets,
}
