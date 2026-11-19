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

    /// <summary>[S-5602] White: Explorer's desktop icons, whose highlight is a wash of white over the wallpaper.</summary>
    Light,

    /// <summary>[S-13019] Black: the desktop icon's wash on a light ground (a fence on a light card), where white would not show.</summary>
    Dark,
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
/// [S-6627] The numbers of one selection look: the fill's and the border's opacity of the highlight in
/// each state, the corner radius and the border's width (DIPs). <see cref="SelectionLook.Rows"/> is the
/// file widgets' (a folder's rows and tiles); <see cref="SelectionLook.DesktopIcon"/> the desktop icon's,
/// the one place its highlight is tuned against Explorer's.
/// </summary>
public sealed record SelectionPalette(
    double HoverFill,
    double HoverStroke,
    double SelectedFill,
    double SelectedStroke,
    double SelectedHoverFill,
    double SelectedHoverStroke,
    double InactiveFill,
    double InactiveStroke,
    double InactiveHoverFill,
    double InactiveHoverStroke,
    double FocusStroke,
    double Radius,
    double Border)
{
    /// <summary>The colour the mark is drawn in while the widget has the keyboard, and under the pointer.</summary>
    public SelectionTone Tone { get; init; } = SelectionTone.Highlight;

    /// <summary>The colour of a selection while the widget does not have the keyboard.</summary>
    public SelectionTone InactiveTone { get; init; } = SelectionTone.Neutral;
}

/// <summary>
/// [S-6627] The one look of a selected item, the Windows 11 desktop's: a translucent fill of the
/// system highlight colour with a 1-DIP border of it, stronger for a selection than for a hover; a
/// neutral grey while the widget does not have the keyboard; the border alone for the focused item
/// after keyboard use. The opacities are here and only here; <c>SelectionChrome</c> (SDK WinUI)
/// turns them into brushes over the highlight colour Windows reports.
/// </summary>
public static class SelectionLook
{
    /// <summary>[S-5545] [S-5547] A cut item, or a hidden one shown because Explorer shows hidden items: its icon faded, as Explorer draws it.</summary>
    public const double Faded = 0.45;

    /// <summary>[S-5549] How much of the dragged desktop icons shows in the picture that follows the pointer, as Explorer's see-through drag image.</summary>
    public const double DragOpacity = 0.65;

    /// <summary>
    /// [S-13974] How much of the name's shadow colour lies under icon only's name shown on hover, which is drawn over the
    /// neighbouring icons: enough that its light text reads over any icon or wallpaper, while the icons under it still show.
    /// </summary>
    public const double NamePlate = 0.72;

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

    /// <summary>[S-5602] The desktop icon's wash: white, as Explorer's desktop draws it.</summary>
    public static readonly RgbColor White = new(0xFF, 0xFF, 0xFF);

    /// <summary>[S-13019] The desktop icon's wash on a light ground.</summary>
    public static readonly RgbColor Black = new(0x00, 0x00, 0x00);

    /// <summary>The inactive selection's grey: mid-grey reads as grey over a light card and a dark one.</summary>
    public static readonly RgbColor NeutralGrey = new(0x80, 0x80, 0x80);

    /// <summary>[S-6627] The file widgets' look (a folder's rows and tiles): the constants above, the family's radius and hairline.</summary>
    public static SelectionPalette Rows { get; } = new(
        HoverFill, HoverStroke, SelectedFill, SelectedStroke, SelectedHoverFill, SelectedHoverStroke,
        InactiveFill, InactiveStroke, InactiveHoverFill, InactiveHoverStroke, FocusStroke,
        Themia.Sdk.Kit.FamilyShape.Radius, Themia.Sdk.Kit.FamilyShape.Hairline);

    /// <summary>
    /// [S-5602] The desktop icon's look, round the whole cell: hover, selected, selected under the pointer,
    /// the inactive selection, the focus ring, the corner radius and the border. Every number of the
    /// desktop icon's highlight is here and nowhere else, to be tuned against Explorer's desktop without
    /// touching the file widgets' rows.
    /// <para>
    /// Measured on Windows 11's own desktop (build 26300, dark, 150 %, 2026-11-14) from lossless captures,
    /// inside against just outside the item's edge: a wash of **white**, never the highlight colour: 13 %
    /// under the pointer, 27 % selected, 13 % selected while another window has the keyboard; no border
    /// of another strength (the edge is the fill's own, so each stroke equals its fill), corners of about
    /// 2 DIPs. Selected under the pointer (33 %) and an inactive selection under it (18 %) could not be
    /// captured cleanly and are estimates one step above their rest. The focus ring is Themia's.
    /// </para>
    /// </summary>
    public static SelectionPalette DesktopIcon { get; } = new(
        HoverFill: 0.13, HoverStroke: 0.13,
        SelectedFill: 0.27, SelectedStroke: 0.27,
        SelectedHoverFill: 0.33, SelectedHoverStroke: 0.33,
        InactiveFill: 0.13, InactiveStroke: 0.13,
        InactiveHoverFill: 0.18, InactiveHoverStroke: 0.18,
        FocusStroke: FocusStroke,
        Radius: 2,
        Border: Themia.Sdk.Kit.FamilyShape.Hairline)
    {
        Tone = SelectionTone.Light,
        InactiveTone = SelectionTone.Light,
    };

    /// <summary>
    /// [S-13019] The desktop icon's look on a light ground: every number of <see cref="DesktopIcon"/> (the strengths, the
    /// whole cell, the corners), its wash black instead of white, so a fence's items on a light card are highlighted
    /// exactly as the free icons are on the wallpaper, only seen.
    /// </summary>
    public static SelectionPalette DesktopIconOnLight { get; } = DesktopIcon with { Tone = SelectionTone.Dark, InactiveTone = SelectionTone.Dark };

    /// <summary>[S-13019] The desktop icon's look for a ground: white over a dark one (the wallpaper, a dark card), black over a light one.</summary>
    public static SelectionPalette DesktopIconOver(bool lightGround) => lightGround ? DesktopIconOnLight : DesktopIcon;

    /// <summary>[S-13019] Whether a ground whose text is <paramref name="text"/> is light: its text is dark (relative luminance under a half).</summary>
    public static bool LightGround(RgbColor text) => ((0.2126 * text.R) + (0.7152 * text.G) + (0.0722 * text.B)) / 255.0 < 0.5;

    /// <summary>[S-6624] The band's mark.</summary>
    public static SelectionInk Band { get; } = new(SelectionTone.Highlight, BandFill, BandStroke);

    /// <summary>[S-6627] The mark of an item in <paramref name="cue"/>, in the file widgets' look.</summary>
    public static SelectionInk Ink(SelectionCue cue) => Ink(cue, Rows);

    /// <summary>[S-6627] The mark of an item in <paramref name="cue"/>, in <paramref name="look"/>.</summary>
    public static SelectionInk Ink(SelectionCue cue, SelectionPalette look)
    {
        ArgumentNullException.ThrowIfNull(look);

        // A drop target reads as the selection under the pointer, as Explorer lights a folder a drag is over.
        if (cue.DropTarget)
        {
            return new(look.Tone, look.SelectedHoverFill, look.SelectedHoverStroke);
        }

        if (cue.Selected)
        {
            return cue.Active
                ? cue.Hovered
                    ? new(look.Tone, look.SelectedHoverFill, look.SelectedHoverStroke)
                    : new(look.Tone, look.SelectedFill, look.SelectedStroke)
                : cue.Hovered
                    ? new(look.InactiveTone, look.InactiveHoverFill, look.InactiveHoverStroke)
                    : new(look.InactiveTone, look.InactiveFill, look.InactiveStroke);
        }

        // The focus rectangle shows only after keyboard use, and only while the widget has the keyboard.
        var focusShown = cue.Focused && cue.Cues && cue.Active;

        if (cue.Hovered)
        {
            return new(look.Tone, look.HoverFill, focusShown ? look.FocusStroke : look.HoverStroke);
        }

        return focusShown ? new(look.Tone, 0, look.FocusStroke) : new(SelectionTone.None, 0, 0);
    }

    /// <summary>[S-6627] The colour of <paramref name="tone"/>, given the highlight Windows reports.</summary>
    public static RgbColor Colour(SelectionTone tone, RgbColor highlight) => tone switch
    {
        SelectionTone.Neutral => NeutralGrey,
        SelectionTone.Light => White,
        SelectionTone.Dark => Black,
        _ => highlight,
    };

    /// <summary>
    /// [S-6629] Whether a selected tile's name is shown in full over what is below it, as the desktop
    /// shows the label of its focused, selected icon: only for that one item, only when it is cut
    /// short, only while the widget has the keyboard, and never while it is being renamed.
    /// </summary>
    public static bool ShowsFullName(bool selected, bool focused, bool active, bool trimmed, bool renaming)
        => selected && focused && active && trimmed && !renaming;
}
