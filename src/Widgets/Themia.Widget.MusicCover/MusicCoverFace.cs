using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.MusicCover;

/// <summary>[S-11320] [S-15102] The cover atom's box: a picture atom, as large or small as its card.</summary>
public static class MusicCoverFace
{
    /// <summary>[S-11320] A new cover's square side.</summary>
    public const double Side = 160;

    /// <summary>
    /// [S-11320] The cover's sizing ([S-2438]): a picture atom's (<see cref="AtomRules.PictureSizing"/>), a new one a
    /// square of <see cref="Side"/>, the picture in the universal spacing; the card's size is the user's (no Size setting,
    /// [S-2449] retired): down to the grid's least widget (WR-14) and up as far as the work area goes ([S-12411]).
    /// </summary>
    public static WidgetSizing Sizing(MusicCoverConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return AtomRules.PictureSizing(Side);
    }
}
