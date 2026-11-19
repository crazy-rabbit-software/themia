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

    /// <summary>
    /// [S-12813] The ink it is sized by: <see cref="Ink"/>, but for a part showing a placeholder, which keeps the
    /// size of what it stands for (a readout's "--" its reading's).
    /// </summary>
    LineInk Held => Ink;

    /// <summary>[S-12807] Whether it grows with its stack's room (a row: its texts stay centred in it), or keeps its height (a readout).</summary>
    bool Grows { get; }
}
