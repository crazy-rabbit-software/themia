using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-4623]..[S-4627] The alarm of every alarm widget as a widget reaches it (<see cref="IWidgetHost.Alarms"/>):
/// the host's one alarm service, which rings them whether or not a widget shows them (its waitable
/// timer, the ringtone, the Windows notification stay host code). A widget asks what its alarm is
/// doing, by its widget id, and snoozes or dismisses it.
/// </summary>
public interface IAlarms
{
    /// <summary>What an alarm is doing changed (rang, silenced, synced): views draw again. UI thread.</summary>
    event Action? Changed;

    /// <summary>The zone alarms are reckoned in.</summary>
    TimeZoneInfo Zone { get; }

    /// <summary>What the alarm of widget <paramref name="widgetId"/> is doing now.</summary>
    AlarmStatus Status(string widgetId);

    /// <summary>[S-4623] Take every view's alarms again (a switch on a widget changed its own).</summary>
    void Sync();

    /// <summary>[S-4625] Snooze a ringing alarm.</summary>
    void Snooze(string widgetId);

    /// <summary>[S-4625] Dismiss a ringing or snoozed alarm.</summary>
    void Dismiss(string widgetId);
}
