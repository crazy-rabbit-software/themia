using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11130] [S-11131] The air index's two looks and their sizes. Number: the index as the Readout on the
/// family's 28 with its unit, its band the Caption under it: one height, 160..320 wide. Gauge: the
/// index on the Chart's half ring ([S-6717]) of a fixed 160, the index on 28 in its bowl, the band's
/// words centred under it: one size. Its pollutants and pollen are the air facts' atom.
/// </summary>
public static class AirLayout
{
    /// <summary>The widest index the widget prints ("500 US AQI") with its band under it.</summary>
    public static readonly ReadoutText Worst = new("500", " US AQI", LineUnder: true);

    /// <summary>[S-11131] The half ring's width.</summary>
    public const double Diameter = 160;

    public const double Narrowest = 160;

    public const double Widest = 320;

    /// <summary>[S-11131] The index's sizing, with its header.</summary>
    public static WidgetSizing Sizing(AirQualityConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Style == AirQualityStyle.Gauge)
        {
            var gauge = ChartRules.GaugeHeight(Diameter) + FamilyType.Caption.LineHeight;
            return WidgetSizing.Fixed(new((2 * FamilySpace.Inset) + Diameter, FamilySpace.ContentTop + Math.Ceiling(gauge) + FamilySpace.Inset));
        }

        var height = FamilySpace.ContentTop + ReadoutRules.Height(FamilyType.Big28, Worst) + FamilySpace.Inset;
        return WidgetSizing.Range(new(Narrowest, height), new(200, height), new(Widest, height));
    }
}
