using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Shell;

/// <summary>
/// [S-2302] The settings' lines as a page's rows: one mapping of the one settings-row model (<see cref="SettingsLine"/>),
/// the kinds Core's (<see cref="SettingsRows.KindOf"/>), for a settings page's lines and a widget's own alike.
/// [S-2351] What a line asks (a line of text, an "are you sure?") it carries to its row, which asks it in place.
/// </summary>
internal static class PageRows
{
    private const string Scope = "pages";

    private static readonly Uri FontsPage = new("ms-settings:fonts");

    /// <summary>
    /// Where the lines come from, which says how a row is keyed and its glyph drawn: a settings page's lines carry
    /// their own paths and draw a glyph as the page's icon; a widget's lines are keyed by where they stand under
    /// their prefix (their words, a divider its place) and draw a glyph as its item menu draws it (Explorer's
    /// two-tone picture of it, a file's own icon).
    /// </summary>
    private enum Source
    {
        Settings,
        Widget,
    }

    /// <summary>
    /// [S-2301] A settings page's sections as drawn; [S-2311] its preview (the Appearance's) built by
    /// <paramref name="preview"/>, the page's host's: real cards on its display's wallpaper.
    /// </summary>
    public static PageModel Of(SettingsPage page, Func<FrameworkElement>? preview = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new PageModel(page.Id.ToString(), [.. page.Sections.Select(s => new PageSection(s.Key, s.Heading, [.. s.Lines.Select(l => Of(l, preview))]))]);
    }

    /// <summary>[S-2302] One settings line as a row; a line with a picture shows it as its icon ([S-3581]).</summary>
    public static PageRow Of(SettingsLine line, Func<FrameworkElement>? preview = null)
    {
        ArgumentNullException.ThrowIfNull(line);
        return Row(line, line.Key, Source.Settings, preview);
    }

    /// <summary>
    /// [S-2302] [S-3719] A widget's own lines (and the card's) as rows, keyed under <paramref name="prefix"/> by where they
    /// stand: a choice a choice, a submenu an expander, a search a lookup, a switch a switch, a link a link, [S-2351] a
    /// line that asks (a line of text, an "are you sure?") a card asking it in place.
    /// </summary>
    public static List<PageRow> Of(IReadOnlyList<SettingsLine> lines, string prefix)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return Children(lines, prefix, Source.Widget, null);
    }

    /// <summary>The rows of <paramref name="lines"/> under the row keyed <paramref name="parent"/>.</summary>
    private static List<PageRow> Children(IReadOnlyList<SettingsLine> lines, string parent, Source source, Func<FrameworkElement>? preview)
    {
        var rows = new List<PageRow>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var key = source == Source.Settings ? line.Key : parent + "/" + (line is SettingsSeparator ? "-" + i : line.Key);
            rows.Add(Row(line, key, source, preview));
        }

        return rows;
    }

    /// <summary>
    /// [S-2302] One line as its row keyed <paramref name="key"/>; [S-2317] its icon its own where it is a colour's, a
    /// picture's, a file's or the icon it chose; [S-2375] greyed, it says why.
    /// </summary>
    private static PageRow Row(SettingsLine line, string key, Source source, Func<FrameworkElement>? preview)
    {
        var row = RowOf(line, key, source, preview) with
        {
            Disabled = line.Disabled,
            Marked = line.Marked || line.GlyphColour is not null || line.Picture is not null || line.IconFile is not null,
            SetsUp = line.SetsUp,
        };
        row = Greyed(row, line.Disabled, line.Why);
        row = line.Picture is { } picture ? row with { Icon = () => MenuPictures.Create(picture) } : row;

        // [S-2361] What its icon, its head's picture and its picker draw, as keys: a row drawn the same keeps its card.
        return row with
        {
            IconKey = row.Icon is null ? null : string.Join('|', source, GlyphOf(line), line.GlyphColour, line.Disabled, line.IconFile, line.Picture),
            ShowsKey = row.Shows is null || line is not SettingsIcon shown ? null : string.Join('|', shown.Head.Glyph, line.GlyphColour),
            PickerKey = line switch
            {
                SettingsFont font => "font|" + font.Current,
                SettingsIcon icon => string.Join('|', "icon", icon.Current, icon.DefaultName, icon.DefaultText),
                _ => null,
            },
        };
    }

    /// <summary>The glyph a line's icon is drawn from; null for none.</summary>
    private static string? GlyphOf(SettingsLine line) => line switch
    {
        SettingsHeading l => l.Glyph,
        SettingsReading l => l.Glyph,
        SettingsToggle l => l.Glyph,
        SettingsRadio l => l.Glyph,
        SettingsChoice l => l.Glyph,
        SettingsSlider l => l.Glyph,
        SettingsFont l => l.Glyph,
        SettingsIcon l => l.Glyph,
        SettingsSubmenu l => l.Glyph,
        SettingsCommand l => l.Glyph,
        SettingsAsk l => l.Glyph,
        SettingsPrompt l => l.Glyph,
        SettingsLink l => l.Glyph,
        SettingsSearch l => l.Glyph,
        SettingsItem l => l.Glyph,
        _ => null,
    };

    /// <summary>
    /// [S-2375] A greyed row says why under its name (Karpo's §309, <c>OptionRow.Why</c>): its caption while it is greyed,
    /// in place of what it would say there otherwise.
    /// </summary>
    private static PageRow Greyed(PageRow row, bool disabled, string? why)
        => disabled && !string.IsNullOrWhiteSpace(why) ? row with { Caption = why } : row;

    private static PageRow RowOf(SettingsLine line, string key, Source source, Func<FrameworkElement>? preview)
    {
        var kind = SettingsRows.KindOf(line);
        var row = new PageRow(key, kind);
        Func<UIElement?>? Icon(string? glyph, string? colour = null) => source == Source.Widget
            ? glyph is null && line.IconFile is null ? null : () => ItemMenu.IconFor(glyph, line.Disabled, line.IconFile)
            : IconOf(glyph, colour);
        List<PageRow> Under(IReadOnlyList<SettingsLine> lines, string at) => Children(lines, at, source, preview);

        return line switch
        {
            SettingsSeparator => row,
            SettingsHeading heading => row with { Text = heading.Text, Icon = IconOf(heading.Glyph, null) },
            SettingsNote note => row with { Text = note.Text, Danger = note.Critical, Busy = note.Busy },
            SettingsReading reading => row with
            {
                Text = reading.Text,
                Value = reading.Value,
                Caption = reading.Caption,
                Icon = Icon(reading.Glyph),
                Beside = reading.Beside is { } beside ? Of(beside) : null,
            },

            // [S-3556] A form's field, every letter handed to the form as it is typed; a password's masked.
            SettingsField field => row with
            {
                Text = field.Text,
                Edit = new PageEdit(field.Placeholder, field.Masked ? string.Empty : field.Value ?? string.Empty, _ => { })
                {
                    Live = field.Set,
                    Masked = field.Masked,
                    Fill = field.Masked ? field.Fill : null,
                    Wipe = field.Masked ? field.Wipe : null,
                    Numeric = field.Numeric,
                },
            },
            SettingsToggle toggle => row with
            {
                Text = toggle.Text,
                Caption = toggle.Value,
                Icon = Icon(toggle.Glyph, toggle.GlyphColour),
                On = toggle.On,
                Toggle = toggle.Set,
            },
            SettingsRadio radio => row with { Text = radio.Text, Icon = Icon(radio.Glyph, radio.GlyphColour), Holds = radio.Holds, Run = radio.Pick, Keywords = radio.Keywords },
            SettingsChoice choice => row with
            {
                Text = choice.Text,
                Value = choice.Value,
                Icon = Icon(choice.Glyph),

                // [S-3636] What its picker holds after its options (Custom…) follows them.
                Rows = Under([.. choice.Options, .. choice.More], key),
                Filter = choice.Filter,
            },
            SettingsSlider slider => row with
            {
                Text = slider.Text,
                Value = slider.ValueText ?? slider.Format(slider.Value),
                Icon = Icon(slider.Glyph),
                Number = new MenuSlider(slider.Value, slider.Min, slider.Max, slider.Step, slider.Format, slider.Set),
            },
            SettingsColour colour => row with
            {
                Text = colour.Text,
                Colour = MenuColour.Of(colour.Current, colour.Set, new ColorChoiceOptions
                {
                    DefaultLabel = colour.DefaultText,
                    Presets = colour.Presets,
                    DefaultColor = colour.DefaultIsWindowsAccent ? ColorChoice.WindowsAccent : colour.DefaultHex is { } hex ? () => HexColour(hex) : null,
                }),
            },
            SettingsFill fill => row with { Text = fill.Text, Colour = MenuColour.OfFill(fill.Current, fill.Set, new FillChoiceOptions { DefaultLabel = fill.DefaultText }) },
            SettingsFont font => row with
            {
                Text = font.Text,
                Value = font.Value,
                Icon = Icon(font.Glyph),
                Picker = () => FontPickerFor(font.Current, family =>
                {
                    if (!string.Equals(family, font.Current, StringComparison.Ordinal))
                    {
                        font.Set(family);
                    }
                }),
                Rows =
                [
                    new PageRow(font.Key + "/add", SettingsRowKind.Action)
                    {
                        Text = Str.Shell_Font_Add.Text(),
                        Icon = IconOf(Glyphs.Add, null),
                        IconKey = Glyphs.Add,
                        Run = () => _ = ShellLauncher.OpenWindowsSettings(FontsPage),
                    },
                ],
            },
            SettingsIcon icon => row with
            {
                Text = icon.Text,
                Icon = Icon(icon.Glyph, icon.GlyphColour),

                // [S-14322] Folded, its head shows what it holds: the chosen icon, or the default's words.
                Shows = IconOf(icon.Head.Glyph, icon.GlyphColour),
                Value = icon.Head.Words,
                Picker = () => new IconPicker(icon.Current, icon.DefaultName, icon.DefaultText, glyph =>
                {
                    if (!string.Equals(glyph, icon.Current, StringComparison.Ordinal))
                    {
                        icon.Set(glyph);
                    }
                }),
            },
            SettingsSubmenu submenu => row with
            {
                Text = submenu.Text,
                Value = submenu.Value,
                Icon = Icon(submenu.Glyph, submenu.GlyphColour),
                Rows = Under(submenu.Lines, key),
                Run = submenu.Run,

                // [S-3732] A widget's list item does not carry its place to its row (as before the one row model): its
                // line holds it, its card draws no grip yet.
                Order = source == Source.Settings ? submenu.Order : null,

                // [S-2310] A thing's card: its line under its name, its icon buttons at its head's end.
                Caption = submenu.Caption,
                Tools = submenu.Tools.Count > 0 ? [.. submenu.Tools.Select(ToolOf)] : null,
            },
            SettingsCommand command => row with
            {
                Text = command.Text,
                Value = command.Value,
                Caption = command.Caption,
                Icon = Icon(command.Glyph, command.GlyphColour),
                Danger = command.Danger,
                Run = command.Run,

                // [S-2351] Asked first, in place in its card (its kind Ask, Core's).
                Question = command.Confirm is { } confirm ? QuestionOf(confirm) : null,
            },

            // [S-2302] A line of text kept in its box: Enter or leaving keeps it, Escape restores it.
            SettingsAsk text => row with
            {
                Text = text.Text,
                Icon = Icon(text.Glyph),
                Edit = new PageEdit(text.Placeholder, text.Initial, text.Done) { Refuse = text.Refuse, AllowEmpty = text.AllowEmpty },
            },

            // [S-2351] A line of text asked in place: its card's head (what it holds, the expand chevron) and, opened, the
            // field and its accept. Never a page of its own.
            SettingsPrompt prompt => row with
            {
                Text = prompt.Text,
                Value = prompt.Value,
                Icon = Icon(prompt.Glyph),
                Danger = prompt.Danger,
                Edit = new PageEdit(prompt.Ask.Placeholder, prompt.Ask.Initial ?? string.Empty, prompt.Ask.Done)
                {
                    Refuse = prompt.Ask.Refuse,
                    AllowEmpty = prompt.Ask.AllowEmpty,
                    Accept = prompt.Ask.Accept,
                },
            },

            // [S-3749] Somewhere the user must go to do something: the open mark at its end, no chevron.
            SettingsLink link => row with { Text = link.Text, Icon = Icon(link.Glyph), Owner = link.Owner, Run = link.Run },

            // [S-3722] A lookup that asks: its box, its answers, the lines that always follow them.
            SettingsSearch search => row with
            {
                Text = search.Text,
                Value = search.Value,
                Icon = Icon(search.Glyph),
                Search = search,
                Rows = Under(search.Fixed, key + "/fixed"),
            },

            // [S-2311] The Appearance's preview, the page host's; none where the page was built without one (a test's).
            // [S-13700] The Unplaced icons page's list is its page's own live part the same way.
            SettingsPreview live => row with { Text = live.Name ?? Str.SettingsPages_Appearance_Preview.Text(), Picker = preview },
            SettingsItem item => row with
            {
                Text = item.Text,
                Icon = Icon(item.Glyph, item.GlyphColour),
                Order = item.Order,
                Tools = [.. item.Tools.Select(ToolOf)],
            },
            _ => throw new ArgumentOutOfRangeException(nameof(line), line.GetType().Name, "a settings line no page draws"),
        };
    }

    /// <summary>[S-566] A colour of its own for Default's swatch (<c>#RRGGBB</c>); null where it does not read as one.</summary>
    private static Windows.UI.Color? HexColour(string hex)
        => HexColor.TryParse(hex, out var r, out var g, out var b) ? HexBrush.Colour(new RgbColor(r, g, b)) : null;

    /// <summary>[S-2351] [S-3727] An "are you sure?" as its row or tool asks it in place.</summary>
    internal static PageQuestion QuestionOf(SettingsConfirm confirm)
    {
        ArgumentNullException.ThrowIfNull(confirm);
        return new PageQuestion(confirm.Question, confirm.Accept, confirm.Done, confirm.Danger, confirm.Detail);
    }

    /// <summary>[S-3549] A tool: a command (Duplicate), or [S-2351] a question asked first in place in its card (Delete, Sign out).</summary>
    private static PageTool ToolOf(SettingsTool tool) => new(
        tool.Key,
        tool.Glyph,
        tool.Name,
        tool.Shortcut switch
        {
            SettingsToolKey.Rename => Windows.System.VirtualKey.F2,
            SettingsToolKey.Delete => Windows.System.VirtualKey.Delete,
            _ => null,
        },
        tool.Run ?? (() => { }),
        tool.Disabled,
        tool.Confirm is { } confirm ? QuestionOf(confirm) : null);

    /// <summary>A glyph's icon, in a colour of its own where it has one (a view's mark, [S-3810]); null for none.</summary>
    internal static Func<UIElement?>? IconOf(string? glyph, string? colour)
        => glyph is null ? null : colour is null ? () => UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize }) : () => ViewMark.Icon(glyph, colour);

    /// <summary>
    /// [S-827] [S-6443] The font picker over the presets and the installed families: until the list has
    /// been read (off the UI thread, once) it lists the presets and the current family; it follows the
    /// list as it comes, and again while it is up whenever Windows says the installed fonts changed.
    /// </summary>
    private static FontPicker FontPickerFor(string current, Action<string> choose)
    {
        var presets = SettingsMenu.FontPresets();
        var installed = InstalledFonts.None;
        IReadOnlyList<FontOption> Options() => FontSearch.Options(presets, installed, current);

        var picker = new FontPicker(Options(), FontSearch.Chosen(Options(), current), choose);
        var queue = DispatcherQueue.GetForCurrentThread();

        void Apply(InstalledFonts families)
        {
            installed = families;
            picker.SetOptions(Options());
        }

        void Load()
        {
            var loading = SystemFonts.LoadAsync();
            if (loading.IsCompletedSuccessfully)
            {
                Apply(loading.Result);
                return;
            }

            _ = loading.ContinueWith(
                task =>
                {
                    if (!task.IsCompletedSuccessfully)
                    {
                        Log.Warn(Scope, "the installed font list could not be read; only the presets are offered", task.Exception);
                        return;
                    }

                    if (queue is null || !queue.TryEnqueue(() => Apply(task.Result)))
                    {
                        Log.Warn(Scope, "could not list the installed fonts on the page's thread");
                    }
                },
                TaskScheduler.Default);
        }

        picker.Loaded += (_, _) =>
        {
            SystemFonts.Changed += Load;
            Load();
        };
        picker.Unloaded += (_, _) => SystemFonts.Changed -= Load;
        return picker;
    }
}
