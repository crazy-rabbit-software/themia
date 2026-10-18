namespace Themia.Core.Widgets;

/// <summary>
/// [S-3916] The inset of the toggles' and the power buttons' faces, every style: the card's standard
/// content inset (<c>Fluent.Space4</c>, as every other widget), <c>Fluent.Space2</c> while the tile's
/// short side is under the Compact breakpoint (128), where 16 on each side would take a quarter of it.
/// Tied to the short side alone, so a narrow but tall tile keeps the standard inset.
/// </summary>
public static class ButtonInset
{
    public const double Standard = 16;

    public const double Compact = 8;

    /// <summary>The short side below which the inset tightens: the Compact height breakpoint.</summary>
    public const double CompactSide = 128;

    public static double For(WidgetArea area)
        => area.IsMeasured && Math.Min(area.Width, area.Height) < CompactSide ? Compact : Standard;
}
