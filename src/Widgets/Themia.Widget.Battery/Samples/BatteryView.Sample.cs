using Themia.App.Widgets;
using Themia.Core.Widgets;

namespace Themia.Widget.Battery;

/// <summary>[S-11850] The Battery widget's sample: a made-up laptop's battery on battery, never this PC's.</summary>
internal sealed partial class BatteryView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _pack.Release();
        _power.Release();
        _held = (new BatteryPower(true, 82, false, false, TimeSpan.FromMinutes(190)), null);
        Show();
        return Task.CompletedTask;
    }
}
