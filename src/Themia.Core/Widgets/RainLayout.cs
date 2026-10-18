using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>[S-5126] The rain widget's ladder for one size.</summary>
/// <param name="HeadlineRoom">The height the headline may grow into.</param>
/// <param name="Caption">The intensity / chance line under the headline.</param>
/// <param name="ChartHeight">The bar timeline's height, bars and their time labels; 0 for none.</param>
public sealed record RainPlan(double HeadlineRoom, bool Caption, double ChartHeight);

/// <summary>[S-5126] The rain widget's ladder.</summary>
public static class RainLayout
{
    /// <summary>[S-5126] The bars need this much to read as a timeline; the time labels come on top.</summary>
    public const double ChartMin = 32;

    /// <summary>
    /// [S-5126] The headline is reserved; then the caption line; then the bar timeline (bars and a
    /// Caption line of times) where at least <see cref="ChartMin"/> of bars fit, and the timeline
    /// takes the rest. The caller sizes the headline by its width before asking (its floor), so a
    /// large tile has a large sentence over a tall timeline.
    /// </summary>
    public static RainPlan Rain(WidgetArea area, double headlineFloor, bool hasCaption)
    {
        if (!area.IsMeasured)
        {
            return new RainPlan(0, false, 0);
        }

        var box = area.Deflate(OnlineLayouts.Inset, OnlineLayouts.VerticalInset);
        var room = new Disclosure(box.Width, box.Height, OnlineLayouts.Gap);
        room.Reserve(headlineFloor);

        var caption = hasCaption && room.Take(FluentType.Caption.LineHeight);
        var labels = FluentType.Caption.LineHeight;
        var chart = 0.0;

        if (box.Width >= 120 && room.Fits(ChartMin + labels))
        {
            var spare = room.Remaining - (caption || headlineFloor > 0 ? OnlineLayouts.Gap : 0);
            chart = spare;
            room.Take(chart);
        }

        return new RainPlan(headlineFloor + room.Remaining, caption, chart);
    }
}
