using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Notes;

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
/// the card's inset (12 either side and under it, [S-6703]). A failed write is the header's problem
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

    /// <summary>The header's own inset on each side.</summary>
    public const double HeaderInset = FamilySpace.Inset;

    /// <summary>The header's icon and the gap after it.</summary>
    public const double HeaderLeading = HeaderRules.Leading;

    /// <summary>The gap between the title and the bar.</summary>
    public const double TitleGap = FamilySpace.S2;

    /// <summary>The note picker's chevron button (side by side with the bar's).</summary>
    public const double PickerWidth = FamilyShape.HeaderButton;

    /// <summary>
    /// [S-11206] The note's sizes: the text alone, a free range 160..600 × 100..600 (a new one 300 × 220).
    /// The text wraps and scrolls; nothing else appears with the size.
    /// </summary>
    public static WidgetSizing Sizing { get; } = WidgetSizing.Range(new(160, 100), new(300, 220), new(600, 600));

    /// <summary>The rename box under the picker: room for a title of a few words.</summary>
    public const double RenameWidth = 200;

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

        var inline = Math.Clamp((int)Math.Floor((room - ButtonSize + LayoutMath.Tolerance) / (ButtonSize + ButtonGap)), 0, AllButtons - 1);
        return new NotesHeaderBar(inline, true);
    }
}
