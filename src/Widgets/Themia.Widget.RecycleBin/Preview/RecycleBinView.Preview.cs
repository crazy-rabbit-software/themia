#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): a fixed bin, so renders compare; <c>empty</c> is an empty one.</summary>
internal sealed partial class RecycleBinView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "empty"];

    public Task ShowPreviewStateAsync(string state)
    {
        RecycleBinState? fixture = state == "sample" ? new RecycleBinState(37, 1_288_490_189) : new RecycleBinState(0, 0);
        _state = () => fixture;
        Render();
        return Task.CompletedTask;
    }
}
#endif
