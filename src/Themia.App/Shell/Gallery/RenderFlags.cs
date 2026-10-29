#if DEBUG
namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-68] The renders and checks a Debug build runs instead of the app, by the flag each looks up as
/// its first argument. A flag missing here is refused as unknown and one listed that no render takes is
/// refused too ([S-68]): either way nothing starts and the running copy hears nothing, so a mismatch
/// fails safe. A new render adds its flag here.
/// </summary>
internal static class RenderFlags
{
    public static IReadOnlyCollection<string> All { get; } =
    [
        ShowcaseCommand.Flag,
        "--picker-png",
        "--editbar-png",
        "--iconpicker-png",
        "--viewbar-png",
        "--desktop-items-check",
        DesktopMenuCheck.Flag,
        "--notices-png",
        "--layout-editor-png",
        "--widget-png",
        "--menu-png",
        "--motion-png",
        "--grid-png",
        "--switch-check",
        "--drop-check",
        "--family-check",
        "--family-blocks-png",
        "--clock-style-check",
        DesktopRender.Flag,
    ];
}
#endif
