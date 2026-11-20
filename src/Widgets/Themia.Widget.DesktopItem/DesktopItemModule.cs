using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.DesktopItem;
using Themia.App.Shell;

[assembly: ThemiaWidget("desktopItem", typeof(DesktopItemModule))]

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-5600] [S-9500] The desktop icon, the one kind for everything drawn and used as Explorer's desktop
/// draws an icon. A widget of it is one of two sources (<see cref="DesktopItemConfig"/>): a desktop item
/// (a file, folder, link or shell item of the desktop, which the host creates, rewrites and removes as
/// the desktop changes, [S-5520]) or an own icon (a file, folder, app, web address, Windows location,
/// Settings page or app layout the user points it at). Add widget's tile makes an own icon; the desktop's
/// items a view lacks are listed beside it ([S-5523]). Its behaviour is SPEC.md beside this file.
/// </summary>
public sealed class DesktopItemModule : IWidgetModule
{
    /// <summary>Its glyph (Segoe Fluent Icons U+E71B, a link), the module's own.</summary>
    internal static readonly string Glyph = Glyphs.Link;

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("desktopItem"),
        ConfigType = typeof(DesktopItemConfig),
        CreateDefault = () => new DesktopItemConfig(),
        Json = DesktopItemJsonContext.Default,
        Keywords = ["link", "launcher", "bookmark", "url", "open", "pin", "icon", "app", "settings", "layout", "desktop", "file"],
        // [S-5611] The kind is a desktop icon, of either source; only its tile on the add page, which makes
        // an own icon, is called Shortcut.
        Label = DesktopItemStr.DesktopItem_Label.Local(),
        AddLabel = DesktopItemStr.Shortcut_Label.Local(),
        Description = DesktopItemStr.Shortcut_Desc.Local(),
        Glyph = Glyph,

        // [S-11204] [S-5593] One fixed cell per its name lines, the desktop's own at the icon size in force.
        // [S-12411] A desktop icon's box is the desktop's cell, never the content's to stretch.
        Sizing = c => WidgetSizing.Fixed(DesktopCell.Size(DesktopCell.Current, ((DesktopItemConfig)c).LabelLines)) with { Room = StretchRoom.None },

        // [S-5600] The name is under the icon: no header, no item layer, and a new one on no card.
        // [S-11880] An icon is no card: it never joins the icons it touches on the grid.
        Traits = WidgetTraits.StartsTransparent | WidgetTraits.NeverJoins,
        Links = LinkUse.Of([LinkTopicId.HoveredApp, LinkTopicId.HoveredFile], [LinkTopicId.HoveredApp, LinkTopicId.SelectedApp, LinkTopicId.HoveredFile]),

        // [S-5523] Its tile in Files makes an own icon; the view's unplaced desktop items are listed in "On the desktop".
        Add = new(AddFolderId.Files, 0, 1, Popular: 10),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new DesktopItemView(host);

    /// <summary>
    /// [S-3719] [S-9509] [S-11204] A desktop item is what the desktop says it is: its one line is how many
    /// lines its name has. An own icon has every option on the page: what it opens, its name, its icon,
    /// its name lines and how it opens.
    /// </summary>
    public void MenuLines(WidgetMenuLines menu, Themia.Core.Model.WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var icon = (DesktopItemConfig)config;
        if (!icon.IsDesktopItem)
        {
            OwnIconMenu.Lines(menu, icon, SettingsHost.AppLayouts);
            return;
        }

        menu.Lines.Add(NameLines(menu, icon));
    }

    /// <summary>[S-11204] The Name lines choice, the one line both sources share.</summary>
    internal static ItemMenuEntry NameLines(WidgetMenuLines menu, DesktopItemConfig config) => menu.Choice(
        null,
        DesktopItemStr.DesktopItem_NameLines.Text(),
        [(1, Str.Kit_LinesOne.Text()), (2, Str.Kit_LinesTwo.Text())],
        DesktopCell.ClampLines(config.LabelLines),
        v => config.LabelLines = v);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DesktopItemConfig))]
internal sealed partial class DesktopItemJsonContext : JsonSerializerContext;
