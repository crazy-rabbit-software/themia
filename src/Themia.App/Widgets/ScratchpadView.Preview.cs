#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Model;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: <c>confirm</c> draws a sample stroke and opens the clear confirmation
/// ([S-1500]). The render's flyout is constrained to its window so the render can draw it (a popup
/// of its own window does not render to a bitmap); on the desktop it never is.
/// </summary>
internal sealed partial class ScratchpadView : IWidgetPreviewState
{
    IReadOnlyList<string> IWidgetPreviewState.PreviewStates { get; } = ["confirm"];

    Task IWidgetPreviewState.ShowPreviewStateAsync(string state)
    {
        if (state != "confirm")
        {
            return Task.CompletedTask;
        }

        _config.Strokes.Add(new InkStroke
        {
            Color = ScratchpadConfig.DefaultPenColor,
            Points = [0.2, 0.6, 0.35, 0.35, 0.5, 0.55, 0.65, 0.3, 0.8, 0.5],
        });
        RenderAll();
        UpdateCommands();

        if (_clearButton.Visibility == Microsoft.UI.Xaml.Visibility.Visible && _clearButton.Flyout is { } flyout)
        {
            flyout.ShouldConstrainToRootBounds = true;
            flyout.ShowAt(_clearButton);

            // The flyout opens with an animation; the render draws it once it has settled.
            return Task.Delay(900);
        }
        else
        {
            Console.WriteLine("widget: Scratchpad's clear is not in the header at this size");
        }

        return Task.CompletedTask;
    }
}
#endif
