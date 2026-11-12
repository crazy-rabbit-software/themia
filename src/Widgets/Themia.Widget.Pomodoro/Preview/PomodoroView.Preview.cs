#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> is the timer's sample (<see cref="ShowSampleAsync"/>);
/// <c>running</c> is the same phase going (Pause, no tick subscribed, so the render holds still);
/// <c>least</c> a fresh 5-minute break, where − is disabled ([S-8111]).
/// </summary>
internal sealed partial class PomodoroView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "running", "least"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            return ShowSampleAsync();
        }

        if (state == "running")
        {
            ShowPhase(running: true);
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
