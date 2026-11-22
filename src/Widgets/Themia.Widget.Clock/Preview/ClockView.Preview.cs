using Themia.App.Shell.Gallery;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-2050]): <c>sample</c> is the clock's sample (<see cref="ShowSampleAsync"/>); [S-12833] <c>world</c>
/// the sample as a world clock (Tokyo, its offset, seconds) and <c>world12</c> that in 12-hour time, to see its meridiem
/// and line under give way as the card gets smaller (render them at explicit <c>--sizes</c>: the card's range is the
/// default clock's); [S-14319] <c>at0111</c> and <c>at1008</c> the sample at 1:11:11 and at 10:08:08, to see the face hold
/// its size and place whatever time it shows (compare their layouts with the sample's); [S-15802] <c>noZone</c> a world clock whose
/// zone this Windows does not resolve: its face tells local time and its line under says why (WR-10).
/// </summary>
public sealed partial class ClockView : IWidgetPreviewState
{
    IReadOnlyList<string> IWidgetPreviewState.PreviewStates { get; } = ["sample", "world", "world12", "at0111", "at1008", "noZone"];

    /// <summary>
    /// [S-11603] A world state's own config (seconds, its city line): a render sizes the card by it, never at the default
    /// clock's least, which holds no seconds (held to "88:88:88" its figures stood 7 into the inset of a 48 card).
    /// </summary>
    Themia.Core.Model.WidgetConfig? IWidgetPreviewState.DrawnConfig => _drawn;

    private ClockConfig? _drawn;

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
            _drawn = _config;
        }

        if (state == "noZone")
        {
            _config = (ClockConfig)_config.Clone();
            _config.TimeZoneId = "Etc/Nowhere";
            _drawn = _config;
        }

        if (state is "at0111" or "at1008")
        {
            var time = state == "at0111" ? new TimeOnly(1, 11, 11) : new TimeOnly(10, 8, 8);
            _frozen = Themia.Sdk.Samples.SampleDay.At(Themia.Sdk.Samples.SampleDay.Today, time, _config.ResolveTimeZone());
            ApplyConfig(_config);
            return Task.CompletedTask;
        }

        return state is "sample" or "world" or "world12" or "noZone" ? ShowSampleAsync() : Task.CompletedTask;
    }
}
