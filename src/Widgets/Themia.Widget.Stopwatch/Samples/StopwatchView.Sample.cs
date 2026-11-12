using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>[S-11850] The stopwatch's sample: running for 1:23 with one lap taken. It never subscribes: a picture is a still.</summary>
internal sealed partial class StopwatchView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        var now = Now();
        var start = now - TimeSpan.FromSeconds(83.4);
        ShowState(StopwatchState.Start(start).Lap(start + TimeSpan.FromSeconds(31.2)));
        return Task.CompletedTask;
    }

    /// <summary>Show <paramref name="state"/> as it is; the ticker is not asked to move it.</summary>
    private void ShowState(StopwatchState state)
    {
        _state = state;
        Render();
        Lay();
    }
}
