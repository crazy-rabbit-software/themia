using Themia.Sdk.Kit;
using M = Themia.Sdk.Kit.ShellMetrics.Flyout;

namespace Themia.Core.Geometry;

/// <summary>[S-2325] Where a page's flyout stands (DIPs, the surface's): its box and where along its top the beak's tip is.</summary>
public readonly record struct FlyoutSpot(double X, double Y, double Width, double Height, double Tip);

/// <summary>
/// [S-2325] [S-2326] A page's flyout hung from its entry, Karpo's (karpo-app, docs/widgets.md §578, §584;
/// <c>FlyoutPlacement</c>, <c>Beak</c>): 580 wide, centred on its entry, 12 under the bar, pushed in to
/// stay 12 from the work area's sides; as tall as its page up to 70 % of the work area (then it scrolls)
/// and never past the work area's foot less that margin; its beak's tip over the entry's middle, kept on
/// the straight of the top (the corner's radius and half the beak in from either end), so a flyout
/// pushed in at a screen edge still points at its entry. Pure: the surface measures and asks.
/// </summary>
public static class FlyoutPlacement
{
    /// <summary>
    /// The flyout of a page <paramref name="content"/> DIPs tall, hung from <paramref name="entry"/> on
    /// <paramref name="bar"/>, inside <paramref name="area"/> (the work area).
    /// </summary>
    public static FlyoutSpot Place(PixelRect entry, PixelRect bar, PixelRect area, double content)
    {
        var width = Math.Min(M.Width, Math.Max(0, area.Width - (2 * M.EdgeMargin)));
        var middle = entry.X + (entry.Width / 2.0);
        var x = middle - (width / 2);
        x = Math.Max(area.X + M.EdgeMargin, Math.Min(x, area.X + area.Width - M.EdgeMargin - width));

        var y = bar.Y + bar.Height + M.Gap;
        var room = Math.Min(Math.Floor(area.Height * M.MostShare), area.Y + area.Height - M.EdgeMargin - y);
        var height = Math.Max(0, Math.Min(Math.Ceiling(content), room));
        return new FlyoutSpot(Math.Round(x), y, width, height, Tip(middle - Math.Round(x), width));
    }

    /// <summary>[S-2326] The beak's tip <paramref name="at"/> DIPs along a top <paramref name="width"/> wide, held to its straight; its middle where it is too short.</summary>
    public static double Tip(double at, double width)
    {
        var least = ShellMetrics.OverlayRadius + (M.BeakWidth / 2);
        var most = width - least;
        return most < least ? width / 2 : Math.Clamp(at, least, most);
    }
}
