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
        WidgetBatch.Flag,
        CatalogDump.Flag,
        "--menu-png",
        "--motion-png",
        "--grid-png",
        "--drag-png",
        DragBench.Flag,
        UiBench.Flag,
        "--switch-check",
        "--startup-check",
        "--drop-check",
        DeskSelectCheck.Flag,
        ForecastEditCheck.Flag,
        DisplaysCheck.Flag,
        FirstDragCheck.Flag,
        "--family-check",
        InlineCheckRender.Flag,
        "--clock-style-check",
        DesktopRender.Flag,
        SceneRender.Flag,
        SceneRender.RecipeFlag,
        WidgetGalleryRender.Flag,
        WidgetGalleryRender.CheckFlag,
    ];
}
#endif
