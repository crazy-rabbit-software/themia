#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): a widget holding 3, 7 or 20 shortcuts, in the grid (<c>grid3</c>,
/// <c>grid7</c>, <c>grid20</c>), the list (<c>list7</c>) or the details view (<c>details7</c>), so a
/// render shows where the items sit in the tile. Local locations only: no favicon is fetched.
/// The fixture is applied, never saved.
/// </summary>
internal sealed partial class ShortcutsView : IWidgetPreviewState
{
    private static readonly string[] PreviewShellNames =
    [
        "RecycleBinFolder", "MyComputerFolder", "Downloads", "Personal", "Desktop", "My Pictures",
        "My Music", "My Video", "UsersFilesFolder", "ControlPanelFolder", "NetworkPlacesFolder", "Profile",
    ];

    private static readonly string[] PreviewPaths =
    [
        @"C:\Windows", @"C:\Program Files", @"C:\Users", @"C:\Windows\System32",
        @"C:\Windows\notepad.exe", @"C:\Windows\explorer.exe", @"C:\ProgramData", @"C:\Windows\Fonts",
    ];

    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "grid3", "grid7", "grid20", "list7", "details7"];

    public Task ShowPreviewStateAsync(string state)
    {
        // [S-6006] "sample": seven items in whatever style the render chose (--style).
        var (view, count) = state switch
        {
            "sample" => (_config.Style, 7),
            "grid3" => (ShortcutsStyle.Grid, 3),
            "grid7" => (ShortcutsStyle.Grid, 7),
            "grid20" => (ShortcutsStyle.Grid, 20),
            "list7" => (ShortcutsStyle.List, 7),
            "details7" => (ShortcutsStyle.Details, 7),
            _ => (ShortcutsStyle.Grid, 0),
        };

        if (count == 0)
        {
            return Task.CompletedTask;
        }

        var targets = PreviewShellNames.Select(LaunchTargets.ShellTarget).Concat(PreviewPaths).Take(count);
        ApplyConfig(new ShortcutsConfig
        {
            Style = view,
            Items = targets.Select(target => ShortcutItem.Create(target)).ToList(),
        });

        return Task.CompletedTask;
    }
}
#endif
