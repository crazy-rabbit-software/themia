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

    /// <summary>[S-11101] The reading's sizing: one height (header, content, inset), 140..320 wide.</summary>
    public static WidgetSizing Sizing(WeatherNowConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var height = FamilySpace.ContentTop + Content(config.Style) + FamilySpace.Inset;
        var natural = config.Style switch
        {
            WeatherNowStyle.LargeIcon => 200,
            WeatherNowStyle.Minimal => 140,
            _ => 180,
        };
        return WidgetSizing.Range(new(Narrowest, height), new(natural, height), new(Widest, height));
    }
}
