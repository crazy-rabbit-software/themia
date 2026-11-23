using Themia.App.Shell.Gallery;
using Themia.Sdk;
using Themia.Sdk.Samples;

namespace Themia.Widget.Meetings;

/// <summary>
/// Debug renders only ([S-1917], [S-2711], [S-2054]): <c>sample</c> (<see cref="ShowSampleAsync"/>); <c>running</c> the design
/// review under way (11:20, its bar how far it has run); <c>none</c> a calendar with nothing coming; <c>nolink</c> the sample
/// with no join link anywhere, so no Join ([S-2592]).
/// </summary>
internal sealed partial class MeetingView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "running", "none", "nolink"];

    public async Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                await ShowSampleAsync().ConfigureAwait(true);
                return;
            case "running" or "none" or "nolink":
                await ShowCalendarAsync(state == "running" ? SampleDay.TodayAt(new TimeOnly(11, 20)) : SampleDay.Now, empty: state == "none").ConfigureAwait(true);
                if (state == "nolink")
                {
                    _feed.Events = [.. _feed.Events.Select(e => e with { JoinUrl = null })];
                    Render();
                }

                return;
        }
    }
}

/// <summary>
/// Debug renders only: <c>sample</c> (<see cref="ShowSampleAsync"/>); <c>running</c> at 11:20, the design review under way;
/// <c>none</c> a calendar with nothing coming.
/// </summary>
internal sealed partial class MeetingsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "running", "none"];

    public Task ShowPreviewStateAsync(string state) => state switch
    {
        "sample" => ShowSampleAsync(),
        "running" => ShowCalendarAsync(SampleDay.TodayAt(new TimeOnly(11, 20)), empty: false),
        "none" => ShowCalendarAsync(SampleDay.Now, empty: true),
        _ => Task.CompletedTask,
    };
}
