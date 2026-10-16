using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-6560] What a fill is: the default, nothing, or a colour of the user's own.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FillKind>))]
public enum FillKind
{
    /// <summary>
    /// The default. App-wide: the Fluent fill (the Windows card for a background, each item's own
    /// fill for items). On a widget: whatever the app-wide choice is.
    /// </summary>
    Default = 0,

    /// <summary>No fill at all: what is behind shows through.</summary>
    Transparent = 1,

    /// <summary><see cref="FillChoice.Colour"/> at <see cref="FillChoice.Opacity"/>.</summary>
    Colour = 2,
}

/// <summary>
/// [S-6560] One fill choice, the same for a widget's background and for its items' background:
/// Default, Transparent, or a colour (<c>#RRGGBB</c>) with an opacity 0..1, picked in one colour
/// control. Stored as <c>{ "kind": "Colour", "colour": "#1B2030", "opacity": 0.55 }</c>.
/// <para>
/// The colour and opacity are kept when the kind is not <see cref="FillKind.Colour"/>, so going
/// back to a colour finds the one last picked; they mean nothing until then.
/// </para>
/// </summary>
public sealed class FillChoice
{
    public FillKind Kind { get; set; } = FillKind.Default;

    /// <summary>The colour as <c>#RRGGBB</c>; read only when <see cref="Kind"/> is <see cref="FillKind.Colour"/>.</summary>
    public string Colour { get; set; } = string.Empty;

    /// <summary>How opaque the colour is, 0 to 1; read only when <see cref="Kind"/> is <see cref="FillKind.Colour"/>.</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>A fresh default choice.</summary>
    public static FillChoice Default() => new();

    /// <summary>A fresh transparent choice.</summary>
    public static FillChoice Transparent() => new() { Kind = FillKind.Transparent };

    /// <summary>A colour at an opacity, normalised and clamped as load validation would leave it.</summary>
    public static FillChoice Of(string hex, double opacity) => new()
    {
        Kind = FillKind.Colour,
        Colour = HexColor.Normalise(hex),
        Opacity = ClampOpacity(opacity),
    };

    public FillChoice Clone() => new() { Kind = Kind, Colour = Colour, Opacity = Opacity };

    /// <summary>
    /// [S-6560] The same choice as far as anything drawn can tell: the same kind, and for a colour the
    /// same colour (case-insensitively) and opacity within a slider's rounding. What a Default or
    /// Transparent choice remembers of an earlier colour does not count.
    /// </summary>
    public bool SameAs(FillChoice? other)
        => other is not null && Kind == other.Kind
            && (Kind != FillKind.Colour
                || (string.Equals(Colour.Trim(), other.Colour.Trim(), StringComparison.OrdinalIgnoreCase)
                    && Math.Abs(Opacity - other.Opacity) < 0.001));

    /// <summary>
    /// [S-6561] Bring a loaded choice inside its invariants, loudly: an unknown kind is Default; a
    /// colour choice whose colour is not one is Default; a colour that is kept is normalised; an
    /// opacity that is not a number is 1 and the rest is clamped to 0..1. Returns what changed, each
    /// line naming <paramref name="what"/>, for the load log.
    /// </summary>
    public IReadOnlyList<string> Repair(string what)
    {
        var repairs = new List<string>();

        if (!Enum.IsDefined(Kind))
        {
            repairs.Add($"{what} had an unknown kind {(int)Kind}; using the default");
            Kind = FillKind.Default;
        }

        var colour = HexColor.Normalise(Colour);
        if (Kind == FillKind.Colour && colour.Length == 0)
        {
            repairs.Add($"{what} colour '{Colour}' is not a colour; using the default");
            Kind = FillKind.Default;
        }

        Colour = colour;

        var opacity = ClampOpacity(Opacity);
        if (!opacity.Equals(Opacity))
        {
            repairs.Add($"{what} opacity {Opacity} out of range; using {opacity}");
            Opacity = opacity;
        }

        return repairs;
    }

    /// <summary>
    /// [S-6562] The choice that is drawn: a widget's own unless it is Default, which follows the
    /// app-wide one. Null own: the app-wide one.
    /// </summary>
    public static FillChoice Effective(FillChoice appWide, FillChoice? own)
    {
        ArgumentNullException.ThrowIfNull(appWide);
        return own is null || own.Kind == FillKind.Default ? appWide : own;
    }

    /// <summary>A fill opacity: not finite is 1, the rest clamped to 0..1.</summary>
    public static double ClampOpacity(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 1;
}
