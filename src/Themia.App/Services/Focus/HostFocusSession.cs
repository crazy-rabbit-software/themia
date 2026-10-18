using Themia.Sdk.WinUI;

namespace Themia.App.Services.Focus;

/// <summary>[S-4421] The app's one focus session (<see cref="FocusSessionService"/>) as a widget's host hands it over.</summary>
internal sealed class HostFocusSession : IFocusSession
{
    public static HostFocusSession Instance { get; } = new();

    private HostFocusSession()
    {
    }

    public FocusSnapshot Snapshot => FocusSessionService.Snapshot;

    public IDisposable Acquire(Action changed) => FocusSessionService.Acquire(changed);

    public void Refresh() => FocusSessionService.Refresh();

    public void Start(int minutes, bool doNotDisturb) => FocusSessionService.Start(minutes, doNotDisturb);

    public void End() => FocusSessionService.End();
}
