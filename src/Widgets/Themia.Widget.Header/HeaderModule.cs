using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Header;

[assembly: ThemiaWidget("header", typeof(HeaderModule))]

namespace Themia.Widget.Header;

/// <summary>
/// [S-9600] The Header widget: a title of the person's own over the widgets under it (no card has a header of
/// its own, [S-12480]). Its behaviour is SPEC.md beside this file.
/// </summary>
public sealed class HeaderModule : IWidgetModule
{
    /// <summary>Its glyph (Segoe Fluent Icons U+E8D2, Text), the module's own.</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE8D2);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("header"),
        ConfigType = typeof(HeaderConfig),
        CreateDefault = () => new HeaderConfig(),
        Json = HeaderJsonContext.Default,
        Label = HeaderStr.Header_Label.Local(),
        Description = HeaderStr.Header_Desc.Local(),
        Glyph = Glyph,
        Keywords = ["title", "heading", "label", "caption", "group title"],
        Sizing = config => HeaderLayout.Sizing(config as HeaderConfig ?? new HeaderConfig()),
        Traits = WidgetTraits.None,
        // [S-9603] Among the notes' words, a family of its own after them: a title is text the person writes.
        Add = new(AddFolderId.Notes, 2, 0),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new HeaderView();

    /// <summary>
    /// [S-9602] Its lines: the title and the caption (each asked beside the page), and the icon, the one icon picker
    /// inline; [S-9604] then Show the place and the place's lines every widget about a place shares (a group sets it once).
    /// </summary>
    public void MenuLines(WidgetMenuLines menu, Themia.Core.Model.WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        if (config is not HeaderConfig header)
        {
            return;
        }

        menu.Lines.Add(Ask(menu, Glyphs.Rename, HeaderStr.Header_Title.Text(), header.Text, typed => header.Text = typed));
        menu.Lines.Add(menu.Icon(HeaderStr.Header_Icon.Text(), header.Glyph, HeaderStr.Header_NoIcon.Text(), glyph => header.Glyph = glyph ?? string.Empty));
        menu.Lines.Add(Ask(menu, Glyphs.Font, HeaderStr.Header_Caption.Text(), header.Caption, typed => header.Caption = typed));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(menu.Toggle(Glyphs.Place, HeaderStr.Header_ShowsPlace.Text(), header.ShowsPlace, on => header.ShowsPlace = on));
        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, header));
    }

    /// <summary>A line of text asked beside the page; an empty one clears it.</summary>
    private static ItemMenuEntry Ask(WidgetMenuLines menu, string glyph, string text, string current, Action<string> set)
        => new(glyph, text, () => menu.Ask(new WidgetTextAsk(
            text,
            text,
            Str.Common_Save.Text(),
            _ => null,
            typed =>
            {
                var clean = HeaderLayout.Clean(typed);
                if (!string.Equals(clean, current, StringComparison.Ordinal))
                {
                    set(clean);
                    menu.Save();
                }
            },
            Initial: current,
            AllowEmpty: true)), Asks: true, Value: current);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HeaderConfig))]
internal sealed partial class HeaderJsonContext : JsonSerializerContext;
