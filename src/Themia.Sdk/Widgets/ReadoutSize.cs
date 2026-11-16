using System.Text.Json.Serialization;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-2449] How big a one-readout widget's number is (the clock, the timers, the toggles): a setting,
/// never the card's size. Small, Medium and Large are the family's 40, 68 and 96 ([S-6700]).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReadoutSize>))]
public enum ReadoutSize
{
    Small = 0,
    Medium = 1,
    Large = 2,
}

/// <summary>[S-2449] A config whose one readout's size is the user's (<see cref="ReadoutSize"/>); its sizing follows from it.</summary>
public interface IReadoutSized
{
    ReadoutSize Size { get; set; }
}

/// <summary>[S-2449] The readout sizes on the family's steps.</summary>
public static class ReadoutSizes
{
    /// <summary>A new widget's: the middle one.</summary>
    public const ReadoutSize Default = ReadoutSize.Medium;

    /// <summary>The family step a size draws its number at: 40, 68, 96.</summary>
    public static TypeStep Step(ReadoutSize size) => size switch
    {
        ReadoutSize.Small => FamilyType.Big40,
        ReadoutSize.Large => FamilyType.Hero96,
        _ => FamilyType.Hero68,
    };

    /// <summary>
    /// [S-12410] The most a face that picks its step by its box (the clock) may grow to in a box larger
    /// than its Size's (a relaxed range, [S-12411]): one family step past the Size's (40 to 68, 68 to 96,
    /// 96 to 136), as a factor on its natural box. Beyond it the rest is spacing round the face.
    /// </summary>
    public static double Climb(ReadoutSize size) => size switch
    {
        ReadoutSize.Small => FamilyType.Hero68.FontSize / FamilyType.Big40.FontSize,
        ReadoutSize.Large => FamilyType.Hero136.FontSize / FamilyType.Hero96.FontSize,
        _ => FamilyType.Hero96.FontSize / FamilyType.Hero68.FontSize,
    };

    /// <summary>A stored size that is none of the three reads as the default.</summary>
    public static ReadoutSize Normalise(ReadoutSize size) => Enum.IsDefined(size) ? size : Default;
}
