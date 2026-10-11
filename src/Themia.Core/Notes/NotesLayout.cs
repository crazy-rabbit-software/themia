using Themia.Core.Widgets;

namespace Themia.Core.Notes;

/// <summary>What the note widget shows at one size.</summary>
/// <param name="ToolbarButtons">How many formatting buttons fit, in priority order; 0 hides the bar.</param>
/// <param name="Status">The status line under the text: word count.</param>
/// <param name="StatusDetail">The status line also gives characters and the save state.</param>
/// <param name="TextWidth">The width the text has: the whole box at every size, never shared with a note list.</param>
/// <param name="TextLines">Whole lines of text the editor shows at least (for tests; the editor scrolls).</param>
public sealed record NotesLayoutPlan(int ToolbarButtons, bool Status, bool StatusDetail, double TextWidth, int TextLines)
{
    public bool Toolbar => ToolbarButtons > 0;
}

/// <summary>
/// [S-1363] The note widget's disclosure ladder (<see cref="Disclosure"/>, S-660).
/// <list type="number">
/// <item>The text — always, at least two lines, even at 120 × 120.</item>
/// <item>The formatting bar, from a regular width with regular height, or on a wide strip; as many
/// buttons as fit, the most used first.</item>
/// <item>A status line (words) once the height is expanded; with characters and the save state once
/// the width is too.</item>
/// </list>
/// The widget is the one note at every size: a larger tile gives the text more room, never a note
/// list beside it (switching notes is the header's picker, [S-1364]).
/// </summary>
public static class NotesLayout
{
    /// <summary>Every button the bar can show.</summary>
    public const int AllButtons = 9;

    /// <summary>The bar's buttons, square.</summary>
    public const double ButtonSize = 28;

    public const double ButtonGap = 2;

    /// <summary>The bar's height: a button and its focus margin.</summary>
    public const double ToolbarHeight = 32;

    /// <summary>Fewer buttons than this is not a toolbar worth its band.</summary>
    public const int MinButtons = 4;

    /// <summary>Lines of text the editor reserves before anything else is shown.</summary>
    public const int MinTextLines = 2;

    /// <param name="area">The view's slot.</param>
    /// <param name="inset">The padding the view keeps on each side of its parts.</param>
    /// <param name="gap">The margin between parts.</param>
    /// <param name="lineHeight">One line of the note at its size.</param>
    /// <param name="statusHeight">One line of the status (Caption).</param>
    public static NotesLayoutPlan Plan(WidgetArea area, double inset, double gap, double lineHeight, double statusHeight)
    {
        if (!area.IsMeasured)
        {
            return new NotesLayoutPlan(0, false, false, 0, MinTextLines);
        }

        var box = area.Deflate(inset, inset);
        var width = box.Width;

        var room = new Disclosure(width, box.Height, gap);
        room.Reserve(lineHeight * MinTextLines);

        var buttons = ButtonsFitting(width);
        var toolbar = area.WidthClass >= SizeClass.Regular
            && (area.HeightClass >= SizeClass.Regular || area.Shape == AreaShape.Wide)
            && buttons >= MinButtons
            && room.Take(ToolbarHeight);

        var status = area.HeightClass >= SizeClass.Expanded && room.Take(statusHeight);
        var detail = status && area.WidthClass >= SizeClass.Expanded;

        var lines = MinTextLines + Disclosure.WholeRows(room.Remaining, lineHeight);
        return new NotesLayoutPlan(toolbar ? buttons : 0, status, detail, width, lines);
    }

    /// <summary>How many buttons a row of <paramref name="width"/> holds, up to <see cref="AllButtons"/>.</summary>
    public static int ButtonsFitting(double width)
        => Math.Clamp(Disclosure.WholeRows(width, ButtonSize, ButtonGap), 0, AllButtons);
}
