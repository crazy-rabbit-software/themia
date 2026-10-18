#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> is a focus phase with 9:24 left of 25 minutes,
/// paused, so the ring and the bar ([S-6035]) show the same part of a phase in every render.
/// </summary>
internal sealed partial class PomodoroView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            _state = new PomodoroState(PomodoroPhase.Work, (9 * 60) + 24);
            Render();
            Lay();
        }

        return Task.CompletedTask;
    }
}
#endif
