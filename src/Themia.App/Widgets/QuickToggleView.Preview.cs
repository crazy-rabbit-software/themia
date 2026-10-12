#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only: the states a render cannot reach on this PC ([S-1917], [S-3909]).</summary>
internal sealed partial class QuickToggleView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["on", "off", "unavailable", "unknown", "hover", "pressed", "refused"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "on":
                _preview = ToggleReading.Of(true);
                break;

            case "off":
                _preview = ToggleReading.Of(false);
                break;

            case "unavailable":
                _preview = ToggleReading.Blocked(PreviewBlock());
                break;

            case "unknown":
                _preview = ToggleReading.Pending;
                break;

            case "hover":
                _hover = true;
                break;

            case "pressed":
                _pressed = true;
                break;

            case "refused":
                _preview = ToggleReading.Of(false);
                if (_switch.Press(_preview, DateTimeOffset.UtcNow) is not null)
                {
                    _switch.Settled(succeeded: false);
                }

                break;

            default:
                return Task.CompletedTask;
        }

        Lay();
        return Task.CompletedTask;
    }

    /// <summary>The reason each toggle is most often unavailable for, so the render shows a true sentence.</summary>
    private Themia.Core.Widgets.ToggleBlock PreviewBlock() => _toggle switch
    {
        Themia.Core.Model.QuickToggle.Wifi or Themia.Core.Model.QuickToggle.Bluetooth or Themia.Core.Model.QuickToggle.RotationLock => ToggleBlock.NoHardware,
        Themia.Core.Model.QuickToggle.Sound or Themia.Core.Model.QuickToggle.Microphone => ToggleBlock.NoDevice,
        Themia.Core.Model.QuickToggle.EnergySaver => ToggleBlock.NotOnWindows,
        Themia.Core.Model.QuickToggle.SpatialSound => ToggleBlock.NotSupported,
        _ => ToggleBlock.NoSwitch,
    };
}
#endif
