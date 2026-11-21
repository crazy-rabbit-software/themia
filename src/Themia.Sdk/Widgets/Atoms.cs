using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-15100] The universal spacing (owner, 2026-11-21: "the spacing inside the atoms should be universal and the same for
/// all the widgets (also buttons and text), that way it should look good in the composition"): the one distance an atom
/// keeps between its card's edge and its content, on every side, whatever the atom is (a text, a button, a bar, a
/// picture). Two atoms side by side therefore stand their contents two of it apart, and every content edge of a
/// composition lies on the same lines.
/// </summary>
public static class AtomSpace
{
    /// <summary>[S-15100] The universal spacing: 8, on the family's scale.</summary>
    public const double Pad = FamilySpace.S2;
}

/// <summary>[S-15100] An atom's room: its card's box less the universal spacing on every side, never negative.</summary>
public readonly record struct AtomRoom(double Width, double Height)
{
    /// <summary>Whether there is anything to draw in.</summary>
    public bool IsUsable => Width > 0 && Height > 0;
}

/// <summary>
/// [S-15100]..[S-15105] The atoms' rules, pure: one small widget a part of a composition (a now-playing group's title, its
/// buttons, its cover, its bar), every one drawn in its card's box less the universal spacing (<see cref="AtomSpace.Pad"/>)
/// and filling that room: a text's capitals, standing on the baseline, a glyph's ink, a bar, a picture. The card's size is the content's size: a text never shrinks to show more of itself, it is cut at the room's
/// end with its ellipsis.
/// </summary>
public static class AtomRules
{
    /// <summary>
    /// [S-15101] How tall the family's face (Segoe UI Variable) draws its capitals and figures, a fraction of the font
    /// size (<see cref="LineInk.FaceCap"/>): what fills a text atom's room.
    /// </summary>
    public const double Cap = LineInk.FaceCap;

    /// <summary>
    /// [S-15101] How far the face inks under its baseline, a fraction of the font size: the foot of a "g", a "y", a "p".
    /// Measured on the renders.
    /// </summary>
    public const double Descent = 0.24;

    /// <summary>[S-15101] The letter a text atom is sized and placed by, whatever it says: a capital, standing on the baseline.</summary>
    public const string Capital = "H";

    /// <summary>[S-15101] The letters that say how far the face inks under its baseline at a size: a capital beside its deepest plain letters.</summary>
    public const string Deep = "Hgjpqy";

    /// <summary>
    /// [S-15101] The line a text atom is set on, a multiple of its font size: its baseline (<see cref="LineInk.FaceBaseline"/>
    /// of the line) then stands exactly the font size under the line's top, and its descenders inside the line.
    /// </summary>
    public const double Line = 1.25;

    /// <summary>[S-15101] The smallest text an atom draws: the legibility floor (WR-14).</summary>
    public const double LeastFont = 9;

    /// <summary>[S-15102] A new text's room: capitals of 14, a text of about 20 (a title, an artist, a time alike; a taller card is a larger text).</summary>
    public const double TextRoom = 14;

    /// <summary>[S-15102] A new button's glyph: its room's side.</summary>
    public const double GlyphInk = 24;

    /// <summary>[S-15102] A new bar's thickness: the family's track.</summary>
    public const double BarInk = FamilyShape.Track;

    /// <summary>[S-15100] The room of a card <paramref name="width"/> by <paramref name="height"/>: the box less the spacing on every side.</summary>
    public static AtomRoom Room(double width, double height)
    {
        static double Axis(double size) => double.IsFinite(size) ? Math.Max(0, size - (2 * AtomSpace.Pad)) : 0;
        return new AtomRoom(Axis(width), Axis(height));
    }

    /// <summary>[S-15100] A card's side for a content of <paramref name="ink"/>: the content in the spacing.</summary>
    public static double Side(double ink) => ink + (2 * AtomSpace.Pad);

    /// <summary>
    /// [S-15101] The font size of a text that fills a room <paramref name="height"/> tall: its capitals are the room's
    /// height and it stands on its baseline at the room's foot (owner, 2026-11-21: "the text feels as if it has too much
    /// space at the bottom. its because of letters like y for example that take up space further down"; asked which fit:
    /// "C: capitals fill"), its descenders hanging into the spacing under it, never past the card's edge
    /// (<see cref="Held"/>). The width never makes it smaller (owner: "the text should rather overflow ... than to shrink
    /// and not fill the height up to the spacing").
    /// </summary>
    public static double FontSize(double height)
    {
        if (!(height > 0) || !double.IsFinite(height))
        {
            return 0;
        }

        var font = height / Cap;
        return Held(font, height, font * Descent, height);
    }

    /// <summary>
    /// [S-15101] <paramref name="fontSize"/>, at which the face draws capitals <paramref name="cap"/> tall and inks
    /// <paramref name="descent"/> under its baseline, held so that its descenders stay in the card: in a room
    /// <paramref name="height"/> tall they may hang into the spacing under it (<see cref="AtomSpace.Pad"/>) and no
    /// further. A text taller than that is drawn at the size at which its capitals' top is the room's top and its
    /// descenders' foot the card's edge.
    /// </summary>
    public static double Held(double fontSize, double cap, double descent, double height)
        => fontSize > 0 && cap > 0 && descent > AtomSpace.Pad && height > 0 ? fontSize * (height + AtomSpace.Pad) / (cap + descent) : fontSize;

    /// <summary>[S-15101] The line height of a text atom at <paramref name="fontSize"/>.</summary>
    public static double LineHeight(double fontSize) => fontSize * Line;

    /// <summary>
    /// [S-15101] How far a text atom's line stands over its room's top, so that its capitals' top is the room's top: the
    /// line's room over them (the model's; a view that can measure the face asks the meter instead).
    /// </summary>
    public static double Lift(double fontSize) => (LineHeight(fontSize) * LineInk.FaceBaseline) - (fontSize * Cap);

    /// <summary>
    /// [S-15101] The font size at which a text measured at <paramref name="fontSize"/> to ink <paramref name="ink"/> tall
    /// (its capitals) inks exactly a room <paramref name="height"/> tall: ink is proportional to the size.
    /// </summary>
    public static double Refit(double fontSize, double ink, double height)
        => ink > 0 && fontSize > 0 && height > 0 && double.IsFinite(ink) ? fontSize * height / ink : fontSize;

    /// <summary>
    /// [S-15103] The em size of a glyph whose ink fills <paramref name="room"/>: its ink is <paramref name="inkWidth"/> by
    /// <paramref name="inkHeight"/> of its em box (fractions; 1: the whole box), scaled until the room's height or its
    /// width stops it.
    /// </summary>
    public static double GlyphSize(AtomRoom room, double inkWidth, double inkHeight)
    {
        if (!room.IsUsable)
        {
            return 0;
        }

        var w = inkWidth > 0 && inkWidth <= 1 ? inkWidth : 1;
        var h = inkHeight > 0 && inkHeight <= 1 ? inkHeight : 1;
        return Math.Min(room.Width / w, room.Height / h);
    }

    /// <summary>
    /// [S-15103] Where a content <paramref name="size"/> long starts in a room <paramref name="room"/> long, from the room's
    /// start: at the start, the middle or the end.
    /// </summary>
    public static double Across(Model.ContentAlign align, double room, double size) => align switch
    {
        Model.ContentAlign.Center => (room - size) / 2,
        Model.ContentAlign.Right => room - size,
        _ => 0,
    };

    /// <summary>[S-15104] A bar's corner radius in <paramref name="room"/>: round ends, half its short side.</summary>
    public static double BarRadius(AtomRoom room) => Math.Max(0, Math.Min(room.Width, room.Height) / 2);

    /// <summary>[S-15104] How much of a bar <paramref name="width"/> long is filled at <paramref name="fraction"/> (0..1).</summary>
    public static double BarFill(double fraction, double width)
        => width > 0 && double.IsFinite(fraction) ? Math.Clamp(fraction, 0, 1) * width : 0;

    /// <summary>
    /// [S-15102] A text atom's card ([S-2438]): a new one <paramref name="width"/> wide and its <paramref name="ink"/> in
    /// the spacing tall; down to the legibility floor's ink in the spacing and a few letters wide; the descriptor relaxes
    /// it ([S-12411]): a taller card is a larger text.
    /// </summary>
    public static WidgetSizing TextSizing(double width, double ink)
    {
        var least = Math.Ceiling(Side(LeastFont * Cap));
        var natural = new SizeDip(width, Side(ink));
        return WidgetSizing.Range(new SizeDip(Math.Min(width, Side(3 * LeastFont)), least), natural, natural);
    }

    /// <summary>[S-15102] A bar atom's card: a new one <paramref name="width"/> wide and the track in the spacing tall; down to a hairline's pair in the spacing.</summary>
    public static WidgetSizing BarSizing(double width)
    {
        var least = new SizeDip(Side(3 * LeastFont), Side(FamilyShape.Stroke));
        var natural = new SizeDip(width, Side(BarInk));
        return WidgetSizing.Range(least, natural, natural) with { Room = StretchRoom.Picture };
    }

    /// <summary>[S-15102] A picture atom's card: a new one a square of <paramref name="side"/>, down to the grid's least widget.</summary>
    public static WidgetSizing PictureSizing(double side)
    {
        var least = new SizeDip(WidgetSizing.LeastScaled, WidgetSizing.LeastScaled);
        var natural = new SizeDip(side, side);
        return WidgetSizing.Range(least, natural, natural) with { Room = StretchRoom.Picture };
    }
}
