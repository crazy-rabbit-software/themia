#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only: three alarms set, one of them ringing, or snoozed.</summary>
internal sealed partial class AlarmView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["set", "ringing", "snoozed"];

    public Task ShowPreviewStateAsync(string state)
    {
        _config = new AlarmConfig
        {
            Alarms =
            [
                new AlarmEntry { Id = "wake", Time = "06:45", Days = ["mon", "tue", "wed", "thu", "fri"], On = true, Label = "Wake up" },
                new AlarmEntry { Id = "train", Time = "07:30", On = true, Label = "Train" },
                new AlarmEntry { Id = "weekend", Time = "09:00", Days = ["sat", "sun"], On = false },
            ],
        };

        var now = DateTime.UtcNow;
        _previewStatus = alarm => (state, alarm.Id) switch
        {
            ("ringing", "wake") => new AlarmStatus(AlarmPhase.Ringing),
            ("snoozed", "wake") => new AlarmStatus(AlarmPhase.Snoozed, Until: now.AddMinutes(9)),
            _ when alarm.On => new AlarmStatus(AlarmPhase.Set, AlarmSchedule.Next(alarm.Plan, now, TimeZoneInfo.Local)),
            _ => new AlarmStatus(AlarmPhase.Off),
        };

        Lay();
        return Task.CompletedTask;
    }
}
#endif
