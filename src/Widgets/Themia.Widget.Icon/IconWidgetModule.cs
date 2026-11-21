using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Icon;

[assembly: ThemiaWidget("icon", typeof(IconWidgetModule))]

namespace Themia.Widget.Icon;

/// <summary>
/// [S-15010] The Icon widget (owner, 2026-11-21: "you should also create a new widget that just shows a selectable icon"):
/// one icon the person picks, an icon kind on the icon base ([S-15003]): its glyph alone filling its card. At home under
/// Notes &amp; tools beside the Header widget: a decoration the person sets, as a title is. SPEC.md beside this file.
/// </summary>
public sealed class IconWidgetModule() : IconModule(IconKindOf, IconJsonContext.Default, AddFolderId.Notes, 1, group: 2)
{
    /// <summary>[S-15011] The icon a new one shows, and the one a cleared pick shows: the star.</summary>
    internal static readonly string Default = Glyphs.Important;

    /// <summary>[S-15010] The kind as data: it opens nothing, so it lights nothing ([S-15005]).</summary>
    internal static IconKind IconKindOf { get; } = new("icon", Glyphs.Important, IconStr.Icon_Label.Local(), IconStr.Icon_Desc.Local(),
        ["icon", "symbol", "glyph", "emoji", "decoration", "sign", "picture", "star"], () => new IconWidgetConfig());

    /// <summary>
    /// [S-15012] Its one line: which icon, the one icon picker inline in its card ([S-1803], [S-9602]), its folded head
    /// showing the icon chosen ([S-14322]); the picker's first cell is Default (the star).
    /// </summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config)
    {
        if (config is not IconWidgetConfig icon)
        {
            return;
        }

        menu.Lines.Add(menu.Icon(IconStr.Icon_Label.Text(), icon.Glyph, Str.Common_Default.Text(), glyph => icon.Glyph = glyph ?? string.Empty));
    }

    public override FrameworkElement CreateView(IWidgetHost host) => new IconWidgetView(host, Kind);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(IconWidgetConfig))]
internal sealed partial class IconJsonContext : JsonSerializerContext;
