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

    /// <summary>A stored size that is none of the three reads as the default.</summary>
    public static ReadoutSize Normalise(ReadoutSize size) => Enum.IsDefined(size) ? size : Default;
}
