using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Gallery;

[assembly: ThemiaWidget("gallery", typeof(GalleryModule))]

namespace Themia.Widget.Gallery;

/// <summary>[S-760] Pictures from a folder, one file or Bing's photo of the day: a slideshow, a grid, a scatter of prints or a mosaic, or a sticker. SPEC.md beside this file.</summary>
public sealed class GalleryModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (U+E91B).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE91B);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("gallery"),
        ConfigType = typeof(GalleryConfig),
        CreateDefault = () => new GalleryConfig(),
        Json = GalleryJsonContext.Default,
        Keywords = ["photos", "pictures", "images", "slideshow", "wallpaper", "bing"],
        Label = GalleryStr.Widget_Gallery_Label.Local(),
        Description = GalleryStr.WidgetsA_Catalog_Gallery_Description.Local(),
        Glyph = Glyph,
        DefaultWidth = 320,
        DefaultHeight = 240,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Media, 1, 0),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => GallerySettings.Rows(rows.Rows, (GalleryConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new GalleryView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GalleryConfig))]
internal sealed partial class GalleryJsonContext : JsonSerializerContext;
