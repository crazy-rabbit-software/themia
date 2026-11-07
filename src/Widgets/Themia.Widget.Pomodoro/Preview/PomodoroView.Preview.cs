#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> is a focus phase with 9:24 left of 25 minutes,
/// paused, so the ring and the bar ([S-6035]) show the same part of a phase in every render;
/// <c>running</c> is the same phase going (Pause, no tick subscribed, so the render holds still);
/// <c>least</c> a fresh 5-minute break, where − is disabled ([S-8111]).
/// </summary>
internal sealed partial class PomodoroView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "running", "least"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is "sample" or "running")
        {
            Load(new PomodoroState(PomodoroPhase.Work, (9 * 60) + 24));

            // The running look without a tick: a render is one frame, and a subscription would move it.
            _running = state == "running";
            Lay();
            Render();
        }
        else if (state == "least")
        {
            Load(new PomodoroState(PomodoroPhase.Break, PomodoroCycle.PhaseSeconds(PomodoroPhase.Break, _config)));
            Lay();
            Render();
        }

        return Task.CompletedTask;
    }
}
#endif
