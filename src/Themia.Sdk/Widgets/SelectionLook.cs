namespace Themia.Core.Widgets;

/// <summary>[S-6627] Which colour a selection mark is drawn in.</summary>
public enum SelectionTone
{
    /// <summary>Nothing drawn: an item at rest.</summary>
    None,

    /// <summary>The system highlight colour (Windows' <c>COLOR_HIGHLIGHT</c>), as the desktop's icons.</summary>
    Highlight,

    /// <summary>A neutral grey: a selection in a widget that does not have the keyboard.</summary>
    Neutral,
}

/// <summary>[S-6627] A selection mark: its tone, and the fill's and the 1-DIP border's opacity of it (0..1).</summary>
public readonly record struct SelectionInk(SelectionTone Tone, double Fill, double Stroke);

/// <summary>
/// [S-6627] What an item is, for its selection mark: selected, under the pointer, focused (the keys'
/// item), whether the focus is shown (<see cref="ItemSelection.Cues"/>), whether the widget has the
/// keyboard, and whether a drag is over it as a drop target.
/// </summary>
public readonly record struct SelectionCue(
    bool Selected,
    bool Hovered,
    bool Focused = false,
    bool Cues = false,
    bool Active = true,
    bool DropTarget = false);

/// <summary>
/// [S-6627] The one look of a selected item, the Windows 11 desktop's: a translucent fill of the
/// system highlight colour with a 1-DIP border of it, stronger for a selection than for a hover; a
/// neutral grey while the widget does not have the keyboard; the border alone for the focused item
/// after keyboard use. The opacities are here and only here; <c>SelectionChrome</c> (SDK WinUI)
/// turns them into brushes over the highlight colour Windows reports.
/// </summary>
public static class SelectionLook
{
    public const double HoverFill = 0.12;
    public const double HoverStroke = 0.35;
    public const double SelectedFill = 0.32;
    public const double SelectedStroke = 0.65;
    public const double SelectedHoverFill = 0.40;
    public const double SelectedHoverStroke = 0.75;
    public const double InactiveFill = 0.28;
    public const double InactiveStroke = 0.42;
    public const double InactiveHoverFill = 0.34;
    public const double InactiveHoverStroke = 0.50;
    public const double FocusStroke = 0.65;

    /// <summary>[S-6624] The rubber band: the highlight at this fill under a solid border of it.</summary>
    public const double BandFill = 0.28;

    public const double BandStroke = 1.0;

    /// <summary>Windows' default highlight (<c>COLOR_HIGHLIGHT</c>), for when the system cannot be asked.</summary>
    public static readonly RgbColor DefaultHighlight = new(0x00, 0x78, 0xD7);

    /// <summary>The inactive selection's grey: mid-grey reads as grey over a light card and a dark one.</summary>
    public static readonly RgbColor NeutralGrey = new(0x80, 0x80, 0x80);

    /// <summary>[S-6624] The band's mark.</summary>
    public static SelectionInk Band { get; } = new(SelectionTone.Highlight, BandFill, BandStroke);

    /// <summary>[S-6627] The mark of an item in <paramref name="cue"/>.</summary>
    public static SelectionInk Ink(SelectionCue cue)
    {
        // A drop target reads as the selection under the pointer, as Explorer lights a folder a drag is over.
        if (cue.DropTarget)
        {
            return new(SelectionTone.Highlight, SelectedHoverFill, SelectedHoverStroke);
        }

        if (cue.Selected)
        {
            return cue.Active
                ? cue.Hovered
                    ? new(SelectionTone.Highlight, SelectedHoverFill, SelectedHoverStroke)
                    : new(SelectionTone.Highlight, SelectedFill, SelectedStroke)
                : cue.Hovered
                    ? new(SelectionTone.Neutral, InactiveHoverFill, InactiveHoverStroke)
                    : new(SelectionTone.Neutral, InactiveFill, InactiveStroke);
        }

        // The focus rectangle shows only after keyboard use, and only while the widget has the keyboard.
        var focusShown = cue.Focused && cue.Cues && cue.Active;

        if (cue.Hovered)
        {
            return new(SelectionTone.Highlight, HoverFill, focusShown ? FocusStroke : HoverStroke);
        }

        return focusShown ? new(SelectionTone.Highlight, 0, FocusStroke) : new(SelectionTone.None, 0, 0);
    }

    /// <summary>[S-6627] The colour of <paramref name="tone"/>, given the highlight Windows reports.</summary>
    public static RgbColor Colour(SelectionTone tone, RgbColor highlight) => tone == SelectionTone.Neutral ? NeutralGrey : highlight;

    /// <summary>
    /// [S-6629] Whether a selected tile's name is shown in full over what is below it, as the desktop
    /// shows the label of its focused, selected icon: only for that one item, only when it is cut
    /// short, only while the widget has the keyboard, and never while it is being renamed.
    /// </summary>
    public static bool ShowsFullName(bool selected, bool focused, bool active, bool trimmed, bool renaming)
        => selected && focused && active && trimmed && !renaming;
}
