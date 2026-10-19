namespace Themia.Core.Widgets;

/// <summary>[S-2403] One labelled figure on a detail widget.</summary>
/// <param name="Label">What it is ("Base speed").</param>
/// <param name="Value">The figure ("3.20 GHz"), or "--" when it cannot be read right now.</param>
/// <param name="Available">False when the value is "--": it is drawn dimmed.</param>
public readonly record struct DetailFact(string Label, string Value, bool Available = true)
{
    public static DetailFact Unavailable(string label) => new(label, "--", false);
}

/// <summary>[S-2404] One row of a detail widget's list: a drive, a process, an adapter.</summary>
/// <param name="Key">Stable identity, so the keyboard focus stays on the same row across reads.</param>
/// <param name="Name">The row's name.</param>
/// <param name="Value">The figure at the right.</param>
/// <param name="Second">A second figure, shown beside the first when the row is wide enough (memory beside CPU).</param>
/// <param name="Detail">A line under the name when the row is tall enough ("120 GB free of 476 GB").</param>
/// <param name="Fraction">A bar under the row (0..1), or null for none.</param>
/// <param name="Pressure">The bar's colour.</param>
/// <param name="Symbol">[S-2416] The icon leading the row (an adapter's kind, a program); none by default.</param>
/// <param name="IconPath">[S-2433] A file whose own icon leads the row (a process's executable), drawn instead of the symbol when it has one.</param>
public readonly record struct DetailRow(
    string Key,
    string Name,
    string Value,
    string? Second = null,
    string? Detail = null,
    double? Fraction = null,
    MeterPressure Pressure = MeterPressure.Normal,
    RowSymbol Symbol = RowSymbol.None,
    string? IconPath = null);

/// <summary>[S-2416] [S-2433] The symbol leading a detail row, drawn from the shared glyphs.</summary>
public enum RowSymbol
{
    None = 0,

    /// <summary>A program without an icon of its own (or one Windows will not say the path of).</summary>
    App = 1,
    Wired = 2,
    Wireless = 3,
    Cellular = 4,
    Network = 5,
}

/// <summary>
/// [S-2403] Everything one read of a detail widget puts on screen: the hero and its caption, the facts
/// in priority order, the list rows. The part that read it builds it; <see cref="DetailFace"/> decides
/// how much of it a tile shows.
/// </summary>
/// <param name="Hero">The main figure, or null for a widget without one.</param>
/// <param name="Caption">The line under it, or null.</param>
/// <param name="Facts">Most wanted first. A fact Windows does not report on this machine is left out, never "--".</param>
/// <param name="Rows">The list, in display order.</param>
/// <param name="Absent">
/// [S-82a] [S-1942] The thing measured is not on this machine (no battery): the sheet is the widget's own
/// facts, each "--", drawn dimmed with <paramref name="Note"/> as the tooltip (<see cref="Unavailable.Sheet"/>).
/// </param>
/// <param name="Note">Why the sheet is empty or unavailable, for the empty state or the hero's tooltip.</param>
/// <param name="HeroAvailable">False when the hero is "--": drawn dimmed.</param>
public sealed record DetailSheet(
    string? Hero,
    string? Caption,
    IReadOnlyList<DetailFact> Facts,
    IReadOnlyList<DetailRow> Rows,
    bool Absent = false,
    string? Note = null,
    bool HeroAvailable = true)
{
    /// <summary>Nothing read yet: a dimmed hero and no facts.</summary>
    public static DetailSheet Waiting { get; } = new("--", null, [], [], HeroAvailable: false);
}
