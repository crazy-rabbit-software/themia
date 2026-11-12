#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-2050]): at the sample moment, a weekday alarm at 6:45 set (the sample, <see cref="ShowSampleAsync"/>), ringing, snoozed, or switched off.</summary>
internal sealed partial class AlarmView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["set", "ringing", "snoozed", "off"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "ringing":
                HoldSample(on: true, _ => new AlarmStatus(AlarmPhase.Ringing));
                break;
            case "snoozed":
                HoldSample(on: true, now => new AlarmStatus(AlarmPhase.Snoozed, Until: now.AddMinutes(9)));
                break;
            case "off":
                HoldSample(on: false, _ => new AlarmStatus(AlarmPhase.Off));
                break;
            default:
                return ShowSampleAsync();
        }

        return Task.CompletedTask;
    }
}
#endif
