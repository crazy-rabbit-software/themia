using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12807] [S-12808] A part that knows where its ink stands in its box without being measured (a family row, a
/// readout): what <see cref="Ink"/> and <see cref="SpreadStack"/> read of it.
/// </summary>
public interface IInked
{
    /// <summary>The room in its box over its first ink and under its last.</summary>
    LineInk Ink { get; }

    /// <summary>[S-14810] Whether it is a repeated item (a family row: laid at its optimal, its fixed size), or a part that keeps its own height (a readout).</summary>
    bool IsRow { get; }
}
