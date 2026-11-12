using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.DesktopItem;

[assembly: ThemiaWidget("desktopItem", typeof(DesktopItemModule))]

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-5600] The desktop item widget: one file, folder, shortcut or shell item of the desktop, drawn as
/// Explorer's desktop draws an icon. The host creates, rewrites and removes these as the desktop
/// changes ([S-5520]); Add widget offers the items a view lacks, never the kind alone ([S-5523]).
/// Its behaviour is SPEC.md beside this file.
/// </summary>
public sealed class DesktopItemModule : IWidgetModule
{
    /// <summary>[S-5601] One desktop cell, in DIPs (on the 10-DIP snap grid).</summary>
    public const double CellWidth = 90;

    /// <summary>Its glyph (Segoe Fluent Icons U+E8A5, a document), the module's own.</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE8A5);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("desktopItem"),
        ConfigType = typeof(DesktopItemConfig),
        CreateDefault = () => new DesktopItemConfig(),
        Json = DesktopItemJsonContext.Default,
        Label = DesktopItemStr.DesktopItem_Label.Local(),
        Description = DesktopItemStr.DesktopItem_Desc.Local(),
        Glyph = Glyph,
        Sizing = c => WidgetSizing.Fixed(DesktopItemLayout.Size(((DesktopItemConfig)c).LabelLines)),

        // [S-5600] The name is under the icon: no header, no item layer.
        Traits = WidgetTraits.None,

        // [S-5523] Its row in Files becomes "On the desktop" (the view's unplaced items).
        Add = new(AddFolderId.Files, 99, 0),
        Keywords = ["desktop", "icon", "file", "shortcut"],
    };

    public FrameworkElement CreateView(IWidgetHost host) => new DesktopItemView(host);

    /// <summary>[S-3719] [S-11204] What an item is follows from the desktop; its one line is how many lines its name has.</summary>
    public void MenuLines(WidgetMenuLines menu, Themia.Core.Model.WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var item = (DesktopItemConfig)config;
        menu.Lines.Add(menu.Choice(
            null,
            DesktopItemStr.DesktopItem_NameLines.Text(),
            [(1, Str.Kit_LinesOne.Text()), (2, Str.Kit_LinesTwo.Text())],
            DesktopItemLayout.ClampLines(item.LabelLines),
            v => item.LabelLines = v));
    }
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DesktopItemConfig))]
internal sealed partial class DesktopItemJsonContext : JsonSerializerContext;
