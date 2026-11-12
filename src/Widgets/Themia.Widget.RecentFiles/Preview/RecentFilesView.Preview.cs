#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917], [S-2056]): <c>sample</c> is the recent files' sample (<see cref="ShowSampleAsync"/>); <c>empty</c> none.</summary>
internal sealed partial class RecentFilesView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "empty"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            return ShowSampleAsync();
        }

        ShowFiles(SampleDay.Now, []);
        return Task.CompletedTask;
    }
}
#endif
