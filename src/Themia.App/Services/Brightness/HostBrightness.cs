using Themia.Core.Model;
using Themia.Sdk.WinUI;

namespace Themia.App.Services.Brightness;

/// <summary>[S-6913] The brightness a widget asks for (<see cref="IWidgetHost.Brightness"/>): the app's one <see cref="BrightnessService"/>.</summary>
internal sealed class HostBrightness : IBrightness
{
    public static HostBrightness Instance { get; } = new();

    private HostBrightness()
    {
    }

    public BrightnessSnapshot Snapshot => BrightnessService.Snapshot;

    public IDisposable Acquire(Action changed) => BrightnessService.Acquire(changed);

    public void Refresh() => BrightnessService.Refresh();

    public bool Set(string? own, BrightnessScreens screens, int level) => BrightnessService.Set(own, screens, level);

    public int? Asked(string device) => BrightnessService.Asked(device);

    public string? DeviceOf(nint window) => BrightnessNative.DeviceOf(window);
}
