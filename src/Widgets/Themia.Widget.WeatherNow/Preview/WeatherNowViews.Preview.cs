using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Widget.WeatherNow;

/// <summary>
/// Debug renders only ([S-1917], [S-2052]): <c>sample</c> the sample day's reading; <c>stale</c> that reading through a failed
/// refresh (the status's problem glyph); <c>failed</c> no reading and the service out of reach (the face kept, why its
/// tooltip and its line where the card has the room, the failure the status's problem glyph, [S-15710], [S-15711]).
/// </summary>
internal sealed partial class WeatherSkyView
{
    protected override IReadOnlyList<string> OwnPreviewStates { get; } = ["sample", "stale", "failed"];

    protected override Task ShowOwnPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "stale":
                Hold(WeatherNowSample.Answer(stale: true), FetchStatus.Ok);
                return Task.CompletedTask;
            case "failed":
                Hold(null, FetchStatus.Unreachable);
                return Task.CompletedTask;
            default:
                return ShowSampleAsync();
        }
    }
}

/// <inheritdoc cref="WeatherSkyView"/>
internal sealed partial class WeatherConditionView
{
    protected override IReadOnlyList<string> OwnPreviewStates { get; } = ["sample", "stale", "failed"];

    protected override Task ShowOwnPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "stale":
                Hold(WeatherNowSample.Answer(stale: true), FetchStatus.Ok);
                return Task.CompletedTask;
            case "failed":
                Hold(null, FetchStatus.Unreachable);
                return Task.CompletedTask;
            default:
                return ShowSampleAsync();
        }
    }
}
