using Themia.Core.Motion;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>
/// [S-825] One design number: its key (the settings line's), its range, the step a slider takes and its default, as
/// <see cref="LookNumber"/> is a look token's. Read by the repair of a loaded state and by the slider that sets it, so
/// the two cannot hold different ranges.
/// </summary>
public sealed record DesignNumber(string Key, double Min, double Max, double Step, double Default)
{
    /// <summary>The value held to the range: not finite is the default.</summary>
    public double Clamp(double value) => double.IsFinite(value) ? Math.Clamp(value, Min, Max) : Default;
}

/// <summary>
/// [S-825] The design's numbers and the display's scale, in one table: the card's corner radius and border, the text
/// size, the animations' length and a display's magnification of the widgets.
/// </summary>
public static class DesignNumbers
{
    /// <summary>[S-825] [S-6482] A card's corner radius in DIPs, 0..32; 8 Windows' own.</summary>
    public static DesignNumber CornerRadius { get; } = new("radius", 0, 32, 1, DesignState.DefaultCornerRadiusPx);

    /// <summary>A card's border thickness in DIPs, 0..8.</summary>
    public static DesignNumber BorderWidth { get; } = new("borderWidth", 0, 8, 1, 1);

    /// <summary>The border's opacity, 0..1.</summary>
    public static DesignNumber BorderOpacity { get; } = new("borderOpacity", 0, 1, 0.05, 1);

    /// <summary>[S-3830] The text size, the user's accessibility choice: the family's own range (<see cref="FamilyType"/>).</summary>
    public static DesignNumber FontScale { get; } = new("size", FamilyType.MinTextScale, FamilyType.MaxTextScale, 0.05, 1);

    /// <summary>[S-704] An animation's length in milliseconds (the intro, the transition, the unlock).</summary>
    public static DesignNumber AnimationMs { get; } = new("length", IntroMotion.MinMs, IntroMotion.MaxMs, 100, IntroMotion.DefaultMs);

    /// <summary>[S-705] [S-850] A display's magnification of the widgets' content, 50–200 % in fives.</summary>
    public static DesignNumber DisplayScale { get; } = new("scale", 0.5, 2, 0.05, 1);

    /// <summary>Every design number: the tests read it.</summary>
    public static IReadOnlyList<DesignNumber> All { get; } = [CornerRadius, BorderWidth, BorderOpacity, FontScale, AnimationMs, DisplayScale];
}
