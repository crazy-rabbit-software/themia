#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): the screen-time widgets' one sample history (<see cref="SampleScreenTime"/>) and its linked states.</summary>
internal sealed partial class ScreenTimeAppsView : IWidgetPreviewState
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
