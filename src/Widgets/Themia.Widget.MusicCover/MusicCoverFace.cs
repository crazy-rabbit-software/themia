using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.MusicCover;

/// <summary>[S-11320] The cover atom's box: a picture, as large or small as its card.</summary>
public static class MusicCoverFace
{
    /// <summary>[S-11320] A new cover's square side.</summary>
    public const double Side = 160;

    /// <summary>
    /// [S-11320] The cover's sizing ([S-2438]): a new one a square of <see cref="Side"/>, the picture edge to edge; the
    /// card's size is the user's (no Size setting, [S-2449] retired): down to the grid's least widget (WR-14,
    /// <see cref="WidgetSizing.LeastScaled"/>, which still holds the placeholder's glyph) and up as far as the work area
    /// goes ([S-12411]). No header by default.
    /// </summary>
    public static WidgetSizing Sizing(MusicCoverConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var least = new SizeDip(WidgetSizing.LeastScaled, WidgetSizing.LeastScaled);
        var natural = new SizeDip(Side, Side);
        return WidgetSizing.Range(least, natural, natural) with { Room = StretchRoom.Picture };
    }
}
