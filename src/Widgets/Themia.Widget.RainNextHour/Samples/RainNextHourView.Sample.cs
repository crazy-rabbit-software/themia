using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;
using Themia.Widget.RainNextHour;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2052] The rain graph's sample: the sample day's shower from a quarter past eleven (<see cref="RainSample"/>).
/// Nothing is fetched or saved.
/// </summary>
internal sealed partial class RainNextHourView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        ShowNowcast(dry: false, stale: false);
        return Task.CompletedTask;
    }

    /// <summary>A nowcast at the sample place and moment, dry or the sample's shower, kept through a failed refresh when <paramref name="stale"/>.</summary>
    internal void ShowNowcast(bool dry, bool stale)
    {
        _config = RainSample.Placed(new RainNextHourConfig());
        _pinnedNow = SampleDay.Now;
        _feed.Preview(RainSample.Answer(dry, stale), FetchStatus.Ok);
    }
}
