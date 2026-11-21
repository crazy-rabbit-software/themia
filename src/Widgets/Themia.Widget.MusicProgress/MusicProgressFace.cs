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
}
