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
using Themia.Widget.Scratchpad;

[assembly: ThemiaWidget("scratchpad", typeof(ScratchpadModule))]

namespace Themia.Widget.Scratchpad;

/// <summary>[S-1497] The scratchpad: a pad to draw on with the original's inks and nibs, an eraser and full screen. SPEC.md beside this file.</summary>
public sealed class ScratchpadModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (U+E70F).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE70F);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("scratchpad"),
        ConfigType = typeof(ScratchpadConfig),
        CreateDefault = () => new ScratchpadConfig(),
        Json = ScratchpadJsonContext.Default,
        Keywords = ["draw", "sketch", "drawing", "canvas", "whiteboard", "doodle", "pen"],
        Label = ScratchpadStr.Widget_Scratchpad_Label.Local(),
        Description = ScratchpadStr.WidgetsA_Catalog_Scratchpad_Description.Local(),
        Glyph = Glyph,
        DefaultWidth = 300,
        DefaultHeight = 260,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Notes, 0, 1),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => ScratchpadSettings.Rows(rows.Rows, (ScratchpadConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new ScratchpadView();
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScratchpadConfig))]
internal sealed partial class ScratchpadJsonContext : JsonSerializerContext;
