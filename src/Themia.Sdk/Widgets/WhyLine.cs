using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-15710] WR-10 on a one-reading face (an icon, a line of text): without a reading the face stays and the card says why
/// in one compact line under it, the Tile block's state line (a gap and one Caption line, <see cref="TileRules.Below"/>),
/// where the card's content room holds the face's least over that band and a sentence's room across
/// (<see cref="StatusRules.MinSentence"/>). A smaller card keeps the face alone, the why its tooltip, a problem its status
/// mark as a stale reading's ([S-15711]).
/// </summary>
public static class WhyLine
{
    /// <summary>[S-15710] The line's band under the face: the gap and the Caption line, 24.</summary>
    public static double Band => TileRules.Below(label: false, state: true);

    /// <summary>
    /// [S-15716] The least room a why takes beside a face in a card too short for it under the face (a one-row card): about
    /// ten characters of Caption, so it says at least the start of its sentence ("Can't reach t…"), whole in its tooltip.
    /// </summary>
    public const double MinBeside = 64;

    /// <summary>[S-15716] The least card a why stands beside its face in: its Caption line and 4 over and under it.</summary>
    public static double BesideHeight => FamilyType.Caption.LineHeight + (2 * FamilySpace.S1);

    /// <summary>
    /// [S-15710] Whether a content room <paramref name="width"/> by <paramref name="height"/> (the card's less its inset) holds
    /// a face <paramref name="face"/> tall at its least over the why's band, and a sentence's room across. Unmeasured: no.
    /// </summary>
    public static bool Fits(double width, double height, double face)
        => double.IsFinite(width) && double.IsFinite(height) && width >= StatusRules.MinSentence && height >= face + Band;

    /// <summary>
    /// [S-16432] Half the height a block (<paramref name="width"/>: its own width, <paramref name="ink"/>: its ink's height)
    /// leaves in a room <paramref name="roomWidth"/> by <paramref name="roomHeight"/> where the width stops it (the fit scales
    /// it until its ink fills the height or its width the width): the room kept under the why's line and over the block, so
    /// the block standing on the why's line and the line together stand centred in the card (WR-7: the reading and its
    /// line stay together; "Centred is fine": the leftover shared alike over and under). 0 where the height stops it.
    /// [S-16445] A block drawn at most at <paramref name="most"/> (a text face's "--", [S-15713]) leaves what that scale does.
    /// </summary>
    public static double Spare(double roomWidth, double roomHeight, double width, double ink, double most = double.PositiveInfinity)
    {
        if (!(roomWidth > 0) || !(roomHeight > 0) || !(width > 0) || !(ink > 0))
        {
            return 0;
        }

        var scale = Math.Min(Math.Min(roomWidth / width, roomHeight / ink), most > 0 ? most : double.PositiveInfinity);
        return Math.Max(0, roomHeight - (scale * ink)) / 2;
    }
}
