#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-2050]): <c>sample</c> is the clock's sample (<see cref="ShowSampleAsync"/>); [S-12833] <c>world</c>
/// the sample as a world clock (Tokyo, its offset, seconds) and <c>world12</c> that in 12-hour time, to see its meridiem
/// and line under give way as the card gets smaller (render them at explicit <c>--sizes</c>: the card's range is the
/// default clock's).
/// </summary>
public sealed partial class ClockView : IWidgetPreviewState
{
    IReadOnlyList<string> IWidgetPreviewState.PreviewStates { get; } = ["sample", "world", "world12"];

    Task IWidgetPreviewState.ShowPreviewStateAsync(string state)
    {
        if (state is "world" or "world12")
        {
            if (state == "world12")
            {
                RegionFormat.Apply(RegionFormat.Current with { Use24Hour = false });
            }

            _config = (ClockConfig)_config.Clone();
            _config.TimeZoneId = "Asia/Tokyo";
            _config.OffsetMode = ClockOffsetMode.Utc;
            _config.ShowSeconds = true;
            _label = ClockFormatter.Label(_config);
        }

        return state is "sample" or "world" or "world12" ? ShowSampleAsync() : Task.CompletedTask;
    }
}
#endif
