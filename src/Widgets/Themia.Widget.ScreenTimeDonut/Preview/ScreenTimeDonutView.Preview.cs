#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): the screen-time widgets' one sample history (<see cref="SampleScreenTime"/>; <c>sample</c> is <see cref="ShowSampleAsync"/>) and its linked states.</summary>
internal sealed partial class ScreenTimeDonutView : IWidgetPreviewState
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
            UseSource(null);
            Render();
            return Task.CompletedTask;
        }

        UseSource(new SampleScreenTime(state));
        ScreenTimePreviewStates.Apply(_links, state);
        Render();
        return Task.CompletedTask;
    }
}
#endif
