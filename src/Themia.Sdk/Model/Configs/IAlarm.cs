using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-4620] [S-4623] One alarm as the host's alarm service rings it: what an alarm widget's config
/// holds. The service reads every view's widgets for this (<c>AlarmSlot.Of</c>) instead of naming a
/// widget; a one-shot alarm switches itself off through <see cref="On"/> when its ring is over.
/// </summary>
public interface IAlarm
{
    /// <summary>When it rings, "HH:mm" on the 24-hour clock.</summary>
    string Time { get; }

    /// <summary>The days it repeats on ("mon" … "sun"); null for one that rings once.</summary>
    string[]? Days { get; }

    /// <summary>Whether it is set to ring.</summary>
    bool On { get; set; }

    /// <summary>Its own name; empty for "Alarm".</summary>
    string Label { get; }

    /// <summary>[S-4626] The ringtone; Off rings silently (the notice only).</summary>
    AlarmSound Sound { get; }

    /// <summary>The ring's level, 0 to 100.</summary>
    int EffectiveVolume { get; }

    /// <summary>[S-4625] How long a snooze is.</summary>
    TimeSpan Snooze { get; }

    /// <summary>Its time, days and switch, as the book reckons with them.</summary>
    AlarmPlan Plan { get; }
}
