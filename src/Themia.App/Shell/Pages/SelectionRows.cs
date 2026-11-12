using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>What the selection's page does, given by the surface (or a render's stand-in), for the widgets it is about.</summary>
/// <param name="SetHeader">[S-3703] The widgets' header placement.</param>
/// <param name="SetBackground">[S-3703] [S-6566] The widgets' background.</param>
/// <param name="SetTitle">[S-3704] One widget's title, by id, as typed.</param>
/// <param name="SetStyle">[S-6008] The widgets' style, by the style enum's value.</param>
/// <param name="SetItemBackground">[S-6521] The widgets' own item background.</param>
/// <param name="SetItemTextColor">[S-6544] The widgets' own item text colour, or empty for the app-wide one.</param>
/// <param name="SetScale">[S-6531] The widgets' own content scale, as a factor.</param>
/// <param name="SetAlign">[S-6584] Where the widgets that can align draw their content.</param>
/// <param name="Cut">[S-2340] Cut the selection, as Ctrl+X does; null: no Cut on this page.</param>
/// <param name="Copy">[S-2340] Copy the selection, as Ctrl+C does.</param>
/// <param name="Delete">[S-2340] Remove the selection, as the Delete key does.</param>
/// <param name="WidgetLines">[S-3719] One widget's own lines, by id, over its live config (asking on pages of their own); null for none.</param>
/// <param name="Preview">[S-3731] Shows the widgets at a value not yet kept (a fill's opacity); null ends it.</param>
/// <param name="SetBackgroundImage">[S-6593] The widgets' background image, a picture's full path or empty to remove it.</param>
internal sealed record SelectionCommands(
    Action<HeaderPlacement> SetHeader,
    Action<FillChoice> SetBackground,
    Action<string, string?> SetTitle,
    Action<int> SetStyle,
    Action<FillChoice> SetItemBackground,
    Action<string> SetItemTextColor,
    Action<double> SetScale,
    Action<ContentAlign>? SetAlign = null,
    Action? Cut = null,
    Action? Copy = null,
    Action? Delete = null,
    Func<string, WidgetMenuLines?>? WidgetLines = null,
    Action<CardPreview?>? Preview = null,
    Action<string>? SetBackgroundImage = null)
{
    /// <summary>[S-11751] A shared setting's lines, wired to every widget that holds it (<see cref="SharedRowLines"/>); null: none.</summary>
    public Func<SharedRow, IReadOnlyList<ItemMenuEntry>>? SharedLines { get; init; }

    /// <summary>[S-11754] Rename the group, by its id, as typed; null: the name is words only.</summary>
    public Action<string, string>? RenameGroup { get; init; }

    /// <summary>[S-2454] Ungroup the group, as Ctrl+Shift+G does.</summary>
    public Action? Ungroup { get; init; }

    /// <summary>[S-11755] One member leaves its group, by id.</summary>
    public Action<string>? RemoveFromGroup { get; init; }

    /// <summary>[S-11757] An entered member's link: its whole group selected and its page shown.</summary>
    public Action? OpenGroup { get; init; }

    /// <summary>[S-11755] A member's own page: its model as a lone selection of it, and what acts on it alone.</summary>
    public Func<string, (SelectionModel Model, SelectionCommands Commands)?>? Member { get; init; }
}

/// <summary>[S-3731] What the selected cards show while a value is tried: a scale or a fill not yet kept.</summary>
internal sealed record CardPreview(double? Scale = null, FillChoice? Background = null, FillChoice? Items = null);

/// <summary>
/// [S-2340]..[S-2343] [S-11754]..[S-11757] The selection's page as rows, after Karpo's widget page
/// (karpo-app, docs/widgets.md §529): the header card (the kind's glyph on a tile, the widget's name over
/// its kind, Cut / Copy / Remove); then What it shows (the style first as an inline picker, what that look
/// draws under it, the widget's own lines), Header (the title of one widget, where the header stands) and
/// Appearance (alignment, background, item background, item text, background image, scale). Several
/// selected: their count, For all of them (each setting two or more share, once) and what else they share
/// ([S-3702]). A group: its name to rename, For all of them, its Widgets (each opening its own page), the
/// Appearance of all of them. It is where a widget's options are: there is no widget settings page ([S-6536]).
/// </summary>
internal static class SelectionRows
{
    /// <summary>What a mixed selection's value reads: a dash, as Explorer's properties show differing values.</summary>
    private const string Mixed = "—";

    /// <summary>[S-2340] The page for <paramref name="model"/>; <paramref name="kinds"/> the kinds selected; every key under <paramref name="prefix"/> (a member's page under its group's).</summary>
    public static PageModel Of(SelectionModel model, SelectionCommands commands, IReadOnlyList<WidgetId> kinds, string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(commands);

        var offer = model.Offer;
        var widget = offer.TitleWidgetId is { } one ? commands.WidgetLines?.Invoke(one) : null;
        var sections = new List<PageSection>();
        foreach (var section in SelectionPage.Sections(model, widget is { Lines.Count: > 0 }, widget is { StyleLines.Count: > 0 }))
        {
            var rows = section switch
            {
                SelectionSection.InGroup => InGroup(model, commands),
                SelectionSection.Shows => Shows(offer, commands, widget),
                SelectionSection.Shared => Shared(model, commands),
                SelectionSection.Header => Header(offer, commands),
                SelectionSection.Members => Members(model, commands, prefix),
                _ => Appearance(offer, commands),
            };

            sections.Add(new PageSection(prefix + section.ToString().ToLowerInvariant(), SelectionPage.Heading(section), Keyed(rows, prefix)));
        }

        return new PageModel(prefix + "selection", sections) { Header = HeaderCard(model, commands, kinds) };
    }

    /// <summary>Every row's key under <paramref name="prefix"/>, at every depth, so a member's page beside its group's keeps its own.</summary>
    private static List<PageRow> Keyed(List<PageRow> rows, string prefix)
        => prefix.Length == 0 ? rows : [.. rows.Select(r => Keyed(r, prefix))];

    private static PageRow Keyed(PageRow row, string prefix) => row with
    {
        Key = row.Key.StartsWith(prefix, StringComparison.Ordinal) ? row.Key : prefix + row.Key,
        Rows = row.Rows is { } inner ? [.. inner.Select(r => Keyed(r, prefix))] : null,
    };

    /// <summary>[S-2340] [S-11754] The header card: the kind (or the group), the name, its tools (their words with their keys).</summary>
    private static PageHeader HeaderCard(SelectionModel model, SelectionCommands commands, IReadOnlyList<WidgetId> kinds)
    {
        var offer = model.Offer;
        string Verb(Str one, Str many) => offer.Count > 1 ? many.Plural(offer.Count) : one.Text();
        PageTool Tool(string key, string glyphOf, string words, KeyChord chord, Action? run) => new(
            key,
            glyphOf,
            Str.Shell_Menu_LabelWithShortcut.Format(("label", words), ("shortcut", KeyChordText.Format(chord))),
            null,
            run ?? (() => { }),
            run is null);

        List<PageTool> tools = [];
        if (model.Kind == SelectionKind.Group)
        {
            // [S-11754] A group: Ungroup first, as the card menu offers it.
            tools.Add(Tool("ungroup", Glyphs.Group, Str.Groups_Card_Ungroup.Text(), KeyChord.CtrlShift(ChordKey.G), commands.Ungroup));
        }

        if (commands.Cut is not null || commands.Copy is not null || commands.Delete is not null)
        {
            tools.Add(Tool("cut", Glyphs.Cut, Verb(Str.CtxMenu_Cut, Str.Shell_Card_CutMany), KeyChord.Ctrl(ChordKey.X), commands.Cut));
            tools.Add(Tool("copy", Glyphs.Copy, Verb(Str.CtxMenu_Copy, Str.Shell_Card_CopyMany), KeyChord.Ctrl(ChordKey.C), commands.Copy));

            // [S-5524] Desktop items' widgets leave this view only: the files stay on the desktop.
            tools.Add(Tool("remove", Glyphs.Delete, offer.RemovesFromView ? Str.Desktop_Delete_FromView.Text() : Verb(Str.Common_Delete, Str.Shell_Card_DeleteMany), KeyChord.Plain(ChordKey.Delete), commands.Delete));
        }

        if (model.Kind == SelectionKind.Group)
        {
            var title = model.GroupTitle ?? string.Empty;
            var groupId = model.GroupId!;
            return new PageHeader(SelectionPage.Glyph(model), title, Str.Shell_Views_WidgetCount.Plural(offer.Count), tools)
            {
                Rename = commands.RenameGroup is { } rename
                    ? new PageEdit(Str.Groups_Page_Name.Text(), model.GroupName ?? string.Empty, typed => rename(groupId, typed)) { Title = Str.Common_Rename.Text() }
                    : null,
            };
        }

        var kind = offer.TitleKind ?? offer.CommonKind;
        var kindName = kind is { } named ? ModuleCatalog.Of(named).Name : null;
        var name = offer is { HasTitle: true } && !string.IsNullOrWhiteSpace(offer.Label) ? offer.Label! : kindName ?? Str.Shell_Views_WidgetCount.Plural(offer.Count);

        // Under a title of its own the kind; under the kind's name what the kind is; under several of one kind how many,
        // under several kinds which.
        var caption = offer.Count != 1
            ? offer.CommonKind is not null ? SelectionPage.Words(offer, id => ModuleCatalog.Of(id).Name) ?? string.Empty : string.Join(", ", kinds.Select(k => ModuleCatalog.Of(k).Name))
            : name != kindName ? kindName ?? string.Empty : kind is { } described ? ModuleCatalog.Of(described).Describe() : string.Empty;
        return new PageHeader(SelectionPage.Glyph(model), name, caption, tools);
    }

    /// <summary>[S-11757] An entered member: the link back to its group's page.</summary>
    private static List<PageRow> InGroup(SelectionModel model, SelectionCommands commands)
    {
        var title = model.GroupTitle ?? string.Empty;
        return
        [
            new PageRow("group", SettingsRowKind.Link)
            {
                Text = Str.Groups_Page_InGroup.Format(("name", title)),
                Icon = PageRows.IconOf(SelectionPage.GroupGlyph(model.GroupName), null),
                Owner = title,
                Run = commands.OpenGroup,
                Disabled = commands.OpenGroup is null,
            },
        ];
    }

    /// <summary>
    /// [S-11751] For all of them: each setting two or more of the widgets share, once: the line one of
    /// them builds for it, set on every one that holds it; Mixed while they disagree.
    /// </summary>
    private static List<PageRow> Shared(SelectionModel model, SelectionCommands commands)
    {
        var rows = new List<PageRow>();
        if (commands.SharedLines is not { } lines)
        {
            return rows;
        }

        foreach (var shared in model.Shared)
        {
            var built = lines(shared);
            if (built.Count == 0)
            {
                continue;
            }

            if (rows.Count > 0)
            {
                rows.Add(new PageRow("shared/-" + shared.Setting.Key, SettingsRowKind.Break));
            }

            rows.AddRange(PageRows.Of(built, "shared/" + shared.Setting.Key));
        }

        return rows;
    }

    /// <summary>
    /// [S-11755] A group's widgets: a row each (its glyph, its name as its header shows it, its kind under
    /// it) opening its own page as a lone selection of it shows it, Remove from group at its end.
    /// </summary>
    private static List<PageRow> Members(SelectionModel model, SelectionCommands commands, string prefix)
    {
        var rows = new List<PageRow>(model.Members.Count);
        foreach (var member in model.Members)
        {
            var descriptor = ModuleCatalog.Of(member.Kind);
            var key = "member/" + member.Id;
            var page = commands.Member?.Invoke(member.Id) is var (own, acting)
                ? Of(own, acting, [member.Kind], prefix + key + "/")
                : null;
            var id = member.Id;
            rows.Add(new PageRow(key, SettingsRowKind.SubPage)
            {
                Text = member.Name,
                // As the header card says it: under a title of its own the kind, under the kind's name what the kind is.
                Caption = member.Name == descriptor.Name ? descriptor.Describe() : descriptor.Name,
                Icon = PageRows.IconOf(descriptor.Glyph, null),
                Page = page,
                Tools =
                [
                    new PageTool("leave", Glyphs.Dismiss, Str.Groups_Card_Leave.Text(), Windows.System.VirtualKey.Delete, () => commands.RemoveFromGroup?.Invoke(id), commands.RemoveFromGroup is null),
                ],
            });
        }

        return rows;
    }

    /// <summary>
    /// [S-2342] [S-6007] What it shows: the style first, as an inline picker (only for one kind with
    /// styles), what that look draws under it ([S-3719]), then the widget's own lines in its order.
    /// </summary>
    private static List<PageRow> Shows(QuickSetOffer offer, SelectionCommands commands, WidgetMenuLines? widget)
    {
        var rows = new List<PageRow>();
        if (offer.StyleKind is { } styled && WidgetStyles.For(styled) is { Count: > 0 } styles)
        {
            var now = offer.Style;
            var current = styles.FirstOrDefault(s => s.Value == now);
            var shown = now is null || current.Glyph == 0 ? styles[0] : current;
            rows.Add(new PageRow("style", SettingsRowKind.Choice)
            {
                Text = Str.Styles_Label.Text(),
                Value = now is null ? Mixed : shown.Label.Text(),
                Icon = PageRows.IconOf(char.ConvertFromUtf32(shown.Glyph), null),
                Rows =
                [
                    .. styles.Select(style => new PageRow("style/" + style.Value, SettingsRowKind.Radio)
                    {
                        Text = style.Label.Text(),
                        Icon = PageRows.IconOf(char.ConvertFromUtf32(style.Glyph), null),
                        Holds = now == style.Value,
                        Run = () => commands.SetStyle(style.Value),
                    }),
                ],
            });
        }

        if (widget is { } own)
        {
            rows.AddRange(PageRows.Of(own.StyleLines, "style/w"));
            if (own.Lines.Count > 0)
            {
                rows.Add(new PageRow("-own", SettingsRowKind.Break));
                rows.AddRange(PageRows.Of(own.Lines, "w"));
            }
        }

        return rows;
    }

    /// <summary>
    /// [S-3704] [S-3735] Header: the one widget's title (a text box, the kind's name its hint; a title names
    /// one widget) and where the header stands, an inline picker.
    /// </summary>
    private static List<PageRow> Header(QuickSetOffer offer, SelectionCommands commands)
    {
        var rows = new List<PageRow>();
        if (offer is { TitleWidgetId: { } id, TitleKind: { } kind })
        {
            var name = ModuleCatalog.Of(kind).Name;
            rows.Add(new PageRow("header/title", SettingsRowKind.Text)
            {
                Text = Str.Group_Title_Label.Text(),
                Icon = PageRows.IconOf(Glyphs.Rename, null),
                Edit = new PageEdit(name, offer.Label ?? string.Empty, typed => commands.SetTitle(id, typed)),
            });
        }

        var options = HeaderOptions();
        var holds = Array.Find(options, o => o.Value == offer.Header);
        rows.Add(new PageRow("header", SettingsRowKind.Choice)
        {
            Text = Str.App_ConfigHeaderLabel.Text(),
            Value = offer.Header is null ? Mixed : holds.Text,
            Icon = () => Turned(holds.Text is null ? Glyphs.HeaderBottom : holds.Glyph, holds.Text is null || holds.Turned),
            Rows =
            [
                .. options.Select(o => new PageRow("header/" + o.Value, SettingsRowKind.Radio)
                {
                    Text = o.Text,
                    Icon = () => Turned(o.Glyph, o.Turned),
                    Holds = offer.Header == o.Value,
                    Run = () => commands.SetHeader(o.Value),
                }),
            ],
        });
        return rows;
    }

    /// <summary>
    /// [S-3723] Appearance: Alignment where something selected aligns ([S-6584]); Background, Item
    /// background and Item text (the item rows where something selected has items, [S-6571]); the
    /// background image ([S-6593]); Scale ([S-6531]).
    /// </summary>
    private static List<PageRow> Appearance(QuickSetOffer offer, SelectionCommands commands)
    {
        var rows = new List<PageRow>();
        if (offer.CanAlign)
        {
            (ContentAlign Value, string Glyph, Str Name)[] align =
            [
                (ContentAlign.Left, Glyphs.AlignLeft, Str.Widget_Align_Left),
                (ContentAlign.Center, Glyphs.AlignCenter, Str.Widget_Align_Center),
                (ContentAlign.Right, Glyphs.AlignRight, Str.Widget_Align_Right),
            ];
            var at = Array.FindIndex(align, o => o.Value == offer.Align);
            rows.Add(new PageRow("align", SettingsRowKind.Choice)
            {
                Text = Str.Widget_Align_Label.Text(),
                Value = at >= 0 ? align[at].Name.Text() : Mixed,
                Icon = PageRows.IconOf(at >= 0 ? align[at].Glyph : Glyphs.AlignLeft, null),
                Rows =
                [
                    .. align.Select(o => new PageRow("align/" + o.Value, SettingsRowKind.Radio)
                    {
                        Text = o.Name.Text(),
                        Icon = PageRows.IconOf(o.Glyph, null),
                        Holds = offer.Align == o.Value,
                        Run = () => commands.SetAlign?.Invoke(o.Value),
                    }),
                ],
            });
        }

        void Preview(CardPreview? preview) => commands.Preview?.Invoke(preview);
        rows.Add(new PageRow("background", SettingsRowKind.Colour)
        {
            Text = Str.Settings_Style_BgLabel.Text(),
            Colour = MenuColour.OfFill(offer.Background, commands.SetBackground,
                new FillChoiceOptions { DefaultLabel = Str.Common_Default.Text(), DefaultColor = AppBackgroundColour },
                fill => Preview(fill is null ? null : new CardPreview(Background: fill))),
        });

        if (offer.HasItems)
        {
            rows.Add(new PageRow("items", SettingsRowKind.Colour)
            {
                Text = Str.Shell_Items_Background.Text(),
                Colour = MenuColour.OfFill(offer.Items, commands.SetItemBackground,
                    new FillChoiceOptions { DefaultLabel = Str.Common_Default.Text(), DefaultColor = AppItemColour },
                    fill => Preview(fill is null ? null : new CardPreview(Items: fill))),
            });
        }

        // [S-6574] The text on items: not where every item drawn is a track, which it never reaches.
        if (offer is { HasItems: true, HasItemText: true })
        {
            rows.Add(new PageRow("itemText", SettingsRowKind.Colour)
            {
                Text = Str.Shell_Items_TextShort.Text(),
                Colour = MenuColour.Of(offer.ItemText, commands.SetItemTextColor,
                    new ColorChoiceOptions { DefaultLabel = Str.Common_Default.Text(), Presets = ColorPresets.Surfaces, DefaultColor = AppItemTextColour }),
            });
        }

        // [S-6593] The background image, under the content; Windows' picker chooses one.
        var image = offer.BackgroundImage;
        rows.Add(new PageRow("image", SettingsRowKind.Expander)
        {
            Text = Str.Shell_Fill_Image.Text(),
            Value = image is null ? Mixed : BackgroundImage.Name(image),
            Icon = PageRows.IconOf(Glyphs.Photo, null),
            Rows =
            [
                new PageRow("image/choose", SettingsRowKind.Action)
                {
                    Text = Str.Shell_Fill_ImageChoose.Text(),
                    Run = () => _ = FolderBrowse.PickFileAsync(BackgroundImage.Extensions, path => commands.SetBackgroundImage?.Invoke(path)),
                },
                new PageRow("image/remove", SettingsRowKind.Action)
                {
                    Text = Str.Shell_Fill_ImageRemove.Text(),
                    Disabled = image is { Length: 0 },
                    Run = () => commands.SetBackgroundImage?.Invoke(string.Empty),
                },
            ],
        });

        // [S-6531] The widgets' own content scale, a number on the stored grain.
        var scale = offer.Scale;
        rows.Add(new PageRow("scale", SettingsRowKind.Number)
        {
            Text = Str.Widget_Scale_Label.Text(),
            Value = scale is { } s ? ScaleText(s) : Mixed,
            Icon = PageRows.IconOf(Glyphs.Scale, null),
            Number = new MenuSlider(scale, WidgetScale.Min, WidgetScale.Max, WidgetScale.Step, ScaleText, commands.SetScale),
        });

        return rows;
    }

    private static (HeaderPlacement Value, string Glyph, bool Turned, string Text)[] HeaderOptions() =>
    [
        (HeaderPlacement.Top, Glyphs.HeaderBottom, true, Str.Group_HeaderSide_Top.Text()),
        (HeaderPlacement.Bottom, Glyphs.HeaderBottom, false, Str.Group_HeaderSide_Bottom.Text()),
        (HeaderPlacement.Left, Glyphs.HeaderLeft, false, Str.Group_HeaderSide_Left.Text()),
        (HeaderPlacement.Right, Glyphs.HeaderRight, false, Str.Group_HeaderSide_Right.Text()),
        (HeaderPlacement.Hidden, Glyphs.Hide, false, Str.Toggle_Hidden.Text()),
    ];

    /// <summary>A glyph, or the same glyph turned over (Segoe has "DockBottom" but no "DockTop").</summary>
    internal static FontIcon Turned(string glyph, bool turned)
    {
        var icon = UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize, VerticalAlignment = VerticalAlignment.Center });
        if (turned)
        {
            icon.RenderTransformOrigin = new Point(0.5, 0.5);
            icon.RenderTransform = new RotateTransform { Angle = 180 };
        }

        return icon;
    }

    /// <summary>What the item text's "Default" paints its swatch with: the app-wide colour, or the theme's.</summary>
    private static Windows.UI.Color? AppItemTextColour() => HexBrush.Colour(DesignApplier.Current?.ItemTextColor);

    private static Windows.UI.Color? AppBackgroundColour() => AppColour(DesignApplier.Current?.Background);

    private static Windows.UI.Color? AppItemColour() => AppColour(DesignApplier.Current?.ItemBackground);

    private static Windows.UI.Color? AppColour(FillChoice? fill)
        => fill is { Kind: FillKind.Colour } ? HexBrush.Colour(fill.Colour, FillChoice.Alpha(fill.Opacity) / 255.0) : null;

    private static string ScaleText(double scale) => LocaleFormat.Percent(Strings.Current, WidgetScale.Percent(scale));
}
