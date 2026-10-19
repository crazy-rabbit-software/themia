using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-2851] Whether Windows' location is asked for at all: while any view on any display holds a
/// widget about a place with its device-location switch on, whether or not it is on screen, as the
/// sync keeps every view's places (so a view switched to has its weather). None: nothing is asked,
/// no access is requested and no locator runs.
/// </summary>
public static class DeviceLocationInterest
{
    public static bool Wanted(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Views.SelectMany(view => view.Widgets).Any(widget => widget.Config is IPlaced { UseDeviceLocation: true });
    }
}
