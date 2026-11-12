using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.FocusSession;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11305] The focus widget on the timers' sized face ([S-11300]): the time a one-readout Readout on the
/// step its Size names, centred, its status line under it, or with Ring on, one step under in the least
/// ring that holds it; the Controls row under it (Start with − and + while idle, End session while it
/// runs; at Small icon buttons). Sized for the widest reading of its length
/// (<see cref="FocusSessionRules.WidestClock"/>), so it never resizes as it counts.
/// </summary>
public static class FocusSessionLayout
{
    /// <summary>[S-11305] The widest the time reads for <paramref name="config"/>'s length, with its line under.</summary>
    public static ReadoutText Worst(FocusSessionConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new ReadoutText(FocusSessionRules.WidestClock(config.Minutes), LineUnder: true, OneReadout: true);
    }

    /// <summary>[S-11305] The face at the widget's Size, in its ring where Ring is on.</summary>
    public static TimerSized Sized(FocusSessionConfig config) => TimerFace.Sized(config.Size, Worst(config), ring: config.Ring);

    /// <summary>
    /// [S-11305] The focus widget's sizing ([S-2438]): its face over the Controls row, as wide as the
    /// wider of the idle row (Start worded, − and +) and the running one (End session worded); one height,
    /// up to half again wider. No header by default.
    /// </summary>
    public static WidgetSizing Sizing(FocusSessionConfig config, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(config);
        var controls = Math.Max(
            TimerFace.ControlsWidth(config.Size, [FocusSessionStr.Panels_Focus_Start.Text(locale)], icons: 2),
            TimerFace.ControlsWidth(config.Size, [FocusSessionStr.Panels_Focus_End.Text(locale)]));
        return TimerFace.Sizing(Sized(config), controls, header: false);
    }
}
