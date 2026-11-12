using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11310] The Bluetooth list's rows: one column of the config's rows (one line or two each), the
/// battery as each row's value (a column is at least 180 wide from the list's least width). More devices
/// scroll inside the rows; nothing appears or goes with the card's size.
/// </summary>
public readonly record struct BluetoothRowPlan(int Lines, int Rows);

/// <summary>[S-11310] The Bluetooth list's plan from its config.</summary>
public static class BluetoothLayout
{
    /// <summary>[S-11310] The rows and lines the config asks for, held to the kit's ranges.</summary>
    public static BluetoothRowPlan Plan(Model.BluetoothConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var choice = KitSizing.Choice(config);
        return new BluetoothRowPlan(choice.Lines, choice.Rows);
    }
}
