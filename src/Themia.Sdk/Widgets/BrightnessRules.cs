using Themia.App.Services.Brightness;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>How a display's brightness is reached.</summary>
public enum BrightnessPath
{
    /// <summary>A laptop's or tablet's own panel: Windows' power policy, the path its own slider takes.</summary>
    BuiltIn = 0,

    /// <summary>A monitor that answers DDC/CI's luminance (VCP 0x10) through the Monitor Configuration API.</summary>
    Ddc = 1,
}

/// <summary>[S-4441] One display whose brightness can be set, as last read.</summary>
/// <param name="Device">Its GDI name (<c>\\.\DISPLAY1</c>): what a level is asked for by.</param>
/// <param name="Path">How it is reached.</param>
/// <param name="Level">0..100, as the display last said.</param>
public sealed record BrightnessDisplay(string Device, BrightnessPath Path, int Level);

/// <summary>
/// [S-4440] The brightness slider's rules: which display it shows and which it sets, a monitor's own
/// range in percent, and the keys. Pure, so they are tested without a monitor.
/// </summary>
public static class BrightnessRules
{
    /// <summary>
    /// [S-4442] The display one slider shows, for a widget on <paramref name="own"/>: its own where it
    /// has a brightness; setting every display, the first that has one where its own has none; setting
    /// its own alone, none.
    /// </summary>
    public static BrightnessDisplay? Shown(IReadOnlyList<BrightnessDisplay> displays, string? own, BrightnessScreens screens)
    {
        ArgumentNullException.ThrowIfNull(displays);
        var mine = displays.FirstOrDefault(display => string.Equals(display.Device, own, StringComparison.OrdinalIgnoreCase));
        if (mine is not null)
        {
            return mine;
        }

        return screens == BrightnessScreens.All && displays.Count > 0 ? displays[0] : null;
    }

    /// <summary>
    /// [S-4447] Whether the choice of screens can change anything: with two displays with a brightness
    /// or more, or while the widget's own screen alone is chosen (so the choice can always go back to
    /// every screen). With one, "every screen" and "the one it is on" set the same display, or the own
    /// choice sets none. Offered too until the displays have been looked at: nothing is known yet.
    /// </summary>
    public static bool OffersScreens(BrightnessSnapshot displays, BrightnessScreens screens)
    {
        ArgumentNullException.ThrowIfNull(displays);
        return !displays.Ready || displays.Displays.Count >= 2 || screens != BrightnessScreens.All;
    }

    /// <summary>[S-4442] The displays one slider sets: every one with a brightness, or the widget's own alone.</summary>
    public static IReadOnlyList<BrightnessDisplay> Dimmed(IReadOnlyList<BrightnessDisplay> displays, string? own, BrightnessScreens screens)
    {
        ArgumentNullException.ThrowIfNull(displays);
        if (screens == BrightnessScreens.All)
        {
            return displays;
        }

        return [.. displays.Where(display => string.Equals(display.Device, own, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// [S-4441] A monitor's own reading (<c>GetMonitorBrightness</c>: minimum, current, maximum) in
    /// percent. Null for a range that is no range (a monitor that answered nonsense).
    /// </summary>
    public static int? Percent(uint minimum, uint current, uint maximum)
    {
        if (maximum <= minimum)
        {
            return null;
        }

        var clamped = Math.Clamp(current, minimum, maximum);
        return (int)Math.Round((clamped - minimum) * 100.0 / (maximum - minimum), MidpointRounding.AwayFromZero);
    }

    /// <summary>[S-4441] A percentage as the monitor's own value, inside its range.</summary>
    public static uint Raw(int percent, uint minimum, uint maximum)
    {
        if (maximum <= minimum)
        {
            return minimum;
        }

        var level = Math.Clamp(percent, 0, 100);
        return minimum + (uint)Math.Round((maximum - minimum) * level / 100.0, MidpointRounding.AwayFromZero);
    }

    /// <summary>[S-4444] One step: the arrows by 1 as Windows' slider, Page Up/Down by 10, Home and End to the ends.</summary>
    public static int? Key(WidgetKey key, int level) => key switch
    {
        WidgetKey.Right or WidgetKey.Up or WidgetKey.Plus => Math.Clamp(level + 1, 0, 100),
        WidgetKey.Left or WidgetKey.Down or WidgetKey.Minus => Math.Clamp(level - 1, 0, 100),
        WidgetKey.PageUp => Math.Clamp(level + 10, 0, 100),
        WidgetKey.PageDown => Math.Clamp(level - 10, 0, 100),
        WidgetKey.Home => 0,
        WidgetKey.End => 100,
        _ => null,
    };

    /// <summary>[S-4444] A wheel notch is two points, as a slider's small change (carry as <see cref="AudioRules.Wheel"/>).</summary>
    public static (int Delta, int Carry) Wheel(int carry, int delta) => AudioRules.Wheel(carry, delta);
}

/// <summary>
/// [S-4443] The levels asked for and not written yet, newest per display. A drag asks at every move; a
/// DDC/CI write takes tens to hundreds of milliseconds; so the worker takes only each display's newest,
/// and the last level asked always wins and nothing queues. Thread-safe.
/// </summary>
public sealed class NewestLevels
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, int> _asked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ask <paramref name="level"/> (clamped to 0..100) for <paramref name="device"/>, replacing an older ask.</summary>
    public void Ask(string device, int level)
    {
        lock (_gate)
        {
            _asked[device] = Math.Clamp(level, 0, 100);
        }
    }

    /// <summary>What is asked now, a copy.</summary>
    public IReadOnlyList<KeyValuePair<string, int>> Pending()
    {
        lock (_gate)
        {
            return [.. _asked];
        }
    }

    /// <summary>The newest level asked for a display, or null once it has been written.</summary>
    public int? Asked(string device)
    {
        lock (_gate)
        {
            return _asked.TryGetValue(device, out var level) ? level : null;
        }
    }

    /// <summary><paramref name="level"/> was written: forgotten, unless a newer one was asked meanwhile.</summary>
    public void Written(string device, int level)
    {
        lock (_gate)
        {
            if (_asked.TryGetValue(device, out var still) && still == level)
            {
                _asked.Remove(device);
            }
        }
    }

    /// <summary>A display that went: anything asked of it goes too.</summary>
    public void Forget(string device)
    {
        lock (_gate)
        {
            _asked.Remove(device);
        }
    }
}
