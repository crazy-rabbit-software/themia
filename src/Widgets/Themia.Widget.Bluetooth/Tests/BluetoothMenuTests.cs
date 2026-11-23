using Themia.Core.Localization;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Bluetooth.Tests;

/// <summary>[S-3255] The Bluetooth tile's options on its page.</summary>
public sealed class BluetoothMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(BluetoothConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++);
        BluetoothMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S3255_appearance_then_connected_only_then_what_a_row_shows()
    {
        var lines = Build(new BluetoothConfig());

        Assert.Equal(
            [
                BluetoothStr.WidgetsA_Bluetooth_Settings_ConnectedOnly_Label.Text(),
                Str.Devices_Shown.Text(),
                Str.Devices_Parts.Text(),
                Str.Devices_Gap.Text(),
                Str.Kit_Rows.Text(),
            ],
            lines.Lines.Where(l => !l.IsSeparator).Select(l => l.Text));
    }

    /// <summary>[S-16672] The lines and the lines inside their sections (what an item shows is one section).</summary>
    private static IEnumerable<SettingsLine> Flat(IEnumerable<SettingsLine> lines)
        => lines.SelectMany(l => l.Children is { } inner ? Flat(inner).Prepend(l) : [l]);

    [Fact]
    public void S3255_the_signal_switch_turns_it_on_and_saves_once()
    {
        var config = new BluetoothConfig { ShowSignal = false };
        Flat(Build(config).Lines).Single(l => l.Text == BluetoothStr.WidgetsA_Bluetooth_Settings_Signal_Label.Text()).Action!();

        Assert.True(config.ShowSignal);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S3256_connected_only_offers_no_connection_switch_and_draws_no_connection()
    {
        var connection = BluetoothStr.WidgetsA_Bluetooth_Settings_Connection_Label.Text();
        var config = new BluetoothConfig { ConnectedOnly = true, ShowConnection = true };

        Assert.DoesNotContain(connection, Flat(Build(config).Lines).Select(l => l.Text));
        Assert.False(config.DrawsConnection);

        // Every paired device listed again: the switch is back with the value it kept.
        config.ConnectedOnly = false;
        Assert.True(Flat(Build(config).Lines).Single(l => l.Text == connection).Checked);
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
