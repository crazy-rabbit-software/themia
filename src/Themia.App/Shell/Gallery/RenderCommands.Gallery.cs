#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Shell;

/// <summary>[S-69] The Debug build's renders and checks: each one's <see cref="RenderCommand"/>, by its class. A new render adds its line here.</summary>
internal static partial class RenderCommands
{
    private static partial IEnumerable<RenderCommand> Gallery() =>
    [
        WidgetRender.Command,
        WidgetBatch.Command,
        CatalogDump.Command,
        CatalogDump.States,
        ShowcaseCommand.Command,
        DisplaysCheck.Command,

        // the real-host checks and the benches
        DeskSelectCheck.Command,
        ForecastEditCheck.Command,
        FirstDragCheck.Command,
        DragBench.Command,
        UiBench.Command,
        StartupCheck.Command,
        SwitchCheck.Command,
        DropCheck.Command,
        DesktopItemsCheck.Command,
        DesktopMenuCheck.Command,

        // pages
        EditBarRender.Command,
        PickerRender.Command,
        IconPickerRender.Command,
        ViewBarRender.Command,
        MenuRender.Command,
        LayoutEditorRender.Command,
        NoticeRender.Command,
        WidgetGalleryRender.Command,
        WidgetGalleryRender.Check,

        // scenes
        MotionRender.Command,
        DesktopRender.Command,
        DragRender.Command,
        GridRender.Command,
        SceneRender.Command,
        SceneRender.Recipe,
        FamilyCheckRender.Command,
        InlineCheckRender.Command,
        ClockStyleRender.Command,
    ];
}
#endif
