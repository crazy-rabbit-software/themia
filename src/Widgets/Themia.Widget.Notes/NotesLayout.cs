using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Core.Notes;

/// <summary>What the note widget's body shows at one size.</summary>
/// <param name="TextWidth">The width the text has: the whole box at every size, never shared with a note list.</param>
/// <param name="TextLines">Whole lines of text the editor shows at least (for tests; the editor scrolls).</param>
public sealed record NotesLayoutPlan(double TextWidth, int TextLines);

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
/// [S-1363] The note widget's body: the text, and nothing else, at every size, from the view's top in
/// the card's inset (16 either side and under it, [S-6703]). A failed write is the header's problem
/// glyph ([S-1362]), never a line of the body. The formatting bar is not part of the body: it stands in the card's header while the note is
/// being edited ([S-3963], <see cref="HeaderBar"/>). The widget is the one note at every size: a
/// larger tile gives the text more room, never a note list beside it (switching notes is the
/// header's picker, [S-1364]).
/// </summary>
public static class NotesLayout
{
    /// <summary>Every button the bar can show.</summary>
    public const int AllButtons = 9;

    /// <summary>The bar's buttons: the header's buttons, 24, side by side on the header's title line ([S-6710]).</summary>
    public const double ButtonSize = FamilyShape.HeaderButton;

    public const double ButtonGap = 0;

    /// <summary>Lines of text the editor reserves before anything else is shown.</summary>
    public const int MinTextLines = 2;

    /// <summary>The header's own inset on each side.</summary>
    public const double HeaderInset = FamilySpace.Inset;

    /// <summary>The header's icon and the gap after it.</summary>
    public const double HeaderLeading = HeaderRules.Leading;

    /// <summary>The gap between the title and the bar.</summary>
    public const double TitleGap = FamilySpace.S2;

    /// <summary>The note picker's chevron button (side by side with the bar's).</summary>
    public const double PickerWidth = FamilyShape.HeaderButton;

    /// <summary>The rename box under the picker: room for a title of a few words.</summary>
    public const double RenameWidth = 200;

    /// <param name="area">The view's slot (the card less its header).</param>
    /// <param name="lineHeight">One line of the note (the Body line at the text size).</param>
    public static NotesLayoutPlan Plan(WidgetArea area, double lineHeight)
    {
        if (!area.IsMeasured)
        {
            return new NotesLayoutPlan(0, MinTextLines);
        }

        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var height = Math.Max(0, area.Height - FamilySpace.Inset);
        return new NotesLayoutPlan(width, Math.Max(MinTextLines, Disclosure.WholeRows(height, lineHeight)));
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
