using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// The apps this PC has, read off the Start menu (<see cref="IWidgetHost.InstalledApps"/>): what a
/// Start-menu app dropped on a widget is matched against. The walk stays host code.
/// </summary>
public interface IInstalledApps
{
    /// <summary>At most <paramref name="maxCount"/> apps. <b>Background work</b>: two directory trees, never on the UI thread.</summary>
    IReadOnlyList<InstalledApp> Read(int maxCount, CancellationToken token);
}
