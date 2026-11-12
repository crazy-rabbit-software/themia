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
/// [S-2302] The settings' lines and the selection's entries as a page's rows: one mapping each, the kinds
/// Core's. What a line asks on a page of its own (a tool's Rename) goes to <c>ask</c>.
/// </summary>
internal static class PageRows
{
    private const string Scope = "pages";

    private static readonly Uri FontsPage = new("ms-settings:fonts");

    /// <summary>[S-2301] A settings page's sections as drawn.</summary>
    public static PageModel Of(SettingsPage page, Action<PageEdit> ask)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new PageModel(page.Id.ToString(), [.. page.Sections.Select(s => new PageSection(s.Key, s.Heading, [.. s.Lines.Select(l => Of(l, ask))]))]);
    }

    /// <summary>[S-2302] One settings line as a row; a line with a picture shows it as its icon ([S-3581]).</summary>
    public static PageRow Of(SettingsLine line, Action<PageEdit> ask)
    {
        ArgumentNullException.ThrowIfNull(line);
        var row = RowOf(line, ask) with { Disabled = line.Disabled };
        return line.Picture is { } picture ? row with { Icon = () => MenuPictures.Create(picture) } : row;
    }

    private static PageRow RowOf(SettingsLine line, Action<PageEdit> ask)
    {
        var kind = SettingsRows.KindOf(line);
        var row = new PageRow(line.Key, kind);
        return line switch
        {
            SettingsSeparator or SettingsHeading => row with { Text = (line as SettingsHeading)?.Text ?? string.Empty },
            SettingsNote note => row with { Text = note.Text, Danger = note.Critical, Busy = note.Busy },
            SettingsReading reading => row with { Text = reading.Text, Value = reading.Value, Icon = IconOf(reading.Glyph, null) },

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
                Icon = IconOf(toggle.Glyph, toggle.GlyphColour),
                On = toggle.On,
                Toggle = toggle.Set,
            },
            SettingsRadio radio => row with { Text = radio.Text, Icon = IconOf(radio.Glyph, radio.GlyphColour), Holds = radio.Holds, Run = radio.Pick, Keywords = radio.Keywords },
            SettingsChoice choice => row with
            {
                Text = choice.Text,
                Value = choice.Value,
                Icon = IconOf(choice.Glyph, null),
                Rows = [.. choice.Options.Select(o => Of(o, ask))],
                Filter = choice.Filter,
            },
            SettingsSlider slider => row with
            {
                Text = slider.Text,
                Value = slider.Format(slider.Value),
                Icon = IconOf(slider.Glyph, null),
                Number = new MenuSlider(slider.Value, slider.Min, slider.Max, slider.Step, slider.Format, slider.Set),
            },
            SettingsColour colour => row with
            {
                Text = colour.Text,
                Colour = MenuColour.Of(colour.Current, colour.Set, new ColorChoiceOptions
                {
                    DefaultLabel = colour.DefaultText,
                    Presets = colour.Presets,
                    DefaultColor = colour.DefaultIsWindowsAccent ? ColorChoice.WindowsAccent : null,
                }),
            },
            SettingsFill fill => row with { Text = fill.Text, Colour = MenuColour.OfFill(fill.Current, fill.Set, new FillChoiceOptions { DefaultLabel = fill.DefaultText }) },
            SettingsFont font => row with
            {
                Text = font.Text,
                Value = font.Value,
                Icon = IconOf(font.Glyph, null),
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
                        Run = () => _ = ShellLauncher.OpenWindowsSettings(FontsPage),
                    },
                ],
            },
            SettingsIcon icon => row with
            {
                Text = icon.Text,
                Icon = IconOf(icon.Glyph, icon.GlyphColour),
                Picker = () => new IconPicker(icon.Current, new IconLook(Glyphs.View, icon.DefaultText), icon.DefaultName, glyph =>
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
                Icon = IconOf(submenu.Glyph, submenu.GlyphColour),
                Rows = [.. submenu.Lines.Select(l => Of(l, ask))],
                Run = submenu.Run,
                Order = submenu.Order is { } order ? new MenuOrder(order.Index, order.Count, order.Move) : null,
            },
            SettingsCommand command => row with
            {
                Text = command.Text,
                Value = command.Value,
                Icon = IconOf(command.Glyph, command.GlyphColour),
                Danger = command.Danger,
                Run = command.Run,
            },

            // [S-3736] A line of text kept in its box: Enter or leaving keeps it, Escape restores it.
            SettingsAsk text => row with
            {
                Text = text.Text,
                Icon = IconOf(text.Glyph, null),
                Edit = EditOf(text),
            },
            SettingsLink link => row with { Text = link.Text, Icon = IconOf(link.Glyph, null), Owner = link.Owner, Run = link.Open },
            SettingsItem item => row with
            {
                Text = item.Text,
                Icon = IconOf(item.Glyph, item.GlyphColour),
                Order = item.Order is { } order ? new MenuOrder(order.Index, order.Count, order.Move) : null,
                Tools = [.. item.Tools.Select(t => ToolOf(t, ask))],
            },
            _ => throw new ArgumentOutOfRangeException(nameof(line), line.GetType().Name, "a settings line no page draws"),
        };
    }

    private static PageEdit EditOf(SettingsAsk ask) => new(ask.Placeholder, ask.Initial, ask.Done)
    {
        Refuse = ask.Refuse,
        AllowEmpty = ask.AllowEmpty,
        Accept = ask.Accept,
        Title = ask.Title,
    };

    /// <summary>[S-3549] A tool: a line of text asked on a page of its own (Rename), or a command (Delete, asked first by its host).</summary>
    private static PageTool ToolOf(SettingsTool tool, Action<PageEdit> ask) => new(
        tool.Key,
        tool.Glyph,
        tool.Name,
        tool.Shortcut switch
        {
            SettingsToolKey.Rename => Windows.System.VirtualKey.F2,
            SettingsToolKey.Delete => Windows.System.VirtualKey.Delete,
            _ => null,
        },
        tool.Ask is { } text ? () => ask(EditOf(text)) : tool.Run ?? (() => { }),
        tool.Disabled);

    /// <summary>
    /// [S-2302] [S-3719] A widget's own entries (and the card's) as rows: a submenu of choices a choice, any
    /// other submenu an expander, a search a lookup, a toggle a toggle, a link a link, a line that asks an
    /// action with a chevron, an ordered entry an item.
    /// </summary>
    public static List<PageRow> Of(IReadOnlyList<ItemMenuEntry> entries, string prefix)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var rows = new List<PageRow>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var key = prefix + "/" + (entry.IsSeparator ? "-" + i : entry.Text);
            rows.Add(Of(entry, key));
        }

        return rows;
    }

    private static PageRow Of(ItemMenuEntry entry, string key)
    {
        Func<UIElement?>? icon = entry.Glyph is null && entry.IconFile is null ? null : () => ItemMenu.IconFor(entry);
        var value = entry.Value ?? (entry.Shortcut is { } chord ? KeyChordText.Format(chord) : entry.Search?.Value);
        if (entry.IsSeparator)
        {
            return new PageRow(key, SettingsRowKind.Break);
        }

        if (entry.Colour is { } colour)
        {
            return new PageRow(key, SettingsRowKind.Colour) { Text = entry.Text, Colour = colour, Disabled = entry.Disabled, SetsUp = entry.SetsUp };
        }

        if (entry.Note)
        {
            return new PageRow(key, SettingsRowKind.Note) { Text = entry.Text };
        }

        if (entry.Slider is { } slider)
        {
            return new PageRow(key, SettingsRowKind.Number) { Text = entry.Text, Value = entry.Value, Icon = icon, Number = slider, Disabled = entry.Disabled, SetsUp = entry.SetsUp };
        }

        if (entry.Search is { } search)
        {
            return new PageRow(key, SettingsRowKind.Lookup)
            {
                Text = entry.Text,
                Value = value,
                Icon = icon,
                Search = search,
                Rows = Of(search.Fixed, key + "/fixed"),
                Disabled = entry.Disabled,
                SetsUp = entry.SetsUp,
            };
        }

        if (entry.Children is { } children)
        {
            var rows = Of(children, key);
            var chosen = children.FirstOrDefault(k => k is { Checked: true, Group: not null });
            var choice = children.Count > 0 && children.All(c => c.IsSeparator || c.Group is not null);
            return new PageRow(key, choice ? SettingsRowKind.Choice : SettingsRowKind.Expander)
            {
                Text = entry.Text,
                Value = value ?? (chosen.Group is not null ? chosen.Text : null),
                Icon = icon,
                Rows = rows,
                Disabled = entry.Disabled,
                SetsUp = entry.SetsUp,
            };
        }

        var action = entry.Action;
        if (entry.Group is not null && entry.Checked is { } holds)
        {
            return new PageRow(key, SettingsRowKind.Radio) { Text = entry.Text, Icon = icon, Holds = holds, Run = action, Disabled = entry.Disabled };
        }

        if (entry.Checked is { } on)
        {
            return new PageRow(key, SettingsRowKind.Toggle) { Text = entry.Text, Caption = entry.Value, Icon = icon, On = on, Toggle = _ => action?.Invoke(), Disabled = entry.Disabled, SetsUp = entry.SetsUp };
        }

        if (entry.LinkTo is { } owner)
        {
            return new PageRow(key, SettingsRowKind.Link) { Text = entry.Text, Icon = icon, Owner = owner, Run = action, Disabled = entry.Disabled };
        }

        return new PageRow(key, entry.Order is not null ? SettingsRowKind.Item : SettingsRowKind.Action)
        {
            Text = entry.Text,
            Value = value,
            Icon = icon,
            Run = action,
            Danger = entry.Danger,
            Disabled = entry.Disabled,
            Asks = entry.Asks,
            Order = entry.Order,
            SetsUp = entry.SetsUp,
        };
    }

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
