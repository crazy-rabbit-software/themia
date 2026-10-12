#if DEBUG
using Themia.App.Services.Focus;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): made-up sessions, never this PC's do not disturb.</summary>
internal sealed partial class FocusSessionView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["idle", "running", "quiet", "unavailable", "windows", "long"];

    public Task ShowPreviewStateAsync(string state)
    {
        var now = DateTimeOffset.Now;

        // Twelve minutes gone of thirty: the ring a little over a third.
        var running = new FocusSessionState(now.AddMinutes(-12), now.AddMinutes(18).AddSeconds(-7));
        _preview = state switch
        {
            "idle" => new FocusSnapshot(FocusSessionState.Idle, ToggleReading.Pending, false, false),
            "running" => new FocusSnapshot(running, ToggleReading.Pending, false, false),
            "quiet" => new FocusSnapshot(running, ToggleReading.Of(true), true, false),
            "unavailable" => new FocusSnapshot(running, ToggleReading.Blocked(ToggleBlock.NoSwitch), true, false),
            "windows" => new FocusSnapshot(FocusSessionState.Idle, ToggleReading.Pending, false, true),
            "long" => new FocusSnapshot(new FocusSessionState(now.AddMinutes(-5), now.AddMinutes(85)), ToggleReading.Of(true), true, false),
            _ => _preview,
        };

        if (state == "running")
        {
            _config.DoNotDisturb = false;
        }

        Lay();
        return Task.CompletedTask;
    }
}
#endif
