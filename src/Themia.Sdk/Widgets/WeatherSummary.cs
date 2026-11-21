using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-15020] [S-15025] The weather summary's parts (owner, 2026-11-21: "weather summary should also be split into three
/// widgets"): the removed card (<c>weatherNow</c>: the sky's glyph, the temperature on the 28 and the condition under it) is
/// the sky (<c>weatherSky</c>, an icon kind), the temperature (<c>weatherTemperature</c>, the weather facts' single metric)
/// and the condition (<c>weatherCondition</c>, a text kind). A placed one becomes them in place: the sky on its left, a
/// third of its width, its whole height; the temperature over the condition beside it, half its height each; its Minimal
/// look (the temperature alone) the temperature alone, in its whole spot. Kept in the SDK so the two families that hold the
/// parts declare one split.
/// </summary>
public static class WeatherSummary
{
    /// <summary>[S-15025] The removed card's kind.</summary>
    public const string Removed = "weatherNow";

    /// <summary>[S-15025] The removed card's look that drew the temperature alone.</summary>
    private static readonly (string Key, string Value) Minimal = ("style", "minimal");

    /// <summary>[S-15025] The sky's share of the card: its left third, its whole height.</summary>
    public static MergeShare SkyShare { get; } = new(0, 0, 1.0 / 3, 1);

    /// <summary>[S-15025] The temperature's share: the top half of the rest.</summary>
    public static MergeShare TemperatureShare { get; } = new(1.0 / 3, 0, 2.0 / 3, 0.5);

    /// <summary>[S-15025] The condition's share: the bottom half of the rest.</summary>
    public static MergeShare ConditionShare { get; } = new(1.0 / 3, 0.5, 2.0 / 3, 0.5);

    /// <summary>[S-15025] The sky stands in for the card in every look but Minimal.</summary>
    public static IReadOnlyList<MergeFrom> Sky { get; } = [new(Removed) { Unless = Minimal, Share = SkyShare }];

    /// <summary>[S-15025] The condition stands in for the card in every look but Minimal.</summary>
    public static IReadOnlyList<MergeFrom> Condition { get; } = [new(Removed) { Unless = Minimal, Share = ConditionShare }];

    /// <summary>[S-15025] The temperature: the Minimal card whole, every other card's top right.</summary>
    public static IReadOnlyList<MergeFrom> Temperature { get; } = [new(Removed) { When = Minimal }, new(Removed) { Share = TemperatureShare }];
}
