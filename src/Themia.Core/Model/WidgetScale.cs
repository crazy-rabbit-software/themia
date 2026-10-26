namespace Themia.Core.Model;

/// <summary>
/// [S-6530]..[S-6533] One widget's own content scale (<see cref="WidgetInstance.Scale"/>): what it
/// may hold, what the properties panel offers, how a hand-edited value is repaired, and how it
/// combines with the display's ([S-850]). Pure.
/// </summary>
public static class WidgetScale
{
    /// <summary>[S-6530] The smallest widget scale, as a factor: half size, like the display scale.</summary>
    public const double Min = 0.5;

    /// <summary>[S-6530] The largest widget scale, as a factor: double size, like the display scale.</summary>
    public const double Max = 2.0;

    /// <summary>[S-6530] The grain of a stored scale: 5 %, the properties menu's Scale slider's step ([S-6531]).</summary>
    public const double Step = 0.05;

    /// <summary>
    /// [S-6530] What a scale is stored as: inside <see cref="Min"/>..<see cref="Max"/>, on the 5 %
    /// grain; anything not a number is 100 %.
    /// </summary>
    public static double Normalise(double scale)
    {
        if (!double.IsFinite(scale))
        {
            return 1.0;
        }

        var stepped = Math.Round(Math.Clamp(scale, Min, Max) / Step, MidpointRounding.AwayFromZero) * Step;

        // Rounded again to 2 places so 0.9 is 0.9 and not 0.9000000000000001: equal scales compare equal.
        return Math.Round(stepped, 2);
    }

    /// <summary>
    /// [S-6533] Repair a widget's stored scale in place: what was wrong with it, or null when it was
    /// already a stored value.
    /// </summary>
    public static string? Repair(WidgetInstance widget)
    {
        ArgumentNullException.ThrowIfNull(widget);

        var normal = Normalise(widget.Scale);
        if (normal.Equals(widget.Scale))
        {
            return null;
        }

        var was = double.IsFinite(widget.Scale)
            ? widget.Scale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            : "not a number";
        widget.Scale = normal;
        return $"scale {was} was outside {Min:0.#}–{Max:0.#} or off the 5 % grain";
    }

    /// <summary>
    /// [S-6532] How much a card's content is magnified: the display's scale times the widget's own.
    /// A display scale that is not a positive number counts as 1, and the widget's own is read as
    /// stored (<see cref="Normalise"/>). [S-3590] There is no third, app-wide factor: it was the
    /// display's scale for every display at once, under the same name.
    /// </summary>
    public static double Effective(double display, double widget)
        => Sane(display) * Normalise(widget);

    /// <summary>The whole number of percent a scale reads as, for its label.</summary>
    public static double Percent(double scale) => Math.Round(Normalise(scale) * 100);

    private static double Sane(double factor) => double.IsFinite(factor) && factor > 0 ? factor : 1.0;
}
