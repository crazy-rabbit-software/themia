using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>[S-11850] [S-2050] The alarm's sample: at the sample moment, a weekday alarm at 6:45, set.</summary>
internal sealed partial class AlarmView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        HoldSample(on: true, now => new AlarmStatus(AlarmPhase.Set, AlarmSchedule.Next(_config.Plan, now, TimeZoneInfo.Local)));
        return Task.CompletedTask;
    }

    /// <summary>The sample's weekday alarm (switched <paramref name="on"/> or off) at the sample moment, doing what <paramref name="status"/> says.</summary>
    internal void HoldSample(bool on, Func<DateTime, AlarmStatus> status)
    {
        _config = new AlarmConfig { Time = "06:45", Days = ["mon", "tue", "wed", "thu", "fri"], On = on, Label = "Wake up", Size = _config.Size };

        var now = SampleDay.Now.UtcDateTime;
        _clock = () => now;
        _previewStatus = status(now);
        Lay();

        // A picture is drawn at once: the switch shows where it is, not on its way there.
        Microsoft.UI.Xaml.VisualStateManager.GoToState(_switch, on ? "On" : "Off", useTransitions: false);
    }
}
