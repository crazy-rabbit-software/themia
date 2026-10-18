using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;
using Themia.Widget.Shortcuts;

[assembly: ThemiaWidget("shortcuts", typeof(ShortcutsModule))]

namespace Themia.Widget.Shortcuts;

/// <summary>[S-1380] Links to files, folders, apps, web addresses, Windows locations and app layouts: tiles, a list or a table. SPEC.md beside this file.</summary>
public sealed class ShortcutsModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (U+E71B).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE71B);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("shortcuts"),
        ConfigType = typeof(ShortcutsConfig),
        CreateDefault = ShortcutsConfig.CreateDefault,
        Json = ShortcutsJsonContext.Default,
        Keywords = ["link", "launcher", "bookmark", "url", "open", "pin", "quick access"],
        Label = ShortcutsStr.WidgetsA_Catalog_Shortcuts_Label.Local(),
        Description = ShortcutsStr.WidgetsA_Catalog_Shortcuts_Description.Local(),
        Glyph = Glyph,
        DefaultWidth = 320,
        DefaultHeight = 220,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Files, 0, 1, Popular: 10),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => ShortcutsSettings.Rows(rows.Rows, (ShortcutsConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new ShortcutsView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ShortcutsConfig))]
internal sealed partial class ShortcutsJsonContext : JsonSerializerContext;
