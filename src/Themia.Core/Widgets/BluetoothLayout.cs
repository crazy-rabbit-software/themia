namespace Themia.Core.Widgets;

/// <summary>
/// [S-1254] How the Bluetooth list is drawn at one size.
/// </summary>
/// <param name="Columns">1, or 2 on the largest tiles when one column cannot hold the list.</param>
/// <param name="TwoLine">Rows carry a second line (kind · connection · battery · signal).</param>
/// <param name="RowHeight">One row's height.</param>
/// <param name="VisibleRows">Whole rows per column the list shows before it scrolls.</param>
/// <param name="StatusWord">"Connected"/"Paired" beside the dot (one-line rows only; two-line rows say it below).</param>
/// <param name="BatteryColumn">The battery percentage in its own column (else, on a two-line row, in the second line; on a one-line row, the tooltip).</param>
/// <param name="SignalColumn">The signal reading in its own column, likewise.</param>
/// <param name="BatteryBar">A small level bar beside the battery percentage.</param>
/// <param name="Summary">"2 connected" in the header instead of the bare count.</param>
public readonly record struct BluetoothRowPlan(
    int Columns,
    bool TwoLine,
    double RowHeight,
    int VisibleRows,
    bool StatusWord,
    bool BatteryColumn,
    bool SignalColumn,
    bool BatteryBar,
    bool Summary);

/// <summary>
/// [S-1254] The Bluetooth list's disclosure ladder. XS/S: icon, name, dot in as many rows as fit;
/// from 260 px a column carries the status word and battery %; from 400 px the signal and a battery
/// bar; from Regular×Expanded rows grow a second line; the largest tiles split a long list into two
/// columns. The
/// list itself takes every whole row the tile has — the row cap is a ceiling, not a target. A
/// short list on a large tile stays a short list: the widget lists devices, nothing more.
/// </summary>
public static class BluetoothLayout
{
    /// <summary>Fluent's list row heights (<c>Fluent.RowHeight</c> / <c>RowHeightCompact</c>).</summary>
    public const double RowHeight = 40;

    public const double CompactRowHeight = 32;

    /// <summary>Body line + Caption line + padding.</summary>
    public const double TwoLineRowHeight = 56;

    /// <summary>Below this list height rows go compact (<c>Fluent.CompactBelow</c>).</summary>
    public const double CompactBelow = 200;

    /// <summary>A column at least this wide has room for the status word beside the dot.</summary>
    public const double StatusWordMinWidth = 260;

    /// <summary>
    /// A column at least this wide has room for the battery percentage beside the name. Narrower,
    /// the name keeps the row: a list of "15%" with no device names says nothing.
    /// </summary>
    public const double BatteryColumnMinWidth = 260;

    /// <summary>A column at least this wide also has room for the signal reading.</summary>
    public const double SignalColumnMinWidth = 400;

    /// <summary>A column at least this wide has room for a battery bar beside the percentage.</summary>
    public const double BatteryBarMinWidth = 400;

    /// <param name="area">The view's slot.</param>
    /// <param name="listHeight">The list's height (slot less its own padding).</param>
    /// <param name="count">Devices to show.</param>
    public static BluetoothRowPlan Plan(WidgetArea area, double listHeight, int count)
    {
        var twoLine = area.IsMeasured && area.HeightClass >= SizeClass.Expanded && area.WidthClass >= SizeClass.Regular;

        var rowHeight = twoLine ? TwoLineRowHeight
            : area.IsMeasured && listHeight < CompactBelow ? CompactRowHeight
            : RowHeight;

        var perColumn = Math.Max(1, Disclosure.WholeRows(listHeight, rowHeight));

        var columns = area.IsMeasured && area.WidthClass >= SizeClass.Huge && count > perColumn ? 2 : 1;
        var columnWidth = columns == 2 ? area.Width / 2 : area.Width;

        // An unmeasured area is drawn minimal and grows; never wordy first and then shed.
        var measured = area.IsMeasured;

        return new BluetoothRowPlan(
            columns,
            twoLine,
            rowHeight,
            perColumn,
            StatusWord: measured && !twoLine && columnWidth >= StatusWordMinWidth,
            BatteryColumn: measured && columnWidth >= BatteryColumnMinWidth,
            SignalColumn: measured && columnWidth >= SignalColumnMinWidth,
            BatteryBar: measured && columnWidth >= BatteryBarMinWidth,
            Summary: measured && area.WidthClass >= SizeClass.Expanded);
    }
}
