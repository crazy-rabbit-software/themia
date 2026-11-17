using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Notes;

/// <summary>
/// [S-3963] [S-12481] The formatting bar on the note's own first line: how many buttons stand in it, and
/// whether the rest are behind a "More" button.
/// </summary>
/// <param name="Inline">Buttons shown on the line, in rank order (most used first).</param>
/// <param name="Overflow">A "More" button holds the buttons that did not fit.</param>
public readonly record struct NotesBar(int Inline, bool Overflow)
{
    /// <summary>Anything of the bar is on the line.</summary>
    public bool Shown => Inline > 0 || Overflow;

    /// <summary>The line's width the bar takes, its buttons and the gaps between them.</summary>
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
/// [S-1363] The note widget's body: its tool line ([S-12481]: the problem glyph, the formatting bar while the
/// note is being edited, the picker), then the text and nothing else, at every size, in the card's inset (12
/// either side and under it, [S-6703]). A failed write is the line's problem glyph ([S-1362]), never a line of
/// the text. The widget is the one note at every size: a larger tile gives the text more room, never a note
/// list beside it (switching notes is the line's picker, [S-1364]).
/// </summary>
public static class NotesLayout
{
    /// <summary>Every button the bar can show.</summary>
    public const int AllButtons = 9;

    /// <summary>The bar's buttons: line buttons, 24, side by side on the note's first line ([S-6710]).</summary>
    public const double ButtonSize = FamilyShape.LineButton;

    public const double ButtonGap = 0;

    /// <summary>[S-12481] The line's height and the gap under it, over the text: there whether or not the bar is.</summary>
    public const double LineHeight = FamilySpace.HeaderLine + FamilySpace.HeaderGap;

    /// <summary>The gap between the bar and the picker.</summary>
    public const double PickerGap = FamilySpace.S2;

    /// <summary>The note picker's chevron button (side by side with the bar's).</summary>
    public const double PickerWidth = FamilyShape.LineButton;

    /// <summary>
    /// [S-11206] The note's sizes: the text alone, a free range 160..600 × 100..600 (a new one 300 × 220).
    /// The text wraps and scrolls; nothing else appears with the size.
    /// </summary>
    public static WidgetSizing Sizing { get; } = WidgetSizing.Range(new(160, 100), new(300, 220), new(600, 600));

    /// <summary>The rename box under the picker: room for a title of a few words.</summary>
    public const double RenameWidth = 200;

    /// <summary>[S-3963] What of the line a bar may not take: the insets and the picker with its gap.</summary>
    public static double Reserve(bool picker)
        => (2 * FamilySpace.Inset) + (picker ? PickerWidth + PickerGap : 0);

    /// <summary>
    /// [S-3963] The bar on a line <paramref name="cardWidth"/> wide: every button where they all fit;
    /// else as many as fit beside a "More" button that holds the rest; nothing where not even that
    /// fits (the keyboard shortcuts still work).
    /// </summary>
    public static NotesBar Bar(double cardWidth, bool picker)
    {
        if (!double.IsFinite(cardWidth) || cardWidth <= 0)
        {
            return default;
        }

        var room = cardWidth - Reserve(picker);
        var all = (AllButtons * ButtonSize) + ((AllButtons - 1) * ButtonGap);

        if (room >= all)
        {
            return new NotesBar(AllButtons, false);
        }

        if (room < ButtonSize)
        {
            return default;
        }

        var inline = Math.Clamp((int)Math.Floor((room - ButtonSize + LayoutMath.Tolerance) / (ButtonSize + ButtonGap)), 0, AllButtons - 1);
        return new NotesBar(inline, true);
    }
}
