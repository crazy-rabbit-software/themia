#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2056]): <c>sample</c> is the history's sample (<see cref="ShowSampleAsync"/>);
/// <c>confirm</c> opens the clear question ([S-1500]) over the empty history a render has; nothing is
/// copied or cleared.
/// </summary>
internal sealed partial class ClipboardView : IWidgetPreviewState
{
    IReadOnlyList<string> IWidgetPreviewState.PreviewStates { get; } = ["sample", "confirm"];

    Task IWidgetPreviewState.ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            return ShowSampleAsync();
        }

        if (state != "confirm")
        {
            return Task.CompletedTask;
        }

        // Never answered: the render closes with the question open.
        _ = AskToClearAsync();

        // The flyout opens with an animation; the render draws it once it has settled.
        return Task.Delay(900);
    }
}
#endif
