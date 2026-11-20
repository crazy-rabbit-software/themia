using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Shell;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// One line of a context menu: a command, a checkable command, a submenu, or a separator.
/// </summary>
/// <param name="Glyph">Segoe Fluent glyph, or null for none.</param>
/// <param name="Text">The label; on an icon-row command it is the tooltip and the accessible name.</param>
/// <param name="Action">What a click does; null makes this entry a separator.</param>
/// <param name="Danger">Destructive (delete, clear): drawn in the critical colour in the list.</param>
/// <param name="Disabled">Shown but not clickable, e.g. "Move up" on the first row.</param>
/// <param name="Checked">Non-null makes the entry a toggle showing this state.</param>
/// <param name="Children">
/// [S-1456] Non-null makes the entry a submenu of these entries (its own <paramref name="Action"/>
/// is ignored) — e.g. a destructive command one level down, so a stray click cannot reach it.
/// </param>
/// <param name="Quick">
/// [S-1740] The Windows 11 icon-row command this entry is (Cut, Copy, Paste, Rename, Share,
/// Duplicate, Settings, Delete). <see cref="ContextMenuLayout"/> decides whether it goes to the row.
/// </param>
/// <param name="Shortcut">
/// [S-1753] The key that does the same, shown beside the label as Windows writes it ("Ctrl+C",
/// <see cref="KeyChordText"/>); shown only, never registered.
/// </param>
/// <param name="IconFile">
/// [S-6416] A file whose shell icon is the line's icon, in place of <paramref name="Glyph"/> (an app in
/// "Open with", as Explorer draws it).
/// </param>
/// <param name="Group">
/// [S-3718] With <paramref name="Checked"/>, the entry is one of a set of choices of which one holds
/// (Windows draws a dot, not a tick): the entries of one group sharing this name.
/// </param>
/// <param name="Search">[S-3722] The properties menu's search, opened beside the line; item menus have none.</param>
/// <param name="Value">
/// [S-3726] What the line holds, at its end in the secondary colour (a number, a folder's name); the
/// properties menu draws it, item menus write a shortcut there instead.
/// </param>
/// <param name="Colour">[S-3724] The properties menu's colour line: its submenu is the one colour submenu; item menus have none.</param>
/// <param name="Note">[S-3728] A line of words only (a data source's credit): greyed, never clickable, trimmed with the whole as its tooltip.</param>
/// <param name="SetsUp">
/// [S-1862] The properties menu's line where what the widget waits for is set: the card's Set up
/// button opens the menu at it (<see cref="Themia.Sdk.WinUI.WidgetMenuLines.SetsUp"/>); item menus have none.
/// </param>
/// <param name="Slider">[S-3731] The properties menu's inline slider (words, slider, value in one line); item menus have none.</param>
/// <param name="Order">[S-3732] The properties menu's ordered list item: its line is dragged up and down among the list's; item menus have none.</param>
/// <param name="Ask">
/// [S-2351] [S-3721] The properties menu's line of text asked in place (<see cref="Themia.Sdk.WinUI.WidgetMenuLines.Ask"/>:
/// Rename…, Add…, a path typed): its card opens on the field and its accept, never a page of its own; item menus have none.
/// </param>
/// <param name="Confirm">
/// [S-2351] [S-3727] The properties menu's "are you sure?" asked in place (<see cref="Themia.Sdk.WinUI.WidgetMenuLines.Confirm"/>):
/// its card opens on the question and its answers; only the answer does it. Item menus have none.
/// </param>
/// <param name="LinkTo">
/// [S-3749] A link row: its <paramref name="Action"/> closes the menu and opens another menu at the level that owns
/// the setting (⚙ › Accounts), whose name this is (the tooltip says "Opens Accounts"); drawn with Windows' "open in
/// new window" mark at its end, no chevron. Null for any other line; item menus ignore it.
/// </param>
/// <param name="Shares">
/// [S-11753] The shared setting (<see cref="Themia.Core.Model.SharedSettings"/>, by its key) this line sets: a group's
/// page finds it among a member's lines and shows it once for every member that holds the setting. Null for a
/// line of the widget's own; item menus ignore it.
/// </param>
/// <param name="Icon">[S-9602] The properties menu's icon line: the one icon picker inline in its card; item menus have none.</param>
/// <param name="Field">[S-2332] The properties menu's line of text typed in place, its box at the line's end; item menus have none.</param>
/// <param name="Why">
/// [S-2375] Why a <paramref name="Disabled"/> line is greyed, said under its name on the page (Karpo's §309); item menus
/// ignore it. Null for nothing said.
/// </param>
public readonly record struct ItemMenuEntry(
    string? Glyph,
    string Text,
    Action? Action,
    bool Danger = false,
    bool Disabled = false,
    bool? Checked = null,
    IReadOnlyList<ItemMenuEntry>? Children = null,
    QuickCommand? Quick = null,
    KeyChord? Shortcut = null,
    string? IconFile = null,
    string? Group = null,
    Themia.Sdk.WinUI.MenuSearch? Search = null,
    string? Value = null,
    Themia.Sdk.WinUI.MenuColour? Colour = null,
    bool Note = false,
    bool SetsUp = false,
    Themia.Sdk.WinUI.MenuSlider? Slider = null,
    Themia.Sdk.WinUI.MenuOrder? Order = null,
    Themia.Sdk.WinUI.WidgetTextAsk? Ask = null,
    Themia.Sdk.WinUI.WidgetConfirm? Confirm = null,
    string? LinkTo = null,
    string? Shares = null,
    Themia.Sdk.WinUI.MenuIcon? Icon = null,
    Themia.Sdk.WinUI.MenuField? Field = null,
    string? Why = null)
{
    /// <summary>A divider between groups. Leading, trailing and doubled separators are dropped.</summary>
    public static ItemMenuEntry Separator => new(null, string.Empty, null);

    public bool IsSeparator => Action is null && Children is null && Search is null && Colour is null && Slider is null && Icon is null && Field is null && !Asks && !Note;

    /// <summary>[S-2351] It asks in place (a line of text or a question): its card opens on what it asks.</summary>
    public bool Asks => Ask is not null || Confirm is not null;

    /// <summary>
    /// [S-1740] Toggles and submenus never go to the icon row: an icon cannot show a check or an arrow. [S-14772] A text
    /// format does, its state lit as Windows' text menu lights it (<see cref="ContextMenuLayout.IsFormat"/>).
    /// </summary>
    internal MenuLine Line => IsSeparator ? MenuLine.Separator
        : MenuLine.Entry(Children is null && (Checked is null || Quick is { } quick && ContextMenuLayout.IsFormat(quick)) ? Quick : null);
}

/// <summary>
/// Every context menu in Themia: a widget card's, an item's inside a widget (a row, a file, a day),
/// the empty desktop's in edit mode, and a view chip's.
/// <para>
/// [S-1740] Drawn the way Windows 11's File Explorer draws its menu: a <see cref="CommandBarFlyout"/>,
/// always expanded, with the common commands as a row of icons on top and the labelled list below
/// (a plain <see cref="MenuFlyout"/> of the same list when there is no row, [S-1741]).
/// Which entry goes where is <see cref="ContextMenuLayout"/>'s, tested in Core. This is the one place
/// that builds a menu, so they all look and behave alike: shown at the pointer, the event marked
/// handled so the card's menu does not open on top of it, and a click that throws is logged instead
/// of taking the process down from inside a XAML event.
/// </para>
/// </summary>
public static class ItemMenu
{
    private const string Scope = "item-menu";

    /// <summary>The shell icon of a file at a pixel size (the host's icon cache), for a line with an <see cref="ItemMenuEntry.IconFile"/>.</summary>
    private static Func<string, int, Task<ImageSource?>>? _fileIcons;

    /// <summary>The host's shell-icon reader. Once per process, before any menu is shown.</summary>
    internal static void InstallFileIcons(Func<string, int, Task<ImageSource?>> icons)
    {
        ArgumentNullException.ThrowIfNull(icons);
        if (Interlocked.CompareExchange(ref _fileIcons, icons, null) is not null)
        {
            throw new InvalidOperationException("the file icons are installed once per process");
        }
    }

    /// <summary>
    /// Build a menu of <paramref name="entries"/>, or null when there is nothing in it.
    /// <para>
    /// [S-1740] With an icon row it is a <see cref="CommandBarFlyout"/>; without one ([S-1741]) a
    /// <see cref="MenuFlyout"/>, because a command bar flyout with no row still draws an empty bar
    /// above its list. The two lists look the same (both are the Windows 11 menu), and both are built
    /// here from the same split, so every menu still has one shape.
    /// </para>
    /// </summary>
    public static FlyoutBase? Build(IEnumerable<ItemMenuEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var list = entries.ToList();
        var split = ContextMenuLayout.Split([.. list.Select(e => e.Line)]);

        if (split.Row.Count == 0 && split.List.Count == 0)
        {
            return null;
        }

        if (split.Row.Count == 0)
        {
            // [S-1500] Said rather than left to the default: a widget's menu is always a popup of its
            // own, which the framework keeps in the display's work area.
            var plain = new MenuFlyout { ShouldConstrainToRootBounds = false };
            FillList(plain.Items, list, split);
            return plain;
        }

        var menu = new CommandBarFlyout { AlwaysExpanded = true, ShouldConstrainToRootBounds = false };
        Fill(menu, list, split);
        return menu;
    }

    /// <summary>
    /// Replace what <paramref name="menu"/> holds with <paramref name="entries"/> — for a menu that
    /// lives on a control (a <c>ContextFlyout</c>) and is refilled each time it opens. Meant for a
    /// menu that always has an icon row (a text box's Cut / Copy / Paste).
    /// </summary>
    public static void Fill(CommandBarFlyout menu, IEnumerable<ItemMenuEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(entries);

        var list = entries.ToList();
        Fill(menu, list, ContextMenuLayout.Split([.. list.Select(e => e.Line)]));
    }

    private static void Fill(CommandBarFlyout menu, List<ItemMenuEntry> list, MenuSplit split)
    {
        menu.PrimaryCommands.Clear();
        menu.SecondaryCommands.Clear();

        foreach (var index in split.Row)
        {
            menu.PrimaryCommands.Add(CreateQuick(menu, list[index]));
        }

        foreach (var index in split.List)
        {
            menu.SecondaryCommands.Add(index == MenuSplit.SeparatorIndex ? new AppBarSeparator() : CreateItem(menu, list[index]));
        }
    }

    /// <summary>
    /// [S-3718] Add <paramref name="entries"/> to a menu the caller built (a properties-menu
    /// submenu that starts with choices of its own), as the lines of a menu with no icon row.
    /// </summary>
    public static void AddTo(MenuFlyout menu, IEnumerable<ItemMenuEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(entries);

        var list = entries.ToList();
        FillList(menu.Items, list, ContextMenuLayout.Split([.. list.Select(e => e.IsSeparator ? MenuLine.Separator : MenuLine.Entry())]));
    }

    /// <summary>[S-1741] The list alone, as a <see cref="MenuFlyout"/>: plain lines, toggles and submenus.</summary>
    private static void FillList(IList<MenuFlyoutItemBase> items, List<ItemMenuEntry> list, MenuSplit split)
    {
        foreach (var index in split.List)
        {
            if (index == MenuSplit.SeparatorIndex)
            {
                items.Add(new MenuFlyoutSeparator());
                continue;
            }

            var entry = list[index];
            if (entry.Children is { } children)
            {
                var sub = new MenuFlyoutSubItem { Text = entry.Text, IsEnabled = !entry.Disabled };
                sub.Icon = IconFor(entry, icon => sub.Icon = icon);

                var inner = children.ToList();

                FillList(sub.Items, inner, ContextMenuLayout.Split([.. inner.Select(e => e.IsSeparator ? MenuLine.Separator : MenuLine.Entry())]));
                items.Add(sub);
                continue;
            }

            // [S-12952] One leading column: a toggle or one of a set is a plain line whose mark is its icon, never a
            // ToggleMenuFlyoutItem or RadioMenuFlyoutItem (each adds a check column beside the icons).
            var item = new MenuFlyoutItem
            {
                Text = entry.Text,
                IsEnabled = !entry.Disabled,
            };
            item.Icon = entry.Checked is not null ? MarkIcon(entry) : IconFor(entry, icon => item.Icon = icon);

            if (entry.Shortcut is { } chord)
            {
                item.KeyboardAcceleratorTextOverride = KeyChordText.Format(chord);
            }

            if (entry.Danger)
            {
                var critical = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed);
                item.Foreground = critical;
                if (item.Icon is not null)
                {
                    item.Icon.Foreground = critical;
                }
            }

            var action = entry.Action;
            item.Click += (_, _) => Run(action);
            items.Add(item);
        }
    }

    /// <summary>
    /// Show the menu at the pointer for a right-tap on <paramref name="target"/>. An empty list
    /// leaves the event unhandled, so the card's own menu opens instead.
    /// </summary>
    public static void Show(UIElement target, RightTappedRoutedEventArgs args, IEnumerable<ItemMenuEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(args);

        if (ShowAt(target, args.GetPosition(target), entries))
        {
            args.Handled = true;
        }
    }

    /// <summary>
    /// Open the menu at a point of <paramref name="target"/> (or against its edge when null, for a
    /// menu opened from the keyboard). False, and nothing shown, when there is nothing in it.
    /// <paramref name="closed"/> runs once when the menu closes, for an anchor that must stay while it is open.
    /// </summary>
    public static bool ShowAt(DependencyObject target, Point? at, IEnumerable<ItemMenuEntry> entries, Action? closed = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (Build(entries) is not { } menu)
        {
            return false;
        }

        if (closed is not null)
        {
            menu.Closed += (_, _) => Run(closed);
        }

        // Standard, not transient: the menu takes the keyboard, so arrows, Enter and Escape work in
        // it whether it was opened by the mouse or by the Menu key.
        // [S-1748] Down and to the right of the pointer, as Explorer's menu opens. A CommandBarFlyout
        // left on Auto treats the point like a text selection and opens above it, centred, which
        // put the menu away from the pointer. Near a screen edge the flyout still flips to fit.
        var options = new FlyoutShowOptions
        {
            ShowMode = FlyoutShowMode.Standard,
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
        };
        if (at is { } point)
        {
            options.Position = point;
        }

        menu.ShowAt(target, options);
        return true;
    }

    /// <summary>Open the menu against <paramref name="anchor"/>, as the Menu key or Shift+F10 does.</summary>
    public static bool Open(FrameworkElement anchor, IEnumerable<ItemMenuEntry> entries, Action? closed = null) => ShowAt(anchor, null, entries, closed);

    /// <summary>
    /// [S-14772] A text selection's bar: the menu at <paramref name="at"/> of <paramref name="target"/> (down and to the
    /// right of the pointer, [S-1748]) that comes by itself after text was selected with the pointer, so it does not take
    /// the keyboard (<see cref="FlyoutShowMode.Transient"/>): typing goes on in the text. Returns the menu, or null when
    /// there is nothing in it, for its owner to close when the text is typed in or clicked again.
    /// </summary>
    public static FlyoutBase? ShowBar(UIElement target, Point at, IEnumerable<ItemMenuEntry> entries, Action? closed = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (Build(entries) is not { } menu)
        {
            return null;
        }

        if (closed is not null)
        {
            menu.Closed += (_, _) => Run(closed);
        }

        menu.ShowAt(target, new FlyoutShowOptions
        {
            ShowMode = FlyoutShowMode.Transient,
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
            Position = at,
        });
        return menu;
    }

    /// <summary>
    /// Give <paramref name="target"/> a menu whose entries are read fresh on every right-tap (so a
    /// "Mark as done" can follow the item's state). Dispose the result to unsubscribe — on
    /// <c>Detach</c>, or when the row is recycled.
    /// </summary>
    public static IDisposable Attach(UIElement target, Func<IEnumerable<ItemMenuEntry>> entries)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(entries);

        void OnRightTapped(object sender, RightTappedRoutedEventArgs args)
        {
            try
            {
                Show(target, args, entries());
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "could not open an item menu", ex);
            }
        }

        target.RightTapped += OnRightTapped;
        return new Subscription(() => target.RightTapped -= OnRightTapped);
    }

    /// <summary>
    /// [S-1740] An icon-row command: the icon alone, its label as the tooltip (with the shortcut,
    /// when there is one) and as the name a screen reader reads.
    /// </summary>
    private static AppBarButton CreateQuick(CommandBarFlyout menu, ItemMenuEntry entry)
    {
        var button = new AppBarButton
        {
            Label = entry.Text,
            Icon = IconFor(entry with { Glyph = entry.Glyph ?? QuickGlyph(entry.Quick) }),
            IsEnabled = !entry.Disabled,

            // Icons alone, as Explorer's row: the label is the tooltip and the accessible name.
            LabelPosition = CommandBarLabelPosition.Collapsed,
        };

        var keys = entry.Shortcut is { } chord ? KeyChordText.Format(chord) : null;
        ToolTipService.SetToolTip(button, keys is not null
            ? Str.Shell_Menu_LabelWithShortcut.Format(("label", entry.Text), ("shortcut", keys))
            : entry.Text);
        AutomationProperties.SetName(button, entry.Text);

        if (keys is not null)
        {
            AutomationProperties.SetAcceleratorKey(button, keys);
        }

        // [S-14772] A text format in force (the selection is bold): lit as Windows lights a checked command of its row,
        // still a plain button ([S-12952]); its state is said to a screen reader too.
        if (entry.Checked is true)
        {
            button.Background = ThemeBrush.Of("AppBarToggleButtonBackgroundChecked", Colors.SteelBlue);
            button.Foreground = ThemeBrush.Of("AppBarToggleButtonForegroundChecked", Colors.White);
            AutomationProperties.SetItemStatus(button, Str.Toggle_On.Text());
        }

        Wire(menu, button, entry.Action);
        return button;
    }

    /// <summary>A command in the labelled list: a plain one, a toggle, or a submenu.</summary>
    private static ICommandBarElement CreateItem(CommandBarFlyout menu, ItemMenuEntry entry)
    {
        // [S-12952] One leading column: a toggle is a plain button whose mark is its icon, never an
        // AppBarToggleButton (the overflow then adds a check column beside the icons).
        var button = new AppBarButton { Label = entry.Text, IsEnabled = !entry.Disabled };
        if (entry.Checked is not null)
        {
            if (MarkIcon(entry) is { } mark)
            {
                button.Icon = mark;
            }
        }
        else if (IconFor(entry, shown => button.Icon = shown) is { } icon)
        {
            button.Icon = icon;
        }

        if (entry.Shortcut is { } chord)
        {
            // Text only: the accelerators are handled where the menu's owner listens for keys, so
            // registering real KeyboardAccelerators here would double-fire them.
            button.KeyboardAcceleratorTextOverride = KeyChordText.Format(chord);
        }

        if (entry.Danger)
        {
            button.Foreground = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed);
        }

        if (entry.Children is { } children)
        {
            // [S-1456] One level down, opened beside the line like a Windows submenu.
            if (Build(children) is { } sub)
            {
                sub.Placement = FlyoutPlacementMode.RightEdgeAlignedTop;
                button.Flyout = sub;
            }
            else
            {
                button.IsEnabled = false;
            }

            return button;
        }

        Wire(menu, button, entry.Action);
        return button;
    }

    /// <summary>
    /// [S-12952] A toggle line's icon: its mark in the one leading slot (<see cref="MenuMarks.Slot"/>: the check while on,
    /// the dot for one of a set), nothing while off.
    /// </summary>
    private static FontIcon? MarkIcon(ItemMenuEntry entry)
        => MenuMarks.Slot(entry.Checked, entry.Group is not null, entry.Glyph) is { } mark ? new FontIcon { Glyph = mark } : null;

    private static void Wire(CommandBarFlyout menu, ButtonBase button, Action? action)
    {
        button.Click += (_, _) =>
        {
            // Closed first: a command that opens something of its own (the picker, a rename box, a
            // confirmation) must not have the menu still standing over it.
            menu.Hide();
            Run(action);
        };
    }

    /// <summary>Run a command from inside a XAML event: a throw is logged, never left to escape.</summary>
    private static void Run(Action? action)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception ex)
        {
            // The label is not logged: it can carry user content (a file or task name).
            Log.Error(Scope, "a menu command failed", ex);
        }
    }

    /// <summary>
    /// A line's icon: the shell icon of its <see cref="ItemMenuEntry.IconFile"/>; else, for a line that
    /// can be clicked, File Explorer's two-tone picture of its glyph ([S-1754]); else the glyph
    /// itself (greyed by the menu when disabled); else none.
    /// <para>
    /// [S-5559] A file's icon never delays the menu: one the host already holds is the icon at once; one
    /// still being read shows the line's glyph (or an empty slot) until it arrives and is then put in its
    /// place through <paramref name="replace"/>; one the shell has none for keeps the glyph.
    /// </para>
    /// </summary>
    internal static IconElement? IconFor(ItemMenuEntry entry, Action<IconElement>? replace = null)
    {
        var glyph = entry.Glyph is null ? null
            : Lettered(entry.Glyph) ? Letters(entry.Glyph)
            : entry.Disabled ? UiLanguage.Mirrored(new FontIcon { Glyph = entry.Glyph }) : MenuArtIcons.IconOrGlyph(entry.Glyph);
        if (entry.IconFile is not { } file)
        {
            return glyph;
        }

        Task<ImageSource?> load;
        try
        {
            var icons = Volatile.Read(ref _fileIcons) ?? throw new InvalidOperationException("no file icons installed: the host installs them before any menu is shown");
            load = icons(file, IconPixels);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "a menu line's icon could not be asked for", ex);
            return glyph;
        }

        if (load.IsCompletedSuccessfully)
        {
            return load.Result is { } held ? FileIcon(held) : glyph;
        }

        if (glyph is null || replace is null)
        {
            var image = FileIcon(null);
            _ = LoadIconAsync(load, source => image.Source = source);
            return image;
        }

        _ = LoadIconAsync(load, source => replace(FileIcon(source)));
        return glyph;
    }

    /// <summary>
    /// The pixels a file's icon is asked at: the 16-DIP slot at 150 %, the scale most displays run, so one
    /// bitmap serves a line wherever its menu opens (its popup, and so its display, is not known while it is built).
    /// </summary>
    private const int IconPixels = 24;

    private static ImageIcon FileIcon(ImageSource? source) => new() { Width = FamilyShape.Icon, Height = FamilyShape.Icon, Source = source };

    /// <summary>The file's icon handed to <paramref name="show"/> when it arrives; a file with none keeps what the line shows.</summary>
    private static async Task LoadIconAsync(Task<ImageSource?> load, Action<ImageSource> show)
    {
        try
        {
            if (await load.ConfigureAwait(true) is { } source)
            {
                show(source);
            }
        }
        catch (Exception ex)
        {
            // Fire-and-forget from a menu being built: nothing may escape.
            Log.Warn(Scope, "a menu line's icon could not be loaded", ex);
        }
    }

    /// <summary>
    /// [S-1365] [S-14772] A glyph that is letters, not the icon font's (its private-use area): the note's "H" and "1.", which
    /// Segoe Fluent Icons has no picture for. Drawn in the text's face, semibold, as the letters they are.
    /// </summary>
    private static bool Lettered(string glyph) => glyph.Length > 0 && !glyph.Any(c => c is >= '\uE000' and <= '\uF8FF');

    private static FontIcon Letters(string letters)
    {
        var icon = new FontIcon { Glyph = letters, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        if (Application.Current?.Resources.TryGetValue("ContentControlThemeFontFamily", out var face) == true && face is FontFamily family)
        {
            icon.FontFamily = family;
        }

        return icon;
    }

    /// <summary>The icon a quick command has when its caller gave none.</summary>
    private static string QuickGlyph(QuickCommand? quick) => quick switch
    {
        QuickCommand.Cut => Glyphs.Cut,
        QuickCommand.Copy => Glyphs.Copy,
        QuickCommand.Paste => Glyphs.Paste,
        QuickCommand.Bold => Glyphs.Bold,
        QuickCommand.Italic => Glyphs.Italic,
        QuickCommand.Strikethrough => Glyphs.Strikethrough,
        QuickCommand.Code => Glyphs.Code,
        QuickCommand.BulletList => Glyphs.BulletList,
        QuickCommand.Checklist => Glyphs.Checklist,
        QuickCommand.Quote => Glyphs.Quote,
        QuickCommand.Rename => Glyphs.Rename,
        QuickCommand.Share => Glyphs.Share,
        QuickCommand.Duplicate => Glyphs.Duplicate,
        QuickCommand.Settings => Glyphs.Settings,
        QuickCommand.Delete => Glyphs.Delete,
        _ => Glyphs.More,
    };

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            _dispose?.Invoke();
            _dispose = null;
        }
    }
}
