#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917], [S-15006]): a family's own states (its sample, idle, failed, ...), in its order.</summary>
public abstract partial class IconView : IWidgetPreviewState
{
    /// <summary>The family's own render states, in its order.</summary>
    protected virtual IReadOnlyList<string> OwnPreviewStates => [];

    /// <summary>Show one of <see cref="OwnPreviewStates"/>.</summary>
    protected virtual Task ShowOwnPreviewStateAsync(string state) => Task.CompletedTask;

    public IReadOnlyList<string> PreviewStates => OwnPreviewStates;

    public Task ShowPreviewStateAsync(string state) => ShowOwnPreviewStateAsync(state);
}
#endif
