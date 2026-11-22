using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): the screen-time widgets' one sample history (<see cref="SampleScreenTime"/>; <c>sample</c>
/// is <see cref="ShowSampleAsync"/>) and its linked states; [S-15774] <c>empty</c> (a recording with nothing in the window)
/// and <c>off</c> (no recording at all): the chart kept, why in its caption. [S-15773] <c>hovered</c> and <c>selected</c>
/// light the sample app's part of today's bar, as the pointer on it and a click on it do.
/// </summary>
internal sealed partial class ScreenTimeChartView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates => [.. SampleScreenTime.States, "empty", "off"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            return ShowSampleAsync();
        }

        _feed.Use(state == "off" ? null : new SampleScreenTime(state));
        ScreenTimePreviewStates.Apply(_links, state);
        Render();

        var today = (_data?.Range.Count ?? 0) - 1;
        if (today >= 0 && state == "hovered")
        {
            HoverPart(new ChartPart(SampleScreenTime.HoveredApp, today));
        }
        else if (today >= 0 && state == "selected")
        {
            _pickPart = new ChartPart(SampleScreenTime.SelectedApp, today);
            ApplyFocus();
        }

        return Task.CompletedTask;
    }
}
