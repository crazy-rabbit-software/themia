using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.RainNextHour;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> the sample day's shower; <c>dry</c> a dry window; <c>stale</c> the sample
/// kept through a failed refresh; <c>failed</c> no answer at all (the face kept, why in words, [S-12813]).
/// </summary>
internal sealed partial class RainOutlookView
{
    protected override IReadOnlyList<string> OwnPreviewStates { get; } = ["sample", "dry", "stale", "failed"];

    protected override Task ShowOwnPreviewStateAsync(string state)
    {
        if (state == "failed")
        {
            _feed.Preview(RainSample.Placed(new RainOutlookConfig()), SampleDay.Now, null, FetchStatus.Unreachable);
        }
        else
        {
            Hold(dry: state == "dry", stale: state == "stale");
        }

        return Task.CompletedTask;
    }
}

/// <inheritdoc cref="RainOutlookView"/>
internal sealed partial class RainTotalView
{
    protected override IReadOnlyList<string> OwnPreviewStates { get; } = ["sample", "dry", "stale", "failed"];

    protected override Task ShowOwnPreviewStateAsync(string state)
    {
        if (state == "failed")
        {
            _feed.Preview(RainSample.Placed(new RainTotalConfig()), SampleDay.Now, null, FetchStatus.Unreachable);
        }
        else
        {
            Hold(dry: state == "dry", stale: state == "stale");
        }

        return Task.CompletedTask;
    }
}
