#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only: a stopwatch running, paused with laps, and past an hour with many laps.</summary>
internal sealed partial class StopwatchView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["running", "paused", "laps", "hours"];

    public Task ShowPreviewStateAsync(string state)
    {
        var now = Now();
        var start = now - TimeSpan.FromSeconds(83.4);

        _state = state switch
        {
            "running" => StopwatchState.Start(start).Lap(start + TimeSpan.FromSeconds(31.2)),
            "paused" => StopwatchState.Start(start).Lap(start + TimeSpan.FromSeconds(31.2)).Lap(start + TimeSpan.FromSeconds(58.9)).Pause(now),
            "laps" => Laps(StopwatchState.Start(now - TimeSpan.FromSeconds(754.5)), now - TimeSpan.FromSeconds(754.5), 12),
            "hours" => Laps(StopwatchState.Start(now - TimeSpan.FromSeconds(3723.4)), now - TimeSpan.FromSeconds(3723.4), 4).Pause(now),
            _ => _state,
        };

        // A render shows the state; it never subscribes (the render's ticker stops with it).
        Render();
        Lay();
        return Task.CompletedTask;

        static StopwatchState Laps(StopwatchState running, TimeSpan from, int count)
        {
            var at = from;
            for (var i = 0; i < count; i++)
            {
                at += TimeSpan.FromSeconds(47 + (i * 7 % 23) + (i * 0.3));
                running = running.Lap(at);
            }

            return running;
        }
    }
}
#endif
