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
using Themia.Widget.Clipboard;

[assembly: ThemiaWidget("clipboard", typeof(ClipboardModule))]

namespace Themia.Widget.Clipboard;

/// <summary>[S-1440] What has been copied recently, most recent first, text only and in memory for the session; a click puts a row back on the clipboard. SPEC.md beside this file.</summary>
public sealed class ClipboardModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (U+E77F).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE77F);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("clipboard"),
        ConfigType = typeof(ClipboardConfig),
        CreateDefault = () => new ClipboardConfig(),
        Json = ClipboardJsonContext.Default,
        Keywords = ["copy", "paste", "history", "snippets"],
        Label = ClipboardStr.WidgetsA_Catalog_Clipboard_Label.Local(),
        Description = ClipboardStr.WidgetsA_Catalog_Clipboard_Description.Local(),
        Glyph = Glyph,
        DefaultWidth = 320,
        DefaultHeight = 260,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Notes, 0, 2),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => ClipboardSettings.Rows(rows.Rows, (ClipboardConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new ClipboardView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ClipboardConfig))]
internal sealed partial class ClipboardJsonContext : JsonSerializerContext;
