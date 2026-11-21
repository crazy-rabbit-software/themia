using Themia.Core.Links;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.MusicProgress;

/// <summary>
/// [S-11330] [S-15104] The progress atom's box: a bar atom, the rail in the universal spacing. A wider card lengthens the
/// bar, a taller one thickens it.
/// </summary>
public static class MusicProgressFace
{
    /// <summary>[S-11330] A new bar's card width.</summary>
    public const double NaturalWidth = 240;

    /// <summary>[S-11330] The progress atom's sizing ([S-2438]): the bar atom's (<see cref="AtomRules.BarSizing"/>); no header.</summary>
    public static WidgetSizing Sizing() => AtomRules.BarSizing(NaturalWidth);

    /// <summary>
    /// [S-11331] What a scrub says on the view's links: the track and the position at <paramref name="fraction"/> of its
    /// <paramref name="duration"/> ([S-1465], <see cref="MusicRules.SeekTarget"/>); nothing for a scrub abandoned (a
    /// negative fraction) or with no track.
    /// </summary>
    public static TrackLink? Scrub(string? track, double fraction, TimeSpan duration)
        => string.IsNullOrEmpty(track) || !(fraction >= 0) ? null : new TrackLink(track, MusicRules.SeekTarget(fraction, duration));
}
