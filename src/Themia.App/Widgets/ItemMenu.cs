using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Shell;
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
/// [S-1740] The Windows 11 icon-row command this entry is (Cut, Copy, Paste, Rename, Duplicate,
/// Settings, Delete). <see cref="ContextMenuLayout"/> decides whether it goes to the row.
/// </param>
/// <param name="Shortcut">The key that does the same, shown beside the label ("Ctrl+C"); text only.</param>
internal readonly record struct ItemMenuEntry(
    string? Glyph,
    string Text,
    Action? Action,
    bool Danger = false,
    bool Disabled = false,
    bool? Checked = null,
    IReadOnlyList<ItemMenuEntry>? Children = null,
    QuickCommand? Quick = null,
    string? Shortcut = null)
{
    /// <summary>A divider between groups. Leading, trailing and doubled separators are dropped.</summary>
    public static ItemMenuEntry Separator => new(null, string.Empty, null);

    public bool IsSeparator => Action is null && Children is null;

    /// <summary>[S-1740] Toggles and submenus never go to the icon row: an icon cannot show a check or an arrow.</summary>
    internal MenuLine Line => IsSeparator ? MenuLine.Separator : MenuLine.Entry(Checked is null && Children is null ? Quick : null);
}

/// <summary>
/// Every context menu in Themia: a widget card's, an item's inside a widget (a row, a file, a day),
/// the empty desktop's in edit mode, and the settings window's.
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
internal static class ItemMenu
{
    private const string Scope = "item-menu";

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
            // [S-1500] Said rather than left to the default: inside the surface's window the region
            // clips a menu to its card, so a widget's menu is always a popup of its own.
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
                if (entry.Glyph is not null)
                {
                    sub.Icon = UiLanguage.Mirrored(new FontIcon { Glyph = entry.Glyph });
                }

                var inner = children.ToList();
                FillList(sub.Items, inner, ContextMenuLayout.Split([.. inner.Select(e => e.IsSeparator ? MenuLine.Separator : MenuLine.Entry())]));
                items.Add(sub);
                continue;
            }

            MenuFlyoutItem item = entry.Checked is { } isChecked ? new ToggleMenuFlyoutItem { IsChecked = isChecked } : new MenuFlyoutItem();
            item.Text = entry.Text;
            item.IsEnabled = !entry.Disabled;

            if (entry.Glyph is not null)
            {
                item.Icon = UiLanguage.Mirrored(new FontIcon { Glyph = entry.Glyph });
            }

            if (entry.Shortcut is not null)
            {
                item.KeyboardAcceleratorTextOverride = entry.Shortcut;
            }

            if (entry.Danger)
            {
                var critical = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.IndianRed);
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
            Icon = UiLanguage.Mirrored(new FontIcon { Glyph = entry.Glyph ?? QuickGlyph(entry.Quick) }),
            IsEnabled = !entry.Disabled,

            // Icons alone, as Explorer's row: the label is the tooltip and the accessible name.
            LabelPosition = CommandBarLabelPosition.Collapsed,
        };

        ToolTipService.SetToolTip(button, entry.Shortcut is { } keys
            ? Str.Shell_Menu_LabelWithShortcut.Format(("label", entry.Text), ("shortcut", keys))
            : entry.Text);
        AutomationProperties.SetName(button, entry.Text);

        if (entry.Shortcut is not null)
        {
            AutomationProperties.SetAcceleratorKey(button, entry.Shortcut);
        }

        Wire(menu, button, entry.Action);
        return button;
    }

    /// <summary>A command in the labelled list: a plain one, a toggle, or a submenu.</summary>
    private static ICommandBarElement CreateItem(CommandBarFlyout menu, ItemMenuEntry entry)
    {
        AppBarButton? button = null;
        AppBarToggleButton? toggle = null;

        if (entry.Checked is { } isChecked)
        {
            toggle = new AppBarToggleButton { Label = entry.Text, IsChecked = isChecked, IsEnabled = !entry.Disabled };
        }
        else
        {
            button = new AppBarButton { Label = entry.Text, IsEnabled = !entry.Disabled };
        }

        var element = (Control?)button ?? toggle!;

        if (entry.Glyph is not null)
        {
            var icon = UiLanguage.Mirrored(new FontIcon { Glyph = entry.Glyph });
            if (button is not null)
            {
                button.Icon = icon;
            }
            else
            {
                toggle!.Icon = icon;
            }
        }

        if (entry.Shortcut is not null)
        {
            // Text only: the accelerators are handled where the menu's owner listens for keys, so
            // registering real KeyboardAccelerators here would double-fire them.
            if (button is not null)
            {
                button.KeyboardAcceleratorTextOverride = entry.Shortcut;
            }
            else
            {
                toggle!.KeyboardAcceleratorTextOverride = entry.Shortcut;
            }
        }

        if (entry.Danger)
        {
            element.Foreground = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.IndianRed);
        }

        if (entry.Children is { } children && button is not null)
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

        if (button is not null)
        {
            Wire(menu, button, entry.Action);
            return button;
        }

        Wire(menu, toggle!, entry.Action);
        return toggle!;
    }

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

    /// <summary>The icon a quick command has when its caller gave none.</summary>
    private static string QuickGlyph(QuickCommand? quick) => quick switch
    {
        QuickCommand.Cut => Glyphs.Cut,
        QuickCommand.Copy => Glyphs.Copy,
        QuickCommand.Paste => Glyphs.Paste,
        QuickCommand.Rename => Glyphs.Rename,
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
