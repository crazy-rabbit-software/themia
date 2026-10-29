using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the gauge style draws at one size ([S-6170]).</summary>
/// <param name="Side">The gauge in a column on the left and the readings beside it (an expanded width), else the readings under it.</param>
/// <param name="Column">On the side: the gauge's column, from the card's edge to where the rows begin (the inset included).</param>
/// <param name="Diameter">The half ring's width; 0 when it would be under <see cref="MeterPlan.GaugeFloor"/>: the index alone.</param>
/// <param name="Reading">The index's Readout step (in the bowl, or alone).</param>
/// <param name="Part">The gauge and the band's line under it (or the index alone and its line), from the view's top.</param>
/// <param name="RowsTop">Where the rows start: 8 under the part, or at its top on the side.</param>
/// <param name="Facts">How many readings are shown (the first ones, whole rows).</param>
public readonly record struct AirGaugePlan(bool Side, double Column, double Diameter, TypeStep Reading, double Part, double RowsTop, int Facts);

/// <summary>
/// [S-5107] [S-6170] The air widget's faces on the family's blocks: the number style is the detail
/// widgets' plan ([S-2402], <see cref="DetailFace"/>: the index as the Readout, its band as the line
/// under it; the readings as fact rows beside it in two fifths of an expanded width, else 8 under it);
/// the gauge style puts the index on the Chart's half ring ([S-6717], the meters' gauge, [S-6302]).
/// </summary>
public static class AirLayout
{
    /// <summary>The widest index the widget prints ("500 US AQI") with its band under it: the step does not jump with the reading.</summary>
    public static readonly ReadoutText Worst = new("500", " US AQI", LineUnder: true);

    /// <summary>The index in the gauge's bowl: its band stands under the gauge, not in it.</summary>
    public static readonly ReadoutText Bowl = new("500", " US AQI");

    /// <summary>[S-5107] The number style's face for <paramref name="area"/> and <paramref name="facts"/> readings.</summary>
    public static DetailFacePlan Number(WidgetArea area, int facts) => DetailFace.Plan(area, new DetailShape(Worst, Math.Max(0, facts)));

    /// <summary>
    /// [S-6170] The gauge style's face: the half ring as wide as its box (the content, or the side column
    /// less the inset) while its half, its stroke and the band's line under it fit the height; the index
    /// on the Readout's step for the bowl (<see cref="MeterPlan.GaugeReading"/>), standing on the gauge's
    /// foot; the band centred under it. A ring under 48 is not drawn: the index alone, its band under it.
    /// The readings as fact rows, beside it on an expanded width, else 8 under it, whole rows only.
    /// </summary>
    public static AirGaugePlan Gauge(WidgetArea area, int facts)
    {
        facts = Math.Max(0, facts);
        var (width, height) = MeterPlan.Content(area);
        var side = facts > 0 && area.WidthClass >= SizeClass.Expanded;
        var column = side ? Math.Floor(area.Width * DetailFace.HeroShare) : 0;
        var box = side ? column - FamilySpace.Inset : width;

        var diameter = Math.Floor(Math.Min(box, 2 * (height - (FamilyShape.Stroke / 2))));
        var step = MeterPlan.GaugeReading(area, diameter, Bowl);
        if (ChartRules.GaugeHeight(diameter) + FamilyType.Under(step).LineHeight > height)
        {
            diameter = Math.Floor(Math.Min(box, 2 * (height - FamilyType.Under(step).LineHeight - (FamilyShape.Stroke / 2))));
            step = MeterPlan.GaugeReading(area, diameter, Bowl);
        }

        double part;
        if (diameter < MeterPlan.GaugeFloor)
        {
            diameter = 0;
            step = ReadoutRules.Step(area, box, height, Worst);
            part = ReadoutRules.Height(step, Worst) <= height ? ReadoutRules.Height(step, Worst) : step.LineHeight;
        }
        else
        {
            part = ChartRules.GaugeHeight(diameter) + FamilyType.Under(step).LineHeight;
        }

        var top = side ? 0 : part + FamilySpace.S2;
        return new AirGaugePlan(side, column, diameter, step, part, top, Math.Min(facts, RowRules.Fit(height - top, 1)));
    }
}
