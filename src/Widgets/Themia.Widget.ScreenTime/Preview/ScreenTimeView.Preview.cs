#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.App.Widgets;

namespace Themia.Widget.ScreenTime;

/// <summary>Debug renders only ([S-1917]): the screen-time widgets' one sample history (<see cref="SampleScreenTime"/>) and its linked states.</summary>
internal sealed partial class ScreenTimeView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates => SampleScreenTime.States;

    public Task ShowPreviewStateAsync(string state)
    {
        UseSource(new SampleScreenTime(state));
        ScreenTimePreviewStates.Apply(_links, state);
        Render();
        return Task.CompletedTask;
    }
}
#endif
