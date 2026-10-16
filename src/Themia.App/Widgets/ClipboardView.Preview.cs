#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: <c>confirm</c> opens the clear question ([S-1500]) over the empty history a
/// render has ([S-1443]); nothing is cleared.
/// </summary>
internal sealed partial class ClipboardView : IWidgetPreviewState
{
    IReadOnlyList<string> IWidgetPreviewState.PreviewStates { get; } = ["confirm"];

    Task IWidgetPreviewState.ShowPreviewStateAsync(string state)
    {
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
