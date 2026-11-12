#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): <c>sample</c> is the bin's sample (<see cref="ShowSampleAsync"/>); <c>empty</c> is an empty one.</summary>
internal sealed partial class RecycleBinView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "empty"];

    public Task ShowPreviewStateAsync(string state)
        => state == "sample" ? ShowSampleAsync() : ShowBin(new RecycleBinState(0, 0));
}
#endif
