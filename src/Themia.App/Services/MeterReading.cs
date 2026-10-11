namespace Themia.App.Services;

/// <summary>One reading from a meter. <see cref="Fraction"/> is 0..1, or null when unavailable.</summary>
/// <param name="Fraction">Where the reading sits on its scale; what a share chart plots.</param>
/// <param name="Primary">The number on the tile.</param>
/// <param name="Available">False when the machine cannot answer (no battery, drive gone).</param>
/// <param name="Absent">
/// True when the thing being measured does not exist on this machine at all, as opposed to
/// existing but being momentarily unreadable. A desktop with no battery is not a battery meter
/// that failed, and the two want different things on screen: one is an empty state, the other
/// is a dimmed <c>--</c>.
/// </param>
/// <param name="Value">
/// [S-664] The reading in its own unit — bytes per second, milliseconds — for a meter whose
/// <see cref="Fraction"/> is a position on a scale rather than the value itself: what a
/// window-scaled chart plots. Null means the fraction is the value (a percentage meter).
/// </param>
/// <param name="Second">
/// [S-1126] A second series for the chart (upload beside download, writes beside reads), in the
/// same terms the first is plotted in: a fraction on a share chart, a value on a window-scaled one.
/// </param>
/// <param name="Reason">
/// [S-1762] Why a reading is unavailable ("Drive C: is not answering"), shown on hover over the
/// dimmed reading — the tile itself carries no text but the number. Null when there is nothing to say.
/// </param>
/// <param name="Charging">
/// [S-1187] A battery reading taken while it charges: the bolt in the glyph and the charging colour.
/// </param>
public readonly record struct MeterReading(
    double? Fraction,
    string Primary,
    bool Available,
    bool Absent = false,
    double? Value = null,
    double? Second = null,
    string? Reason = null,
    bool Charging = false)
{
    public static MeterReading Unavailable(string reason) => new(null, "--", false, Reason: reason);

    /// <summary>The thing being measured is not present on this machine.</summary>
    public static MeterReading Missing(string reason) => new(null, "--", false, Absent: true, Reason: reason);
}
