using Themia.App.Services;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-4474] [S-4470] The paired Bluetooth devices as a widget reaches them (<see cref="IWidgetHost.Bluetooth"/>): the
/// host's one pass over the radios that every Bluetooth widget shares, made again on a slow cadence and on Refresh, a
/// connected device's battery the host's one battery inventory's (<see cref="IWidgetHost.DeviceBatteries"/>). UI thread only.
/// </summary>
public interface IBluetoothDevices
{
    /// <summary>The last pass, its batteries joined in; <see cref="BluetoothStatus.Loading"/> until the first is in.</summary>
    BluetoothSnapshot Last { get; }

    /// <summary>A pass is out.</summary>
    bool Busy { get; }

    /// <summary>
    /// Watch the devices; <paramref name="signal"/> asks for the signal readings too (an LE scan of a few seconds, only
    /// while some watcher wants it). <paramref name="onChanged"/> runs on the UI thread after each pass, when a pass goes
    /// out, and when a battery reading changes. Dispose to let go.
    /// </summary>
    IDisposable Watch(Ticker ticker, bool signal, Action onChanged);

    /// <summary>Read again now, past the cadence (a Refresh command). A pass already out is followed by one more.</summary>
    void Refresh();
}
