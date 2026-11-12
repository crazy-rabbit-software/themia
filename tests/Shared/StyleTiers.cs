using Themia.Core.Widgets;

namespace Themia.Core.Tests;

/// <summary>
/// The seven size tiers as a style draws in them (each card less its 36 header), shared by the
/// style tests of the weather, forecast, stock, exchange-rate and air widgets.
/// </summary>
internal static class StyleTiers
{
    public static readonly WidgetArea Xs = new(120, 84);
    public static readonly WidgetArea S = new(200, 114);
    public static readonly WidgetArea M = new(320, 204);
    public static readonly WidgetArea Wide = new(480, 124);
    public static readonly WidgetArea Tall = new(200, 364);
    public static readonly WidgetArea L = new(560, 384);
    public static readonly WidgetArea Xl = new(800, 524);

    public static readonly WidgetArea[] Tiers = [Xs, S, M, Wide, Tall, L, Xl];
}
