using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12807] [S-12808] A part that knows where its ink stands in its box without being measured (a family row, a
/// readout): what <see cref="PartInk"/> and <see cref="SpreadStack"/> read of it.
/// </summary>
public interface IInked
{
    /// <summary>The room in its box over its first ink and under its last.</summary>
    LineInk Ink { get; }

    /// <summary>[S-12807] The band over and under its content at its natural height, which a stack's ends cancel (a row's; 0 for a readout).</summary>
    double Band { get; }
}
