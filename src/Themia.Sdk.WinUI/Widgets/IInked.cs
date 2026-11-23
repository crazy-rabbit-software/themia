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

/// <summary>
/// [S-17153] A part whose over and under are its own ink and whose ink across is its box's (a tile's planned words), unless
/// <see cref="OwnAcross"/>: then what its first child draws across (the glyph alone).
/// </summary>
public interface IInkedBox : IInked
{
    /// <summary>Whether its ink across is its first child's rather than its box's.</summary>
    bool OwnAcross { get; }
}
