using System.Net.NetworkInformation;
using Microsoft.Windows.System.Power;
using Themia.Core.Diagnostics;
using Themia.Core.SystemData;

namespace Themia.App.Services.SystemData;

/// <summary>
/// [S-2509] Where Windows says a figure changed, the hub reads it at once instead of waiting out the
/// cadence: a power event (plugged in, charging, the charge moved) refreshes the battery feeds, an
/// address change the addresses and the throughput. The hub listens only while a feed reads such a
/// source. Those refreshes are readings, never chart points, so the history keeps its spacing.
/// </summary>
internal sealed partial class SystemHub
{
    private bool _powerHooked;
    private bool _networkHooked;

    /// <summary>Listen to exactly the events an open source wants; called whenever a source opens or closes.</summary>
    private void UpdateEventHooks()
    {
        var wantPower = !_disposed && _order.Any(f => f.Key.Source is SystemSource.Battery or SystemSource.BatteryPack);
        var wantNetwork = !_disposed && _order.Any(f => f.Key.Source is SystemSource.Network or SystemSource.LocalAddresses or SystemSource.PublicAddress);

        if (wantPower != _powerHooked)
        {
            HookPower(wantPower);
        }

        if (wantNetwork != _networkHooked)
        {
            if (wantNetwork)
            {
                NetworkChange.NetworkAddressChanged += OnAddressChanged;
            }
            else
            {
                NetworkChange.NetworkAddressChanged -= OnAddressChanged;
            }

            _networkHooked = wantNetwork;
        }
    }

    private void HookPower(bool hook)
    {
        try
        {
            if (hook)
            {
                PowerManager.BatteryStatusChanged += OnPowerChanged;
                PowerManager.PowerSupplyStatusChanged += OnPowerChanged;
                PowerManager.RemainingChargePercentChanged += OnPowerChanged;
            }
            else
            {
                PowerManager.BatteryStatusChanged -= OnPowerChanged;
                PowerManager.PowerSupplyStatusChanged -= OnPowerChanged;
                PowerManager.RemainingChargePercentChanged -= OnPowerChanged;
            }

            _powerHooked = hook;
        }
        catch (Exception ex)
        {
            // Without the events the battery still reads every tick; it only reacts a little later.
            Log.Warn(Scope, $"could not {(hook ? "listen to" : "stop listening to")} power events", ex);
            _powerHooked = hook;
        }
    }

    // Both handlers run on a system thread: only a hand-off to the UI thread, nothing that can escape.
    private void OnPowerChanged(object? sender, object args) => Post(OnPowerChangedOnUi);

    private void OnAddressChanged(object? sender, EventArgs args) => Post(OnAddressChangedOnUi);

    private void Post(Action action)
    {
        try
        {
            if (!_disposed && !_dispatcher.TryEnqueue(() => Guard(action)))
            {
                Log.Debug(Scope, "a system event arrived while the dispatcher was shutting down");
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not hand a system event to the UI thread", ex);
        }
    }

    private void Guard(Action action)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "handling a system event failed", ex);
        }
    }

    private void OnPowerChangedOnUi()
    {
        RefreshSource(SystemSource.Battery);
        RefreshSource(SystemSource.BatteryPack);
    }

    private void OnAddressChangedOnUi()
    {
        // A new network is a new address and, perhaps, a new public one: both are asked again.
        Services.LocalAddresses.Invalidate();
        Services.PublicAddress.Invalidate();
        RefreshSource(SystemSource.LocalAddresses);
        RefreshSource(SystemSource.PublicAddress);
        RefreshSource(SystemSource.Network);
    }
}
