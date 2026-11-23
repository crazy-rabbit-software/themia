using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-723] [S-6161] [S-6162] [S-11101] The reading's three looks and their sizes: the temperature on the
/// family's 28 ([S-2449]: a data widget's number, never larger with the card), the condition the
/// Caption under it. Standard leads the temperature with the sky's glyph at its step; Large icon puts
/// the sky's 48 glyph beside the temperature and its condition; Minimal is the temperature alone. Each
/// is one height (nothing in the card can use more) and 140..320 wide: narrower and the widest
/// reading does not fit, wider is only empty.
/// </summary>
public static class WeatherNowStyles
{
    /// <summary>The widest reading the widget prints ("-18°F"), led by its glyph, its condition under it.</summary>
    public static readonly ReadoutText Worst = new("-18", "°F", Lead: true, LineUnder: true);

    /// <summary>The large-icon style's reading: the condition under it, the glyph beside, not leading.</summary>
    public static readonly ReadoutText Beside = new("-18", "°F", LineUnder: true);

    /// <summary>The minimal style's widest reading: the temperature alone.</summary>
    public static readonly ReadoutText Bare = new("-18", "°F");

    /// <summary>[S-6161] The sky's glyph beside the temperature: the family's large cell icon.</summary>
    public const double LargeGlyph = FamilyShape.IconHuge;

    public const double Narrowest = 140;

    public const double Widest = 320;

    /// <summary>[S-11101] The content's height for a style: the readout's block (the large icon's glyph where it is taller).</summary>
    public static double Content(WeatherNowStyle style) => style switch
    {
        WeatherNowStyle.Minimal => ReadoutRules.Height(FamilyType.Big28, Bare),
        WeatherNowStyle.LargeIcon => Math.Max(LargeGlyph, ReadoutRules.Height(FamilyType.Big28, Beside)),
        _ => ReadoutRules.Height(FamilyType.Big28, Worst),
    };

    /// <summary>[S-11101] A style's natural width.</summary>
    public static double NaturalWidth(WeatherNowStyle style) => style switch
    {
        WeatherNowStyle.LargeIcon => 200,
        WeatherNowStyle.Minimal => 140,
        _ => 180,
    };

    /// <summary>[S-11101] The reading's sizing: one height (the inset, the content, the inset), 140..320 wide.</summary>
    public static WidgetSizing Sizing(WeatherNowConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var height = FamilySpace.ContentTop + Content(config.Style) + FamilySpace.Inset;
        return WidgetSizing.Range(new(Narrowest, height), new(NaturalWidth(config.Style), height), new(Widest, height));
    }

    /// <summary>
    /// [S-12833] The reading's parts (<see cref="FaceParts"/>): the temperature its main reading, at 9 in its least card;
    /// the condition under it, its unit and its glyph give way on the way down (the large icon is larger than the reading
    /// and stays).
    /// </summary>
    public static FaceParts Face(WeatherNowConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var room = NaturalWidth(config.Style) - (2 * FamilySpace.Inset);
        return config.Style switch
        {
            WeatherNowStyle.Minimal => FaceParts.Readout(FamilyType.Big28, Bare),
            WeatherNowStyle.LargeIcon => FaceParts.Readout(FamilyType.Big28, Beside, room - LargeGlyph - FamilySpace.GlyphGap)
                .Led(FacePiece.Box(LargeGlyph + FamilySpace.GlyphGap, FamilyType.Big28.LineHeight)),
            _ => FaceParts.Readout(FamilyType.Big28, Worst, room),
        };
    }

    /// <summary>
    /// [S-16447] The face with no reading in <paramref name="style"/> (WR-10: the face kept, its reading "--"): the unknown sky
    /// where the style draws one (leading the reading in Standard, beside it at 48 in Large icon).
    /// </summary>
    public static UnreadFace Unread(WeatherNowStyle style) => style switch
    {
        WeatherNowStyle.Minimal => new UnreadFace(false, false),
        WeatherNowStyle.LargeIcon => new UnreadFace(false, true),
        _ => new UnreadFace(true, false),
    };

    /// <summary>
    /// [S-16455] What Minimal's "--" is held to (<c>ReadoutBlock.Hold</c>; the other styles stand on their sky's ink): two
    /// figures and the degree, about as wide as the dashes and as tall as a reading's, so the face fills its card as a
    /// reading's does (WR-1, WR-4), never blown up by the dashes' thin ink.
    /// </summary>
    public const string UnreadHold = "00";

    /// <summary>
    /// [S-16455] The ink height of the face with no reading at its own size (scale 1): the reading's row, its sky beside it
    /// at 48 in Large icon. The why's line stands under the face only where the face keeps at least this.
    /// </summary>
    public static double UnreadInk(WeatherNowStyle style)
    {
        var row = FaceParts.Readout(FamilyType.Big28, new ReadoutText("-18", "°F", Lead: style == WeatherNowStyle.Standard)).Plan(0).Ink;
        return style == WeatherNowStyle.LargeIcon ? Math.Max(LargeGlyph, row) : row;
    }

    /// <summary>
    /// [S-16455] Whether why stands as its compact line under the face (WR-10: "says why in a compact line"; WR-7: the face
    /// and its line one block): where the content room <paramref name="width"/> by <paramref name="height"/> holds the face
    /// at its own size (<see cref="UnreadInk"/>: the why never makes it smaller than that) over the line's band and a
    /// sentence's room across (<see cref="WhyLine.Fits"/>). Else why is the status mark beside the face, its tooltip saying
    /// it ([S-16456]).
    /// </summary>
    public static bool WhyUnder(WeatherNowConfig config, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(config);
        return WhyLine.Fits(width, height, UnreadInk(config.Style));
    }

    /// <summary>[S-16456] The status mark's room across beside the face: its glyph and the gap before it.</summary>
    public static double MarkRoom => FamilyShape.Icon + FamilySpace.S2;

    /// <summary>
    /// [S-16456] The least content box (no inset) of a style: its main reading alone at the legibility floor ([S-12833]: its
    /// unit, glyph and condition give way first, none drawn under 9) and the status mark's room beside it, kept at its start
    /// too so a centred reading stays centred ([S-16454]) (WR-10, WR-14: a stale reading or no reading says so by the mark
    /// there, never by nothing).
    /// </summary>
    public static SizeDip Least(WeatherNowConfig config)
    {
        var least = Face(config).LeastBox();
        return new SizeDip(least.Width + (2 * MarkRoom), least.Height);
    }

    /// <summary>[S-11101] [S-16456] The kind's sizing: its stated range scaling down to its least box in the card's inset.</summary>
    public static WidgetSizing Scaled(WeatherNowConfig config)
    {
        var least = Least(config);
        var inset = 2 * FamilySpace.Inset;
        return Sizing(config).ScalesDownTo(new SizeDip(least.Width + inset, least.Height + inset));
    }

    /// <summary>
    /// [S-16433] Where the status mark (its <paramref name="room"/> across, over a band <paramref name="band"/> under the face's
    /// top) stands against the reading's block (<paramref name="block"/> at its own size, <paramref name="least"/> at its
    /// floor) scaled into a room <paramref name="width"/> by <paramref name="height"/>: clear of a block that stands under the
    /// band (or with nothing shown), beside a block that would stand under it (the block ends its room sooner), or giving
    /// way (WR-14: never over the reading) where the room left beside it cannot hold the block at its floor.
    /// </summary>
    public static MarkPlace Mark(double width, double height, SizeDip block, SizeDip least, double room, double band)
    {
        if (!(room > 0) || !MetricRules.UnderMark(width, height, default, block, MetricArrangement.Stacked, 0, head: false, band))
        {
            return MarkPlace.Clear;
        }

        return width - room >= least.Width - LayoutMath.Tolerance ? MarkPlace.Beside : MarkPlace.Yields;
    }
}

/// <summary>[S-16447] The face with no reading: the sky leading the reading, or the 48 sky beside it.</summary>
public readonly record struct UnreadFace(bool Lead, bool BigGlyph);

/// <summary>[S-16433] Where the status mark stands against a reading's block (<see cref="WeatherNowStyles.Mark"/>).</summary>
public enum MarkPlace
{
    /// <summary>Over the top end corner with nothing of the block under it (or no mark).</summary>
    Clear,

    /// <summary>Beside the block: the block's room ends the mark's width sooner.</summary>
    Beside,

    /// <summary>Gone to the face's tooltip: the card is too small for the block and the mark.</summary>
    Yields,
}
