using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-3520] One of the edit bar's drop-down menus (View ▾, Display ▾, ⚙): the menu engine
/// (<see cref="MenuPanel"/>) over Core's lines (<see cref="SettingsLine"/>s, <see cref="SettingsMenu"/>'s
/// for ⚙), with no head or foot of its own: the fixed rectangle whose levels open in place
/// ([S-3718], [S-3734]). The lines are Core's; what is drawn here is only how each kind of line becomes
/// the engine's row (a colour its one colour level, a font the font picker, a field or a question a
/// level of its own), keyed by the lines' own keys, so a change of words (a language switch) is
/// written into the rows in place and the level open stays open.
/// </summary>
internal sealed partial class BarMenuView : MenuPanel
{
    private const string Scope = "bar-menu";

    /// <summary>[S-6443] The Fonts page of Windows Settings, where fonts are added.</summary>
    private static readonly Uri FontsPage = new("ms-settings:fonts");

    private readonly string _name;
    private readonly Func<IReadOnlyList<SettingsLine>> _lines;
    private readonly Action _close;

    /// <param name="name">Which menu it is: the key its first level is drawn for, and what the log names.</param>
    /// <param name="lines">Its lines as the state holds them now, read on every change.</param>
    /// <param name="close">[S-3521] Escape on the menu's own rows closes the menu.</param>
    public BarMenuView(string name, Func<IReadOnlyList<SettingsLine>> lines, Action close)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _lines = lines ?? throw new ArgumentNullException(nameof(lines));
        _close = close ?? throw new ArgumentNullException(nameof(close));
    }

    /// <summary>
    /// [S-3531] Show the lines as the state holds them now: values written in place, so the level
    /// open stays open on the pick; <paramref name="rebuild"/> draws everything again (a language switch).
    /// </summary>
    public void Show(bool rebuild = false)
    {
        try
        {
            Present(_name, Nodes(_lines()), null, null, rebuild);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the {_name} menu could not be built", ex);
        }
    }

    /// <summary>[S-3534] Open the levels along <paramref name="path"/> (the lines' keys), as clicks would.</summary>
    public bool Open(IReadOnlyList<string> path, bool focus) => path.Count == 0 || OpenPath(path, focus);

    /// <inheritdoc />
    protected override void OnEscape() => _close();

    /// <summary>[S-3727] [S-3737] Ask on a level of the menu's own (a preset over a look of one's own, deleting a view, an import).</summary>
    public void Confirm(SettingsConfirm confirm)
    {
        ArgumentNullException.ThrowIfNull(confirm);
        AskQuestion(new MenuQuestion(confirm.Question, confirm.Accept, confirm.Done, confirm.Danger, confirm.Detail));
    }

    private List<MenuNode> Nodes(IReadOnlyList<SettingsLine> lines) => [.. lines.Select(Node)];

    /// <summary>
    /// One of Core's lines as the engine's: the kinds map one to one, a colour and a fill to the one
    /// colour submenu ([S-3724]); [S-3581] a line with a picture shows it in its icon column.
    /// </summary>
    private MenuNode Node(SettingsLine line)
    {
        var node = NodeOf(line);
        return line.Picture is { } picture
            ? node with { IconKey = "picture:" + picture, Icon = () => MenuPictures.Create(picture) }
            : node;
    }

    private MenuNode NodeOf(SettingsLine line) => line switch
    {
        SettingsSeparator => MenuNode.Separator(line.Key),
        SettingsHeading heading => new MenuNode(heading.Key, MenuNodeKind.Heading) { Text = heading.Text },
        SettingsNote note => new MenuNode(note.Key, MenuNodeKind.Note) { Text = note.Text, Danger = note.Critical, Busy = note.Busy },

        // [S-3561] A reading: a line with nothing to do.
        SettingsReading reading => new MenuNode(reading.Key)
        {
            Text = reading.Text,
            Value = reading.Value,
            IconKey = reading.Glyph ?? string.Empty,
            Icon = IconOf(reading.Glyph),
            Disabled = reading.Disabled,
        },

        // [S-3736] [S-3556] A form's field: a row whose level is the field, every letter handed to the
        // form as it is typed; a password's masked, its words never in the node, bullets for its value.
        SettingsField field => new MenuNode(field.Key)
        {
            Text = field.Text,
            Value = field.Masked ? (field.HasValue ? MaskedValue : null) : field.Value,
            Disabled = field.Disabled,
            Asks = true,
            Wipe = field.Masked ? field.Wipe : null,
            Run = () => AskText(new MenuEdit(field.Text, field.Placeholder, field.Masked ? string.Empty : field.Value ?? string.Empty, _ => { })
            {
                Live = field.Set,
                Masked = field.Masked,
                Fill = field.Masked ? field.Fill : null,
                Numeric = field.Numeric,
            }),
        },
        SettingsToggle toggle => new MenuNode(toggle.Key)
        {
            Text = toggle.Text,
            Value = toggle.Value,
            IconKey = IconKeyOf(toggle.Glyph, toggle.GlyphColour),
            Icon = IconOf(toggle.Glyph, toggle.GlyphColour),
            Checked = toggle.On,
            Disabled = toggle.Disabled,
            Run = () => toggle.Set(!toggle.On),
        },
        SettingsRadio radio => Option(radio.Key, radio.Text, IconKeyOf(radio.Glyph, radio.GlyphColour), IconOf(radio.Glyph, radio.GlyphColour), radio.Holds, radio.Pick) with { Disabled = radio.Disabled, Keywords = radio.Keywords },
        SettingsChoice choice => new MenuNode(choice.Key)
        {
            Text = choice.Text,
            Value = choice.Value,
            IconKey = choice.Glyph ?? string.Empty,
            Icon = IconOf(choice.Glyph),
            Disabled = choice.Disabled,
            Children = Nodes(choice.Options),
            Filter = choice.Filter,
        },
        SettingsSlider slider => new MenuNode(slider.Key, MenuNodeKind.Slider)
        {
            Text = slider.Text,
            Value = slider.Format(slider.Value),
            IconKey = slider.Glyph ?? string.Empty,
            Icon = IconOf(slider.Glyph),
            Disabled = slider.Disabled,
            Slider = new MenuSlider(slider.Value, slider.Min, slider.Max, slider.Step, slider.Format, slider.Set),
        },
        SettingsColour colour => ColourNode(colour.Key, colour.Text, MenuColour.Of(colour.Current, colour.Set, new ColorChoiceOptions
        {
            DefaultLabel = colour.DefaultText,
            Presets = colour.Presets,
            DefaultColor = colour.DefaultIsWindowsAccent ? ColorChoice.WindowsAccent : null,
        }), colour.Disabled),
        SettingsFill fill => ColourNode(fill.Key, fill.Text, MenuColour.OfFill(fill.Current, fill.Set, new FillChoiceOptions { DefaultLabel = fill.DefaultText }), fill.Disabled),
        SettingsFont font => FontNode(font),
        SettingsSubmenu submenu => new MenuNode(submenu.Key)
        {
            Text = submenu.Text,
            Value = submenu.Value,
            IconKey = IconKeyOf(submenu.Glyph, submenu.GlyphColour),
            Icon = IconOf(submenu.Glyph, submenu.GlyphColour),
            Disabled = submenu.Disabled,
            Children = Nodes(submenu.Lines),

            // [S-3541] An item's row (a view's) keeps its tick's place whether it holds or not, so
            // the tick moves from row to row in place; a plain level has none.
            Checked = submenu.Holds || submenu.Run is not null ? submenu.Holds : null,
            Radio = true,
            Run = submenu.Run,
            Order = submenu.Order is { } order ? new MenuOrder(order.Index, order.Count, order.Move) : null,
        },
        SettingsCommand command => new MenuNode(command.Key)
        {
            Text = command.Text,
            Value = command.Value,
            IconKey = IconKeyOf(command.Glyph, command.GlyphColour),
            Icon = IconOf(command.Glyph, command.GlyphColour),
            Disabled = command.Disabled,
            Danger = command.Danger,
            Run = command.Run,
        },

        // [S-3721] [S-3736] Rename… and the like: a line of text on a level of its own.
        SettingsAsk ask => new MenuNode(ask.Key)
        {
            Text = ask.Text,
            Value = ask.Value,
            IconKey = ask.Glyph ?? string.Empty,
            Icon = IconOf(ask.Glyph),
            Disabled = ask.Disabled,
            Asks = true,
            Run = () => AskText(new MenuEdit(ask.Title, ask.Placeholder, ask.Initial, ask.Done) { Refuse = ask.Refuse, AllowEmpty = ask.AllowEmpty, Accept = ask.Accept }),
        },

        // [S-3749] A link: the owner's icon and name; a pick closes this menu and opens the owner's at its level.
        SettingsLink link => new MenuNode(link.Key)
        {
            Text = link.Text,
            IconKey = link.Glyph ?? string.Empty,
            Icon = IconOf(link.Glyph),
            Disabled = link.Disabled,
            Link = link.Owner,
            Run = link.Open,
        },
        SettingsIcon icon => IconNode(icon),

        // [S-3549] A thing of a list one manages: its tools at its end, its place in the list's order.
        SettingsItem item => new MenuNode(item.Key)
        {
            Text = item.Text,
            IconKey = IconKeyOf(item.Glyph, item.GlyphColour),
            Icon = IconOf(item.Glyph, item.GlyphColour),
            Disabled = item.Disabled,
            Order = item.Order is { } order ? new MenuOrder(order.Index, order.Count, order.Move) : null,
            Tools = [.. item.Tools.Select(ToolOf)],
        },
        _ => throw new ArgumentOutOfRangeException(nameof(line), line.GetType().Name, "a settings line the menu cannot draw"),
    };

    private static Func<UIElement?>? IconOf(string? glyph) => glyph is null ? null : () => Glyph(glyph);

    /// <summary>[S-3549] A tool of Core's as the engine's: a line of text asked on the row's level (Rename), or a command (Delete, asked first by its host).</summary>
    private MenuTool ToolOf(SettingsTool tool) => new(
        tool.Key,
        tool.Glyph,
        tool.Name,
        tool.Shortcut switch
        {
            SettingsToolKey.Rename => Windows.System.VirtualKey.F2,
            SettingsToolKey.Delete => Windows.System.VirtualKey.Delete,
            _ => null,
        },
        tool.Ask is { } ask
            ? () => AskText(new MenuEdit(ask.Title, ask.Placeholder, ask.Initial, ask.Done) { Refuse = ask.Refuse, AllowEmpty = ask.AllowEmpty, Accept = ask.Accept })
            : tool.Run ?? (() => { }),
        tool.Disabled);

    /// <summary>[S-3810] A glyph in a colour of its own (a view's mark), or in the theme's where it has none.</summary>
    private static Func<UIElement?>? IconOf(string? glyph, string? colour)
        => glyph is null ? null : colour is null ? IconOf(glyph) : () => ViewMark.Icon(glyph, colour);

    /// <summary>What a line's icon is, its colour included: a new colour draws it again.</summary>
    private static string IconKeyOf(string? glyph, string? colour) => glyph is null ? string.Empty : glyph + "|" + colour;

    /// <summary>
    /// [S-3542] An icon row: its level is the icon picker (its default cell, a filter over every
    /// Segoe Fluent glyph, the chosen one selected, [S-1803]), at the menu's width. A pick stores it,
    /// and the picker is drawn again with that one chosen.
    /// </summary>
    private static MenuNode IconNode(SettingsIcon icon) => new(icon.Key)
    {
        Text = icon.Text,
        IconKey = IconKeyOf(icon.Glyph, icon.GlyphColour),
        Icon = IconOf(icon.Glyph, icon.GlyphColour),
        Disabled = icon.Disabled,
        Children =
        [
            new MenuNode(icon.Key + "/picker", MenuNodeKind.Custom)
            {
                ValueKey = icon.Current ?? string.Empty,
                Build = () => new IconPicker(icon.Current, new IconLook(Glyphs.View, icon.DefaultText), icon.DefaultName, glyph =>
                {
                    if (!string.Equals(glyph, icon.Current, StringComparison.Ordinal))
                    {
                        icon.Set(glyph);
                    }
                })
                {
                    Margin = new Thickness(Fluent.Space2, Fluent.Space2, Fluent.Space2, Fluent.Space2),
                },
            },
        ],
    };

    /// <summary>
    /// [S-3528] The font row: its level is the font picker (its search box, every installed family
    /// drawn in the font it names, the chosen one ticked, [S-6440]..[S-6442]), then Add fonts
    /// ([S-6443]). A pick stores it and the picker is drawn again with the new one ticked.
    /// </summary>
    private MenuNode FontNode(SettingsFont font) => new(font.Key)
    {
        Text = font.Text,
        Value = font.Value,
        IconKey = font.Glyph ?? string.Empty,
        Icon = IconOf(font.Glyph),
        Disabled = font.Disabled,
        Children =
        [
            new MenuNode(font.Key + "/picker", MenuNodeKind.Custom)
            {
                ValueKey = font.Current,
                Build = () => FontPickerFor(font.Current, family =>
                {
                    if (!string.Equals(family, font.Current, StringComparison.Ordinal))
                    {
                        font.Set(family);
                    }
                }),
            },
            MenuNode.Separator(font.Key + "/-add"),
            new MenuNode(font.Key + "/add")
            {
                Text = Str.Shell_Font_Add.Text(),
                IconKey = Glyphs.Add,
                Icon = IconOf(Glyphs.Add),
                Run = () => _ = ShellLauncher.OpenWindowsSettings(FontsPage),
            },
        ],
    };

    /// <summary>
    /// [S-827] [S-6443] The picker over the presets and the installed families: until the list has
    /// been read (off the UI thread, once) it lists the presets and the current family, so it shows
    /// what is set; it follows the list as it comes, and again while it is up whenever Windows says
    /// the installed fonts changed.
    /// </summary>
    private static FontPicker FontPickerFor(string current, Action<string> choose)
    {
        var presets = SettingsMenu.FontPresets();
        var installed = InstalledFonts.None;
        IReadOnlyList<FontOption> Options() => FontSearch.Options(presets, installed, current);

        var picker = new FontPicker(Options(), FontSearch.Chosen(Options(), current), choose)
        {
            Margin = new Thickness(Fluent.Space2, Fluent.Space1, Fluent.Space2, Fluent.Space1),
        };
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
                        Log.Warn(Scope, "could not list the installed fonts on the menu's thread");
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
