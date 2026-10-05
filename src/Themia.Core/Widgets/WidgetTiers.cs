namespace Themia.Core.Widgets;

/// <summary>One of the sizes every widget is checked at, in DIPs of the painted card.</summary>
public readonly record struct WidgetTier(string Name, double Width, double Height);

/// <summary>
/// The sizes every widget is checked at, laid out side by side by the showcase views ([S-1810]). Chosen to cover what people actually make: a tile barely bigger than an icon, the
/// default-ish small and medium, a wide strip, a tall column, a large panel and an extra-large one
/// (the size at which a widget that only scales, rather than shows more, is plainly mostly empty).
/// </summary>
public static class WidgetTiers
{
    public static IReadOnlyList<WidgetTier> All { get; } =
    [
        new("XS", 120, 120),
        new("S", 200, 150),
        new("M", 320, 240),
        new("Wide", 480, 160),
        new("Tall", 200, 400),
        new("L", 560, 420),
        new("XL", 800, 560),
    ];
}
