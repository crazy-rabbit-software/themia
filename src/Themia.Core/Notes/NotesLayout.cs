using Themia.Core.Widgets;

namespace Themia.Core.Notes;

/// <summary>What the note widget's body shows at one size.</summary>
/// <param name="Status">The line under the text that says the note is not saved ([S-1362]).</param>
/// <param name="TextWidth">The width the text has: the whole box at every size, never shared with a note list.</param>
/// <param name="TextLines">Whole lines of text the editor shows at least (for tests; the editor scrolls).</param>
public sealed record NotesLayoutPlan(bool Status, double TextWidth, int TextLines);

/// <summary>
/// [S-3963] The formatting bar in the card's header: how many buttons stand in it, and whether the
/// rest are behind a "More" button.
/// </summary>
/// <param name="Inline">Buttons shown in the header, in rank order (most used first).</param>
/// <param name="Overflow">A "More" button holds the buttons that did not fit.</param>
public readonly record struct NotesHeaderBar(int Inline, bool Overflow)
{
    /// <summary>Anything of the bar is in the header.</summary>
    public bool Shown => Inline > 0 || Overflow;

    /// <summary>The header width the bar takes, its buttons and the gaps between them.</summary>
    public double Width
    {
        get
        {
            var buttons = Inline + (Overflow ? 1 : 0);
            return buttons == 0 ? 0 : (buttons * NotesLayout.ButtonSize) + ((buttons - 1) * NotesLayout.ButtonGap);
        }
    }
}

/// <summary>
/// [S-1363] The note widget's disclosure ladder (<see cref="Disclosure"/>, S-660).
/// <list type="number">
/// <item>The text — always, at least two lines, even at 120 × 120.</item>
/// <item>"Not saved" under the text, only while the note's last write failed ([S-1362]): no word or
/// character count (user requirement), and nothing while all is well.</item>
/// </list>
/// The formatting bar is not part of the body: it stands in the card's header while the note is
/// being edited ([S-3963], <see cref="HeaderBar"/>). The widget is the one note at every size: a
/// larger tile gives the text more room, never a note list beside it (switching notes is the
/// header's picker, [S-1364]).
/// </summary>
public static class NotesLayout
{
    /// <summary>Every button the bar can show.</summary>
    public const int AllButtons = 9;

    /// <summary>The bar's buttons, square and compact: they sit on the header's title line.</summary>
    public const double ButtonSize = 24;

    public const double ButtonGap = 2;

    /// <summary>Lines of text the editor reserves before anything else is shown.</summary>
    public const int MinTextLines = 2;

    /// <summary>The header's own inset on each side (<c>Fluent.HeaderMargin</c>).</summary>
    public const double HeaderInset = 16;

    /// <summary>The header's icon and the gap after it.</summary>
    public const double HeaderLeading = 24;

    /// <summary>The gap between the title and the bar.</summary>
    public const double TitleGap = 8;

    /// <summary>The note picker's chevron button and the gap before it.</summary>
    public const double PickerWidth = 24;

    /// <param name="area">The view's slot.</param>
    /// <param name="inset">The padding the view keeps on each side of its parts.</param>
    /// <param name="gap">The margin between parts.</param>
    /// <param name="lineHeight">One line of the note at its size.</param>
    /// <param name="statusHeight">One line of the status (Caption).</param>
    /// <param name="notSaved">The note's last write failed, which the status line says.</param>
    public static NotesLayoutPlan Plan(WidgetArea area, double inset, double gap, double lineHeight, double statusHeight, bool notSaved = false)
    {
        if (!area.IsMeasured)
        {
            return new NotesLayoutPlan(false, 0, MinTextLines);
        }

        var box = area.Deflate(inset, inset);
        var width = box.Width;

        var room = new Disclosure(width, box.Height, gap);
        room.Reserve(lineHeight * MinTextLines);

        var status = notSaved && room.Take(statusHeight);

        var lines = MinTextLines + Disclosure.WholeRows(room.Remaining, lineHeight);
        return new NotesLayoutPlan(status, width, lines);
    }

    /// <summary>
    /// [S-3963] What of the header a bar may take: all of it but the insets, the icon, the title's
    /// minimum (<see cref="HeaderFit.MinimumTitle"/>, so the card still says which note it is) and
    /// the picker.
    /// </summary>
    public static double HeaderReserve(bool picker)
        => (2 * HeaderInset) + HeaderLeading + HeaderFit.MinimumTitle + TitleGap + (picker ? PickerWidth : 0);

    /// <summary>
    /// [S-3963] The bar in a header <paramref name="cardWidth"/> wide: every button where they all fit;
    /// else as many as fit beside a "More" button that holds the rest; nothing where not even that
    /// fits (the keyboard shortcuts still work).
    /// </summary>
    public static NotesHeaderBar HeaderBar(double cardWidth, bool picker)
    {
        if (!double.IsFinite(cardWidth) || cardWidth <= 0)
        {
            return default;
        }

        var room = cardWidth - HeaderReserve(picker);
        var all = (AllButtons * ButtonSize) + ((AllButtons - 1) * ButtonGap);

        if (room >= all)
        {
            return new NotesHeaderBar(AllButtons, false);
        }

        if (room < ButtonSize)
        {
            return default;
        }

        var inline = Math.Clamp(Disclosure.WholeRows(room - ButtonSize - ButtonGap, ButtonSize, ButtonGap), 0, AllButtons - 1);
        return new NotesHeaderBar(inline, true);
    }
}
