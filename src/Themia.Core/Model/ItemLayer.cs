namespace Themia.Core.Model;

/// <summary>
/// [S-6510] The item background: the second layer of a widget's look, apart from the card's own
/// background ([S-823]). It is what the things a widget draws inside its card are filled with — list
/// rows, tiles, chips, the tracks of bars and rings — so a widget whose card is None still shows its
/// items on something. App-wide on <see cref="DesignState.ItemBackground"/>, and per widget on
/// <see cref="WidgetInstance.ItemBackground"/>, where null follows the app-wide one ([S-6512]).
/// </summary>
public sealed class ItemLayer
{
    /// <summary>The items' colour as <c>#RRGGBB</c>; empty keeps each item's own Fluent fill.</summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>How opaque the items' fill is, 0 to 1. 1 is the fill as it is.</summary>
    public double Opacity { get; set; } = 1.0;

    public ItemLayer Clone() => new() { Color = Color, Opacity = Opacity };

    /// <summary>The same layer: the colour case-insensitively, the opacity within a slider's rounding.</summary>
    public bool SameAs(ItemLayer? other)
        => other is not null
            && string.Equals(Color.Trim(), other.Color.Trim(), StringComparison.OrdinalIgnoreCase)
            && Math.Abs(Opacity - other.Opacity) < 0.001;

    /// <summary>
    /// [S-6511] Bring a loaded layer inside its invariants: a colour that is not one is the theme's
    /// (empty), an opacity that is not a number is 1 and the rest is clamped to 0..1. Returns what had
    /// to change, for the load log.
    /// </summary>
    public IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        var colour = Widgets.HexColor.Normalise(Color);
        if (!string.Equals(colour, Color, StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(Color) && colour.Length == 0)
            {
                repairs.Add($"item background colour '{Color}' is not a colour; using the theme's");
            }

            Color = colour;
        }

        var opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, 0, 1) : 1;
        if (opacity != Opacity)
        {
            repairs.Add($"item background opacity {Opacity} out of range; using {opacity}");
            Opacity = opacity;
        }

        return repairs;
    }
}
