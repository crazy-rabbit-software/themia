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
/// [S-3520] One of the edit bar's drop-down menus (⚙ the settings menu): the menu engine
/// (<see cref="MenuPanel"/>) over Core's lines (<see cref="SettingsLine"/>s, <see cref="SettingsMenu"/>'s
/// for ⚙), with no frame of its own (no icon row, no header): a Windows 11 context menu that stays open
/// while it is used. The lines are Core's; what is drawn here is only how each kind of line becomes
/// the engine's (a colour its one colour submenu, a font the font picker), keyed by the lines' own
/// keys, so a change of words (a language switch) is written into the lines in place and what is
/// open stays open.
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
    /// <param name="close">[S-3521] Escape with no submenu open closes the menu.</param>
    public BarMenuView(string name, Func<IReadOnlyList<SettingsLine>> lines, Action close)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _lines = lines ?? throw new ArgumentNullException(nameof(lines));
        _close = close ?? throw new ArgumentNullException(nameof(close));
    }

    /// <summary>
    /// [S-3531] Show the lines as the state holds them now: values written in place, so a submenu
    /// left open stays open on the pick; <paramref name="rebuild"/> draws everything again (a language switch).
    /// </summary>
    public void Show(bool rebuild = false)
    {
        try
        {
            Present(_name, Nodes(_lines()), null, rebuild);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the {_name} menu could not be built", ex);
        }
    }

    /// <summary>[S-3534] Open the submenus along <paramref name="path"/> (the lines' keys), as clicks would.</summary>
    public bool Open(IReadOnlyList<string> path, bool focus) => path.Count == 0 || OpenPath(path, focus);

    /// <inheritdoc />
    protected override void OnEscape() => _close();

    /// <summary>[S-3727] What was asked beside the menu closed: the keyboard comes back to the menu.</summary>
    protected override void OnAskClosed() => FocusFirst();

    /// <summary>[S-3727] Ask beside the menu (a preset over a look of one's own, deleting a view).</summary>
    public void Confirm(SettingsConfirm confirm)
    {
        ArgumentNullException.ThrowIfNull(confirm);
        Confirm(new WidgetConfirm(confirm.Question, confirm.Accept, confirm.Done, confirm.Danger, confirm.Detail));
    }

    private List<MenuNode> Nodes(IReadOnlyList<SettingsLine> lines) => [.. lines.Select(Node)];

    /// <summary>One of Core's lines as the engine's: the kinds map one to one, a colour and a fill to the one colour submenu ([S-3724]).</summary>
    private MenuNode Node(SettingsLine line) => line switch
    {
        SettingsSeparator => MenuNode.Separator(line.Key),
        SettingsNote note => new MenuNode(note.Key, MenuNodeKind.Note) { Text = note.Text },
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
        SettingsRadio radio => Option(radio.Key, radio.Text, IconKeyOf(radio.Glyph, radio.GlyphColour), IconOf(radio.Glyph, radio.GlyphColour), radio.Holds, radio.Pick) with { Disabled = radio.Disabled },
        SettingsChoice choice => new MenuNode(choice.Key)
        {
            Text = choice.Text,
            Value = choice.Value,
            IconKey = choice.Glyph ?? string.Empty,
            Icon = IconOf(choice.Glyph),
            Disabled = choice.Disabled,
            Children = Nodes(choice.Options),
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

            // [S-3541] An item's line (a view's) keeps its mark's place whether it holds or not, so
            // the dot moves from line to line in place; a plain submenu has none.
            Checked = submenu.Holds || submenu.Run is not null ? submenu.Holds : null,
            Radio = true,
            Run = submenu.Run,
            Order = submenu.Order is { } order ? new MenuOrder(order.Index, order.Count, order.Move) : null,
        },
        SettingsCommand command => new MenuNode(command.Key)
        {
            Text = command.Text,
            IconKey = IconKeyOf(command.Glyph, command.GlyphColour),
            Icon = IconOf(command.Glyph, command.GlyphColour),
            Disabled = command.Disabled,
            Danger = command.Danger,
            Run = command.Run,
        },

        // [S-3721] Rename… and the like: the menu's one question beside itself.
        SettingsAsk ask => new MenuNode(ask.Key)
        {
            Text = ask.Text,
            IconKey = ask.Glyph ?? string.Empty,
            Icon = IconOf(ask.Glyph),
            Disabled = ask.Disabled,
            Run = () => Ask(new WidgetTextAsk(ask.Title, ask.Placeholder, ask.Accept, ask.Refuse, ask.Done, ask.Initial)),
        },
        SettingsIcon icon => IconNode(icon),
        _ => throw new ArgumentOutOfRangeException(nameof(line), line.GetType().Name, "a settings line the menu cannot draw"),
    };

    private static Func<UIElement?>? IconOf(string? glyph) => glyph is null ? null : () => Glyph(glyph);

    /// <summary>[S-3810] A glyph in a colour of its own (a view's mark), or in the theme's where it has none.</summary>
    private static Func<UIElement?>? IconOf(string? glyph, string? colour)
        => glyph is null ? null : colour is null ? IconOf(glyph) : () => ViewMark.Icon(glyph, colour);

    /// <summary>What a line's icon is, its colour included: a new colour draws it again.</summary>
    private static string IconKeyOf(string? glyph, string? colour) => glyph is null ? string.Empty : glyph + "|" + colour;

    /// <summary>
    /// [S-3542] An icon line: its submenu is the icon picker (its default cell, a search box over
    /// every Segoe Fluent glyph, the chosen one selected, [S-1803]), at the menu's width. A pick
    /// stores it, and the picker is drawn again with that one chosen.
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
    /// [S-3528] The font line: its submenu is the font picker (its search box, every installed family
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
