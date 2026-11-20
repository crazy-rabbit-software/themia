using Themia.App.Shell.Gallery;
using Themia.App.Widgets;

namespace Themia.Widget.ScreenTime;

/// <summary>Debug renders only ([S-1917]): the screen-time widgets' one sample history (<see cref="SampleScreenTime"/>; <c>sample</c> is <see cref="ShowSampleAsync"/>) and its linked states.</summary>
internal sealed partial class ScreenTimeView : IWidgetPreviewState
{
    /// <summary>The sample history's states and <c>off</c>: nothing recording, the default kept ([S-12813]).</summary>
    public IReadOnlyList<string> PreviewStates => [.. SampleScreenTime.States, "off"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            return ShowSampleAsync();
        }

        if (state == "off")
        {
            _feed.Use(null);
            Render();
            return Task.CompletedTask;
        }

        _feed.Use(new SampleScreenTime(state));
        ScreenTimePreviewStates.Apply(_links, state);
        Render();
        return Task.CompletedTask;
    }
}
