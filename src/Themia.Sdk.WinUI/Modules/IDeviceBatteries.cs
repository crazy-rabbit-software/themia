using Themia.App.Services;
using Themia.Sdk.Devices;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-4470] The peripherals' batteries as a widget reaches them (<see cref="IWidgetHost.DeviceBatteries"/>):
/// the host's one read that every battery widget shares, made again when Windows says a device came,
/// went or changed its reading, and on a slow cadence for what Windows sends no event for. UI thread only.
/// </summary>
public interface IDeviceBatteries
{
    /// <summary>The last read; <see cref="DeviceBatterySnapshot.Loading"/> until the first one is in.</summary>
    DeviceBatterySnapshot Last { get; }

    /// <summary>A read is out.</summary>
    bool Busy { get; }

    /// <summary>
    /// Watch the batteries; <paramref name="onChanged"/> runs on the UI thread after each read that
    /// changed something, and when a read goes out or comes back (<see cref="Busy"/>). Dispose to let go.
    /// </summary>
    IDisposable Watch(Ticker ticker, Action onChanged);

    /// <summary>Read again now, past the cadence (a Refresh command). A read already out is followed by one more.</summary>
    void Refresh();
}
