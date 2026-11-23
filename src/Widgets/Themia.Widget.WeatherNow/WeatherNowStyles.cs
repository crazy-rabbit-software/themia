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
    /// [S-16447] The face with no reading in <paramref name="style"/> (WR-10: the face kept, its reading "--"; WR-7: the
    /// reading and its line one block): the unknown sky where the style draws one (leading the reading in Standard, beside it
    /// at 48 in Large icon), "--" held to the style's widest reading so the block is planned and scaled as a reading's, the
    /// why its line under.
    /// </summary>
    public static UnreadFace Unread(WeatherNowStyle style) => style switch
    {
        WeatherNowStyle.Minimal => new UnreadFace(false, false, Bare),
        WeatherNowStyle.LargeIcon => new UnreadFace(false, true, Beside),
        _ => new UnreadFace(true, false, Worst),
    };

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

/// <summary>[S-16447] The face with no reading: the sky leading the reading, the 48 sky beside it, the reading's widest form.</summary>
public readonly record struct UnreadFace(bool Lead, bool BigGlyph, ReadoutText Held);

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
