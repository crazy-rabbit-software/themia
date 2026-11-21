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
/// <param name="SetBackground">[S-3703] [S-6566] The widgets' background.</param>
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
    Action<FillChoice> SetBackground,
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
    public Func<SharedRow, IReadOnlyList<SettingsLine>>? SharedLines { get; init; }
}

/// <summary>[S-3731] What the selected cards show while a value is tried: a scale or a fill not yet kept.</summary>
internal sealed record CardPreview(double? Scale = null, FillChoice? Background = null, FillChoice? Items = null);

/// <summary>
/// [S-2340]..[S-2343] [S-11754] [S-11755] The selection's page as rows, after Karpo's widget page
/// (karpo-app, docs/widgets.md §529): the header card (the kind's glyph on a tile, the widget's name over
/// its kind, Cut / Copy / Remove); then What it shows (the style first as an inline picker, what that look
/// draws under it, the widget's own lines) and Appearance (alignment, background, item background, item text, background image, scale). Several
/// selected: their count, For all of them (each setting two or more share, once) and what else they share
/// ([S-3702]), never a widget one by one ([S-11755]). [S-2345] The Appearance a tab of its own beside the rest's Settings. It is where a
/// widget's options are: there is no widget settings page ([S-6536]).
/// </summary>
internal static class SelectionRows
{
    /// <summary>[S-2376] What a mixed selection's value reads: Mixed, as Karpo's (its §528) and the shared lines say it.</summary>
    private static string Mixed => Str.Groups_Page_Mixed.Text();

    /// <summary>[S-2340] [S-2345] The page for <paramref name="model"/>, <paramref name="kinds"/> the kinds selected: its sections on their tabs.</summary>
    public static PageModel Of(SelectionModel model, SelectionCommands commands, IReadOnlyList<WidgetId> kinds)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(commands);

        var offer = model.Offer;
        var widget = offer.TitleWidgetId is { } one ? commands.WidgetLines?.Invoke(one) : null;
        var sections = new List<PageSection>();
        var shown = SelectionPage.Sections(model, widget is { Lines.Count: > 0 }, widget is { StyleLines.Count: > 0 });
        foreach (var section in shown)
        {
            var rows = section switch
            {
                SelectionSection.Shows => Shows(offer, commands, widget),
                SelectionSection.Shared => Shared(model, commands),
                _ => Appearance(offer, commands),
            };

            sections.Add(new PageSection(KeyOf(section), SelectionPage.Heading(section), rows));
        }

        // [S-2345] Two tabs or none: one tab alone is no strip.
        var tabs = SelectionPage.Tabs(shown);
        return new PageModel("selection", sections)
        {
            Header = HeaderCard(model, commands, kinds),
            Tabs = tabs.Count < 2 ? [] : [.. tabs.Select(t => new PageTab(t.Tab.ToString().ToLowerInvariant(), SelectionPage.TabTitle(t.Tab), [.. t.Sections.Select(KeyOf)]))],
        };
    }

    private static string KeyOf(SelectionSection section) => section.ToString().ToLowerInvariant();

    /// <summary>[S-2340] [S-11754] The header card: the kind, the name, its tools (their words with their keys).</summary>
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

        if (commands.Cut is not null || commands.Copy is not null || commands.Delete is not null)
        {
            tools.Add(Tool("cut", Glyphs.Cut, Verb(Str.CtxMenu_Cut, Str.Shell_Card_CutMany), KeyChord.Ctrl(ChordKey.X), commands.Cut));
            tools.Add(Tool("copy", Glyphs.Copy, Verb(Str.CtxMenu_Copy, Str.Shell_Card_CopyMany), KeyChord.Ctrl(ChordKey.C), commands.Copy));

            // [S-5524] Desktop items' widgets leave this view only: the files stay on the desktop.
            tools.Add(Tool("remove", Glyphs.Delete, offer.RemovesFromView ? Str.Desktop_Delete_FromView.Text() : Verb(Str.Common_Delete, Str.Shell_Card_DeleteMany), KeyChord.Plain(ChordKey.Delete), commands.Delete));
        }

        var kind = offer.TitleKind ?? offer.CommonKind;
        var kindName = kind is { } named ? ModuleCatalog.Of(named).Name : null;
        var name = kindName ?? Str.Shell_Views_WidgetCount.Plural(offer.Count);

        // Under the kind's name what the kind is; under several of one kind how many, under several kinds which.
        var caption = offer.Count != 1
            ? offer.CommonKind is not null ? SelectionPage.Words(offer, id => ModuleCatalog.Of(id).Name) ?? string.Empty : string.Join(", ", kinds.Select(k => ModuleCatalog.Of(k).Name))
            : kind is { } described ? ModuleCatalog.Of(described).Describe() : string.Empty;
        return new PageHeader(SelectionPage.Glyph(model), name, caption, tools);
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
                IconKey = char.ConvertFromUtf32(shown.Glyph),
                Rows =
                [
                    .. styles.Select(style => new PageRow("style/" + style.Value, SettingsRowKind.Radio)
                    {
                        Text = style.Label.Text(),
                        Icon = PageRows.IconOf(char.ConvertFromUtf32(style.Glyph), null),
                        IconKey = char.ConvertFromUtf32(style.Glyph),
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
                IconKey = at >= 0 ? align[at].Glyph : Glyphs.AlignLeft,
                Rows =
                [
                    .. align.Select(o => new PageRow("align/" + o.Value, SettingsRowKind.Radio)
                    {
                        Text = o.Name.Text(),
                        Icon = PageRows.IconOf(o.Glyph, null),
                        IconKey = o.Glyph,
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
            IconKey = Glyphs.Photo,
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

        // [S-6531] The widgets' own content scale, a number on the stored grain; [S-6532] none for desktop icons alone, drawn at 1.
        if (!offer.CanScale)
        {
            return rows;
        }

        var scale = offer.Scale;
        rows.Add(new PageRow("scale", SettingsRowKind.Number)
        {
            Text = Str.Widget_Scale_Label.Text(),
            Value = scale is { } s ? ScaleText(s) : Mixed,
            Icon = PageRows.IconOf(Glyphs.Scale, null),
            IconKey = Glyphs.Scale,
            Number = new MenuSlider(scale, WidgetScale.Min, WidgetScale.Max, WidgetScale.Step, ScaleText, commands.SetScale),
        });

        return rows;
    }

    /// <summary>What the item text's "Default" paints its swatch with: the app-wide colour, or the theme's.</summary>
    private static Windows.UI.Color? AppItemTextColour() => HexBrush.Colour(DesignApplier.Current?.ItemTextColor);

    private static Windows.UI.Color? AppBackgroundColour() => AppColour(DesignApplier.Current?.Background);

    private static Windows.UI.Color? AppItemColour() => AppColour(DesignApplier.Current?.ItemBackground);

    private static Windows.UI.Color? AppColour(FillChoice? fill)
        => fill is { Kind: FillKind.Colour } ? HexBrush.Colour(fill.Colour, FillChoice.Alpha(fill.Opacity) / 255.0) : null;

    private static string ScaleText(double scale) => LocaleFormat.Percent(Strings.Current, WidgetScale.Percent(scale));
}
