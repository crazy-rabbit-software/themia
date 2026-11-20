using Themia.Core.Shell;

namespace Themia.Core.Notes;

/// <summary>[S-1359] [S-14772] A format of the note's text: each acts on the selection (a mark) or its lines (a paragraph's kind).</summary>
public enum NoteCommand
{
    Bold,
    Italic,
    Strike,
    Code,
    Heading,
    Bullet,
    Ordered,
    Task,
    Quote,
}

/// <summary>What a line of the note's menus does.</summary>
public enum NoteMenuItem
{
    /// <summary>A divider between the list's groups (what a line is when nothing else is said).</summary>
    Divider,

    Cut,
    Copy,
    Paste,

    /// <summary>A format (<see cref="NoteMenuLine.Format"/>).</summary>
    Format,
    Undo,
    Redo,
    SelectAll,

    /// <summary>[S-14771] The pill's "…": rename the note shown.</summary>
    Rename,

    /// <summary>[S-14771] The pill's "…": delete the note shown, asked first.</summary>
    Delete,
}

/// <summary>[S-14772] How the text's menu came.</summary>
public enum NoteMenuOpening
{
    /// <summary>By itself, when the pointer let go over text it selected: the selection's bar, its formats alone.</summary>
    Selected,

    /// <summary>
    /// A right-click in the text, Shift+F10 or the Menu key: the text's menu, as Windows' text menus are: Cut, Copy and Paste
    /// before the marks in its row, a paragraph's kinds, Undo, Redo and Select all in its list.
    /// </summary>
    Context,
}

/// <summary>A line of the note's menus, or a divider.</summary>
/// <param name="Item">What it does.</param>
/// <param name="Format">The format, for <see cref="NoteMenuItem.Format"/>.</param>
/// <param name="Quick">Its place in the menu's icon row ([S-1740]); null for the list.</param>
/// <param name="Disabled">Shown greyed: nothing to cut, nothing to undo, a note that cannot be typed in.</param>
/// <param name="On">A format in force at the selection ([S-1359]: lit in the row); null for every other line.</param>
public readonly record struct NoteMenuLine(
    NoteMenuItem Item,
    NoteCommand? Format = null,
    QuickCommand? Quick = null,
    bool Disabled = false,
    bool? On = null)
{
    public static NoteMenuLine Separator => new(NoteMenuItem.Divider);

    /// <summary>A divider between the list's groups.</summary>
    public bool Divider => Item == NoteMenuItem.Divider;
}

/// <summary>[S-1364] [S-14771] A line of the note picker: a note (checked while it is the one shown), or New note (no id).</summary>
public readonly record struct NotePick(string? Id, string Title, bool Shown);

/// <summary>
/// [S-14771] [S-14772] Which of the note's actions stand where (owner, 2026-11-20: "actions that can be inlined into a menu
/// that opens at the mouse location after selecting text should be"): every format acts on the selection or its lines, so
/// all of them are the selection's menu (the toolbar's buttons were nine: Bold, Italic, Strikethrough, Code, Heading, the
/// bulleted, numbered and task lists, Quote); what acts on the note itself (Rename…, Delete note…) is the pill's "…"; which
/// note shows, and a new one, is the pill's picker. Clear note stays the widget's page's ([S-130]); undo and redo are the
/// text's menu's and its keys', as in every Windows text.
/// </summary>
public static class NoteMenus
{
    /// <summary>
    /// The most a menu's icon row holds before the command bar folds the rest into its list on its own (twelve did: Task list
    /// and Quote fell into the list, Task list still lit as a row command): the selection's bar's nine.
    /// </summary>
    public const int RowMost = 9;

    /// <summary>The formats in the selection menu's row: the marks first (as Windows' Bold, Italic), then a paragraph's kinds.</summary>
    public static IReadOnlyList<NoteCommand> Formats { get; } =
    [
        NoteCommand.Bold, NoteCommand.Italic, NoteCommand.Strike, NoteCommand.Code,
        NoteCommand.Heading, NoteCommand.Bullet, NoteCommand.Ordered, NoteCommand.Task, NoteCommand.Quote,
    ];

    /// <summary>A format's place in the icon row.</summary>
    public static QuickCommand Quick(NoteCommand command) => command switch
    {
        NoteCommand.Bold => QuickCommand.Bold,
        NoteCommand.Italic => QuickCommand.Italic,
        NoteCommand.Strike => QuickCommand.Strikethrough,
        NoteCommand.Code => QuickCommand.Code,
        NoteCommand.Heading => QuickCommand.Heading,
        NoteCommand.Bullet => QuickCommand.BulletList,
        NoteCommand.Ordered => QuickCommand.NumberedList,
        NoteCommand.Task => QuickCommand.Checklist,
        _ => QuickCommand.Quote,
    };

    /// <summary>A mark of the selection's words (Bold, Italic, Strikethrough, Code), not a kind of its lines.</summary>
    public static bool IsMark(NoteCommand command) => command is NoteCommand.Bold or NoteCommand.Italic or NoteCommand.Strike or NoteCommand.Code;

    /// <summary>[S-1359] Whether <paramref name="command"/> is in force at the selection (a checked task's strike does not light Strike).</summary>
    public static bool IsOn(NoteCommand command, NoteMarks marks) => command switch
    {
        NoteCommand.Heading => marks.Heading > 0,
        NoteCommand.Bold => marks.Bold,
        NoteCommand.Italic => marks.Italic,
        NoteCommand.Strike => marks.Strike,
        NoteCommand.Code => marks.Code,
        NoteCommand.Bullet => marks.Block == NoteBlock.Bullet,
        NoteCommand.Ordered => marks.Block == NoteBlock.Ordered,
        NoteCommand.Task => marks.Block == NoteBlock.Task,
        _ => marks.Block == NoteBlock.Quote,
    };

    /// <summary>
    /// [S-14772] Whether the pointer letting go opens the selection's bar: the left button (a mouse's, a pen's, a touch's)
    /// let go with text selected, in a note that can be typed in. A click (no selection) never does: it places the caret, or
    /// ticks a task's box ([S-1358]).
    /// </summary>
    public static bool OpensOnRelease(bool leftReleased, int start, int end, bool readOnly)
        => leftReleased && start != end && !readOnly;

    /// <summary>
    /// [S-14772] The text's menu as it opens: the formats, each lit while in force at the selection, all in the selection's
    /// bar's row. Opened as the text's menu (<see cref="NoteMenuOpening.Context"/>), Cut, Copy and Paste come first in the
    /// row with the marks after them, and a paragraph's kinds go to its list (each checked while in force), with Undo, Redo
    /// and Select all: twelve icons are more than a row holds (<see cref="RowMost"/>).
    /// </summary>
    public static IReadOnlyList<NoteMenuLine> Text(NoteMenuOpening opening, bool selected, bool readOnly, bool canUndo, bool canRedo, NoteMarks marks)
    {
        var context = opening == NoteMenuOpening.Context;
        var lines = new List<NoteMenuLine>();
        if (context)
        {
            lines.Add(new NoteMenuLine(NoteMenuItem.Cut, Quick: QuickCommand.Cut, Disabled: readOnly || !selected));
            lines.Add(new NoteMenuLine(NoteMenuItem.Copy, Quick: QuickCommand.Copy, Disabled: !selected));
            lines.Add(new NoteMenuLine(NoteMenuItem.Paste, Quick: QuickCommand.Paste, Disabled: readOnly));
        }

        foreach (var format in Formats)
        {
            var row = !context || IsMark(format);
            lines.Add(new NoteMenuLine(NoteMenuItem.Format, format, row ? Quick(format) : null, Disabled: readOnly, On: IsOn(format, marks)));
        }

        if (context)
        {
            lines.Add(NoteMenuLine.Separator);
            lines.Add(new NoteMenuLine(NoteMenuItem.Undo, Disabled: readOnly || !canUndo));
            lines.Add(new NoteMenuLine(NoteMenuItem.Redo, Disabled: readOnly || !canRedo));
            lines.Add(NoteMenuLine.Separator);
            lines.Add(new NoteMenuLine(NoteMenuItem.SelectAll));
        }

        return lines;
    }

    /// <summary>
    /// [S-14771] The pill's "…": what acts on the note shown, Rename… and Delete note… (asked first), while a note is
    /// shown; a picture of the widget shows the button as the desktop does, opening nothing. Nothing else: the formats are
    /// the selection's, Clear note is the widget's page's.
    /// </summary>
    public static IReadOnlyList<NoteMenuItem> More(bool noteShown) => noteShown ? [NoteMenuItem.Rename, NoteMenuItem.Delete] : [];

    /// <summary>
    /// [S-1364] [S-14771] The pill's picker: every note on this PC by its title (untitled ones by
    /// <paramref name="untitled"/>), the one shown checked, then New note.
    /// </summary>
    public static IReadOnlyList<NotePick> Picker(IReadOnlyList<NoteMeta> notes, string? shownId, string untitled, string newNote)
    {
        ArgumentNullException.ThrowIfNull(notes);

        var lines = new List<NotePick>(notes.Count + 1);
        foreach (var note in notes)
        {
            lines.Add(new NotePick(note.Id, Name(note.Title, untitled), string.Equals(note.Id, shownId, StringComparison.Ordinal)));
        }

        lines.Add(new NotePick(null, newNote, false));
        return lines;
    }

    /// <summary>
    /// [S-14771] The name the pill's picker says: the shown note's title (<paramref name="untitled"/> for a blank one), or
    /// <paramref name="none"/> while no note is shown (it was deleted: the picker is how another is chosen).
    /// </summary>
    public static string PickerName(string? shownTitle, bool noteShown, string untitled, string none)
        => !noteShown ? none : Name(shownTitle, untitled);

    private static string Name(string? title, string untitled) => string.IsNullOrWhiteSpace(title) ? untitled : title;
}
