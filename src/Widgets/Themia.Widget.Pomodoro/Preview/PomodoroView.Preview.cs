#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]), on the classic cycle ([S-8113]): <c>sample</c> is the timer's sample
/// (<see cref="ShowSampleAsync"/>: the second focus, 9:24 left, paused); <c>idle</c> a fresh cycle; <c>running</c> the
/// sample going; <c>break</c> the second short break going, 3:12 left; <c>paused</c> the third short break paused, 2:40
/// left; <c>between</c> the moment after a flip: the second short break just loaded in full, going; <c>last</c> the long
/// break going, 11:05 left; <c>least</c> a fresh 5-minute break, where − is disabled ([S-8111]). Pause shows while
/// going; no tick is subscribed, so the render holds still.
/// </summary>
internal sealed partial class PomodoroView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "idle", "running", "break", "paused", "between", "last", "least"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                return ShowSampleAsync();
            case "idle":
                ShowPhase(0, int.MaxValue, running: false);
                break;
            case "running":
                ShowPhase(SamplePhase, SampleSeconds, running: true);
                break;
            case "break":
                ShowPhase(3, (3 * 60) + 12, running: true);
                break;
            case "paused":
                ShowPhase(5, (2 * 60) + 40, running: false);
                break;
            case "between":
                ShowPhase(3, int.MaxValue, running: true);
                break;
            case "last":
                ShowPhase(7, (11 * 60) + 5, running: true);
                break;
            case "least":
                ShowPhase(1, int.MaxValue, running: false);
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
