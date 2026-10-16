#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only: a weekday alarm at 6:45 set, ringing, snoozed, or switched off.</summary>
internal sealed partial class AlarmView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["set", "ringing", "snoozed", "off"];

    public Task ShowPreviewStateAsync(string state)
    {
        _config = new AlarmConfig { Time = "06:45", Days = ["mon", "tue", "wed", "thu", "fri"], On = state != "off", Label = "Wake up" };

        var now = DateTime.UtcNow;
        _previewStatus = state switch
        {
            "ringing" => new AlarmStatus(AlarmPhase.Ringing),
            "snoozed" => new AlarmStatus(AlarmPhase.Snoozed, Until: now.AddMinutes(9)),
            "off" => new AlarmStatus(AlarmPhase.Off),
            _ => new AlarmStatus(AlarmPhase.Set, AlarmSchedule.Next(_config.Plan, now, TimeZoneInfo.Local)),
        };

        Lay();
        return Task.CompletedTask;
    }
}
#endif
