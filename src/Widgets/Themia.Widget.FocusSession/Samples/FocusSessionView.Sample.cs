using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>[S-11850] The focus session's sample: no session running, never this PC's do not disturb.</summary>
internal sealed partial class FocusSessionView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _preview = new FocusSnapshot(FocusSessionState.Idle, ToggleReading.Pending, false, false);
        Lay();
        return Task.CompletedTask;
    }
}
