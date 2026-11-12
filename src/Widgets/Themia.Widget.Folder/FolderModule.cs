using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Folder;

[assembly: ThemiaWidget("folder", typeof(FolderModule))]

namespace Themia.Widget.Folder;

/// <summary>[S-1320] A folder on this PC as a small Windows 11 file browser: list, details, grid or icons, with Explorer's selection, keys, menus, file operations, clipboard and drag and drop. SPEC.md beside this file.</summary>
public sealed class FolderModule : IWidgetModule
{
    /// <summary>[S-2031] A click on the card opens the folder it shows, in File Explorer; nothing with no folder.</summary>
    internal static CardOpen? Opens(string? shown) => CardOpen.Place(shown);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("folder"),
        ConfigType = typeof(FolderConfig),
        CreateDefault = () => new FolderConfig(),
        Json = FolderJsonContext.Default,
        // [S-4123] The four former folder widgets' names and words find the one folder.
        Keywords =
        [
            "files", "explorer", "directory", "file browser", "documents", "downloads", "list", "folder list",
            "folder details", "table", "columns", "folder grid", "pictures", "photos", "thumbnails", "tiles",
            "folder icons", "desktop icons", "launcher",
        ],
        Label = FolderStr.Styles_Folder_Label.Local(),
        Description = FolderStr.Styles_Folder_Desc.Local(),
        Glyph = Glyphs.Folder,
        Sizing = c => FolderLayout.Sizing((FolderConfig)c),
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Links = LinkUse.Of([LinkTopicId.HoveredFile], [LinkTopicId.HoveredFile]),
        Add = new(AddFolderId.Files, 0, 0, Popular: 9),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => FolderPropertiesMenu.Lines(menu, (FolderConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new FolderView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FolderConfig))]
internal sealed partial class FolderJsonContext : JsonSerializerContext;
