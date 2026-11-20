using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-1917] The timer's sample: the classic cycle's second focus phase (third of eight) with 9:24 left of 25
/// minutes, paused, so the sections done, the one filling and the ones to come ([S-8115]) show in every picture.
/// </summary>
internal sealed partial class PomodoroView : IWidgetSample
{
    /// <summary>The sample's phase: the second focus of the classic cycle.</summary>
    internal const int SamplePhase = 2;

    /// <summary>The sample's time left: 9:24.</summary>
    internal const int SampleSeconds = (9 * 60) + 24;

    public Task ShowSampleAsync()
    {
        ShowPhase(SamplePhase, SampleSeconds, running: false);
        return Task.CompletedTask;
    }

    /// <summary>A phase with <paramref name="seconds"/> left, paused or with the running look (no tick subscribed, so it holds still).</summary>
    internal void ShowPhase(int index, int seconds, bool running)
    {
        var at = PomodoroCycle.Normalise(index, _lengths.Length);
        Load(new PomodoroState(at, Math.Min(seconds, _lengths.Length > 0 ? _lengths[at] : seconds)));
        _running = running;
        Lay();
        Render();
    }
}
