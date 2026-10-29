namespace Themia.Sdk.Kit;

/// <summary>
/// [S-6703] The family's space: one 4-based scale, 4 · 8 · 12 · 16 · 24, and the insets every widget
/// shares, which are sums of it. A card is inset 16 on every side at every tier; the header is one
/// 20 line at 16, then 8 down to the content, so the content starts at 44; a glyph is 8 from its
/// text, a name 12 from its value (and a column from the next).
/// <para>
/// Today's views drew with 37 paddings and 416 margins; most were a token plus or minus a few pixels
/// to nudge a part into line. These are all there is: a part that does not line up on them is laid
/// out wrongly, not nudged.
/// </para>
/// </summary>
public static class FamilySpace
{
    public const double S1 = 4;

    public const double S2 = 8;

    public const double S3 = 12;

    public const double S4 = 16;

    public const double S6 = 24;

    /// <summary>The scale, smallest first (0 included: no gap is a gap too).</summary>
    public static IReadOnlyList<double> Scale { get; } = [0, S1, S2, S3, S4, S6];

    /// <summary>The card's edge to its content, every side, every tier.</summary>
    public const double Inset = S4;

    /// <summary>The header's one line (BodyStrong).</summary>
    public const double HeaderLine = 20;

    /// <summary>From the header's line down to the content.</summary>
    public const double HeaderGap = S2;

    /// <summary>The content's top under a header: inset, the header's line, the gap.</summary>
    public const double ContentTop = Inset + HeaderLine + HeaderGap;

    /// <summary>A glyph to its text.</summary>
    public const double GlyphGap = S2;

    /// <summary>A name to its value, and a column to the next.</summary>
    public const double ColumnGap = S3;

    /// <summary>
    /// The widget's one text edge, from the card's edge, when its rows lead with a 16 glyph: the inset,
    /// the glyph, its gap (where the header's name starts too). Without a lead it is <see cref="Inset"/>.
    /// </summary>
    public const double TextEdge = Inset + FamilyShape.Icon + GlyphGap;

    /// <summary>
    /// How far a 24 header button stands over the header's 20 line either way: its margin, so the
    /// header stays one line and its name does not move ([S-6710]).
    /// </summary>
    public const double ButtonOverflow = (FamilyShape.HeaderButton - HeaderLine) / 2;

    /// <summary>
    /// The lengths derived from the scale that a layout may use as a padding, margin or spacing: the
    /// scale itself, the content top, the text edge, the header line and a header button's overflow. A render walk holds every
    /// padding to these ([S-6731]).
    /// </summary>
    public static IReadOnlyList<double> Derived { get; } = [ContentTop, TextEdge, HeaderLine, ButtonOverflow];

    /// <summary>Whether <paramref name="length"/> (either sign) is on the scale or one of its derived insets.</summary>
    public static bool IsSpace(double length)
    {
        var size = Math.Abs(length);
        foreach (var step in Scale)
        {
            if (Math.Abs(step - size) < 0.01)
            {
                return true;
            }
        }

        foreach (var step in Derived)
        {
            if (Math.Abs(step - size) < 0.01)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// [S-6704] The family's shapes and part sizes: rows 32 · 48 · 64 (one, two, three lines) at every
/// tier; icons 16 (header, rows, status, inline), 32 (an avatar, an app or file icon, a cell) and 48
/// (a large cell); one radius, 4, for every inner rectangle, a pill, dot, ring or track being round
/// (half its short side); one stroke, 2 (chart lines, rings), a 16 % wash under a chart line; one
/// track, 4 (a meter's bar, a range, a slider's rail); a hairline 1. Buttons: <see cref="Button"/>
/// 32 tall with 12 either side, <see cref="IconButton"/> 32 × 32 in the content,
/// <see cref="HeaderButton"/> 24 × 24 in the header, each with a 16 glyph. The card's own radius is
/// the design's setting, not a token.
/// </summary>
public static class FamilyShape
{
    /// <summary>A row of one line.</summary>
    public const double Row1 = 32;

    /// <summary>A row of two lines (a Caption under the name).</summary>
    public const double Row2 = 48;

    /// <summary>A row of three lines.</summary>
    public const double Row3 = 64;

    /// <summary>The glyph in a header, a row, a status and inline.</summary>
    public const double Icon = 16;

    /// <summary>A row's lead when it is an avatar, an app icon or a file icon, and a cell's icon.</summary>
    public const double IconLarge = 32;

    /// <summary>A cell's icon on a large tile.</summary>
    public const double IconHuge = 48;

    /// <summary>The icon sizes, smallest first.</summary>
    public static IReadOnlyList<double> Icons { get; } = [Icon, IconLarge, IconHuge];

    /// <summary>Every inner rectangle's radius: buttons, chips, row fills, bars.</summary>
    public const double Radius = 4;

    /// <summary>A chart's line and a ring.</summary>
    public const double Stroke = 2;

    /// <summary>A meter's bar, a range bar, a slider's rail.</summary>
    public const double Track = 4;

    /// <summary>A rule, a tick, a card's or a control's stroke.</summary>
    public const double Hairline = 1;

    /// <summary>The alpha of the wash under a chart line, on the shape, never on the shared brush.</summary>
    public const double Wash = 0.16;

    /// <summary>A text button's height (Body text, 12 either side).</summary>
    public const double Button = 32;

    /// <summary>A text button's side padding.</summary>
    public const double ButtonPadding = 12;

    /// <summary>An icon button in the content (transport, steppers): square.</summary>
    public const double IconButton = 32;

    /// <summary>An icon button in the header's trailing slot: square, as tall as fits the header's line.</summary>
    public const double HeaderButton = 24;

    /// <summary>A count pill's height (a Caption line) and its side padding.</summary>
    public const double PillHeight = 16;

    public const double PillPadding = 6;

    /// <summary>The height of a row of <paramref name="lines"/> lines (1 to 3; anything else is clamped).</summary>
    public static double RowHeight(int lines) => Math.Clamp(lines, 1, 3) switch
    {
        1 => Row1,
        2 => Row2,
        _ => Row3,
    };

    /// <summary>The radius that makes a <paramref name="width"/> × <paramref name="height"/> part round.</summary>
    public static double Round(double width, double height) => Math.Max(0, Math.Min(width, height) / 2);

    /// <summary>
    /// [S-6704] Whether a corner of <paramref name="radius"/> on a part of that size is the family's: none,
    /// the one radius, or round (half the short side, within half a pixel).
    /// </summary>
    public static bool IsRadius(double radius, double width, double height)
        => radius < 0.01
            || Math.Abs(radius - Radius) < 0.01
            || (width > 0 && height > 0 && Math.Abs(radius - Round(width, height)) <= 0.5);

    /// <summary>[S-6704] Whether a shape's stroke is the family's: none, the hairline, the stroke or the track.</summary>
    public static bool IsStroke(double thickness)
        => thickness < 0.01 || Math.Abs(thickness - Hairline) < 0.01 || Math.Abs(thickness - Stroke) < 0.01 || Math.Abs(thickness - Track) < 0.01;

    /// <summary>
    /// [S-6704] Whether a glyph's size is the family's: an icon size, or a Readout step (a Hero step
    /// only for a one-readout widget), the sizes a big glyph beside a number takes.
    /// </summary>
    public static bool IsGlyph(double size, bool oneReadout)
    {
        foreach (var icon in Icons)
        {
            if (Math.Abs(icon - size) < 0.01)
            {
                return true;
            }
        }

        return size > FamilyType.TitleSize && FamilyType.IsSize(size, oneReadout);
    }
}
