using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1254] How the Bluetooth list is drawn at one size.
/// </summary>
/// <param name="Columns">1, or 2 on the largest tiles when one column cannot hold the list.</param>
/// <param name="Lines">[S-6712] The lines every row shows: 1, or 2 where the tier gives rows two (kind · connection · signal).</param>
/// <param name="VisibleRows">Whole rows per column the list shows before it scrolls.</param>
/// <param name="BatteryValue">[S-3253] The battery percentage as the row's value (a column at least 180 wide); else the second line or the tooltip says it.</param>
public readonly record struct BluetoothRowPlan(int Columns, int Lines, int VisibleRows, bool BatteryValue);

/// <summary>
/// [S-1254] The Bluetooth list's ladder on the family's rows ([S-6711], [S-6712]): every row a Row
/// block, 32 tall, 48 with its second line where the tier gives rows two; as many whole rows as the
/// view's area less the card's bottom inset holds (the device count is a ceiling, not a target); the
/// battery percentage the row's value from a 180 column; two columns at the Huge width when one
/// cannot hold the list. A short list on a large tile stays a short list.
/// </summary>
public static class BluetoothLayout
{
    /// <summary>
    /// [S-3253] A column at least this wide (the Regular width breakpoint) has room for the battery
    /// percentage beside the name. Narrower, the name keeps the row: a list of "15%" with no device
    /// names says nothing, and the second line or the tooltip still carries it.
    /// </summary>
    public const double BatteryValueMinWidth = 180;

    /// <param name="area">The view's area (the card less its header).</param>
    /// <param name="count">Devices to show.</param>
    public static BluetoothRowPlan Plan(WidgetArea area, int count)
    {
        // An unmeasured area is drawn minimal and grows; never wordy first and then shed.
        if (!area.IsMeasured)
        {
            return new BluetoothRowPlan(1, 1, 1, false);
        }

        var lines = RowRules.Lines(area, 2);
        var perColumn = Math.Max(1, RowRules.Fit(area.Height - FamilySpace.Inset, lines));
        var columns = area.WidthClass >= SizeClass.Huge && count > perColumn ? 2 : 1;
        var columnWidth = area.Width / columns;
        return new BluetoothRowPlan(columns, lines, perColumn, columnWidth >= BatteryValueMinWidth);
    }
}
