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
    ];
}
#endif
