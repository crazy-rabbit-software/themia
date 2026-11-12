using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.Music;

/// <summary>
/// [S-11311] The track atom's face: the title (BodyStrong) over the artist and album line (Body,
/// secondary), both cut with their ellipsis, and the transport (previous, play or pause in the accent,
/// next: the Controls row's 28 icon buttons) 4 under them at the bottom inset. One height; a wider card
/// only gives the lines more room.
/// </summary>
public static class MusicFace
{
    /// <summary>[S-11311] The gap between the lines and the transport.</summary>
    public const double TransportGap = FamilySpace.S1;

    /// <summary>[S-11311] The card's width: 200 to 420, a new one 244 (beside a Small cover, the old card's 340).</summary>
    public const double Narrowest = 200, NaturalWidth = 244, Widest = 420;

    /// <summary>
    /// [S-11311] The card's height without a header: the inset, the two lines, 4, the transport, the inset
    /// (96: a Small cover's side, so the two stand side by side as one panel).
    /// </summary>
    public const double Height = FamilySpace.Inset + FamilyType.BodyLine + FamilyType.BodyLine + TransportGap + FamilyShape.IconButton + FamilySpace.Inset;

    /// <summary>[S-11311] The track atom's sizing ([S-2438]): one height, 200..420 wide; no header by default.</summary>
    public static WidgetSizing Sizing()
        => WidgetSizing.Range(new SizeDip(Narrowest, Height), new SizeDip(NaturalWidth, Height), new SizeDip(Widest, Height));
}
