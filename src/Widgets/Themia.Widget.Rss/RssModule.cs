using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Rss;

[assembly: ThemiaWidget("rss", typeof(RssModule))]

namespace Themia.Widget.Rss;

/// <summary>[S-1302] Headlines from one or several feeds, merged newest first: the first widget on the kit. SPEC.md beside this file.</summary>
public sealed class RssModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("rss"),
        ConfigType = typeof(RssConfig),
        CreateDefault = () => new RssConfig(),
        Json = RssJsonContext.Default,
        Keywords = ["rss", "feed", "news", "headlines", "atom", "blog", "reader"],
        Label = RssStr.WidgetsA_Catalog_Feed_Label.Local(),
        Description = RssStr.WidgetsA_Catalog_Feed_Description.Local(),
        Glyph = Glyphs.Rss,
        DefaultWidth = 360,
        DefaultHeight = 280,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Markets, 1, 0),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => RssSettings.Rows(rows.Rows, (RssConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new RssView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RssConfig))]
internal sealed partial class RssJsonContext : JsonSerializerContext;
