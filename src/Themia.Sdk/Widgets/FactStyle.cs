namespace Themia.Core.Widgets;

/// <summary>[S-3862] How a detail widget draws one fact: one of the two shared reading parts ([S-668]).</summary>
public enum FactStyle
{
    /// <summary>A stat line: the label on the left in the secondary colour, the value at the right, both Body.</summary>
    Line = 0,

    /// <summary>A stat cell: the Caption label over the value in Body Strong, no box.</summary>
    Cell = 1,
}
