using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-1917] The timer's sample: a focus phase with 9:24 left of 25 minutes, paused, so the ring
/// and the bar ([S-6035]) show the same part of a phase in every picture.
/// </summary>
internal sealed partial class PomodoroView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        ShowPhase(running: false);
        return Task.CompletedTask;
    }

    /// <summary>The sample's phase, paused or with the running look (no tick subscribed, so it holds still).</summary>
    internal void ShowPhase(bool running)
    {
        Load(new PomodoroState(PomodoroPhase.Work, (9 * 60) + 24));
        _running = running;
        Lay();
        Render();
    }
}
