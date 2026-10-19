using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Bluetooth.Tests;

/// <summary>[S-3255] The Bluetooth tile's options on its properties menu.</summary>
public sealed class BluetoothMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(BluetoothConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        BluetoothMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S3255_appearance_then_connected_only_then_what_a_row_shows()
    {
        var lines = Build(new BluetoothConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [
                BluetoothStr.WidgetsA_Bluetooth_Settings_ConnectedOnly_Label.Text(),
                BluetoothStr.WidgetsA_Bluetooth_Settings_Icon_Label.Text(),
                BluetoothStr.WidgetsA_Bluetooth_Settings_Connection_Label.Text(),
                BluetoothStr.Widget_Battery_Label.Text(),
                BluetoothStr.WidgetsA_Bluetooth_Settings_Signal_Label.Text(),
            ],
            lines.Lines.Where(l => !l.IsSeparator).Select(l => l.Text));
    }

    [Fact]
    public void S3255_the_signal_switch_turns_it_on_and_saves_once()
    {
        var config = new BluetoothConfig { ShowSignal = false };
        Build(config).Lines.Single(l => l.Text == BluetoothStr.WidgetsA_Bluetooth_Settings_Signal_Label.Text()).Action!();

        Assert.True(config.ShowSignal);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S3256_connected_only_offers_no_connection_switch_and_draws_no_connection()
    {
        var connection = BluetoothStr.WidgetsA_Bluetooth_Settings_Connection_Label.Text();
        var config = new BluetoothConfig { ConnectedOnly = true, ShowConnection = true };

        Assert.DoesNotContain(connection, Build(config).Lines.Select(l => l.Text));
        Assert.False(config.DrawsConnection);

        // Every paired device listed again: the switch is back with the value it kept.
        config.ConnectedOnly = false;
        Assert.True(Build(config).Lines.Single(l => l.Text == connection).Checked);
        Assert.True(config.DrawsConnection);
    }

    [Fact]
    public void S3256_turning_connected_only_on_from_the_menu_takes_the_connection_switch_away()
    {
        var config = new BluetoothConfig();
        Build(config).Lines.Single(l => l.Text == BluetoothStr.WidgetsA_Bluetooth_Settings_ConnectedOnly_Label.Text()).Action!();

        Assert.True(config.ConnectedOnly);
        Assert.Equal(1, _saves);
        Assert.Empty(config.ShownSettings);
    }
}
