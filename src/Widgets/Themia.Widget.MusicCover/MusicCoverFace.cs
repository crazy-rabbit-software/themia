using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.MusicCover;

/// <summary>[S-11320] The cover atom's square: its side at each size, the card fixed both ways to it.</summary>
public static class MusicCoverFace
{
    /// <summary>[S-11320] The square's side at each size: 96, 160, 240.</summary>
    public static double SideAt(ReadoutSize size) => ReadoutSizes.Normalise(size) switch
    {
        ReadoutSize.Small => 96,
        ReadoutSize.Large => 240,
        _ => 160,
    };

    /// <summary>[S-11320] The cover's sizing ([S-2438]): a square of its Size, edge to edge, no header by default.</summary>
    public static WidgetSizing Sizing(MusicCoverConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var side = SideAt(config.Size);
        return WidgetSizing.Fixed(new SizeDip(side, side));
    }
}
