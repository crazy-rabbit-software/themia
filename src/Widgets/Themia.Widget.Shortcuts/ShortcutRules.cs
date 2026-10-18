using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Widget.Shortcuts;

namespace Themia.Core.Widgets;

/// <summary>[S-1387] What a shortcut's item menu can do. Each one is offered only where it means something.</summary>
public enum ShortcutCommand
{
    Open,
    OpenAll,
    CopyUrl,
    CopyPath,
    OpenFileLocation,
    Rename,
    Remove,

    /// <summary>[S-1405] Open the layout editor on an app layout.</summary>
    EditLayout,

    /// <summary>[S-4864] Empty the Recycle Bin, Windows asking first ([S-4863]): on an item pointing at it.</summary>
    EmptyRecycleBin,
}

/// <summary>One line of a shortcut's item menu; a null command is a separator.</summary>
public readonly record struct ShortcutMenuEntry(ShortcutCommand? Command, bool Disabled = false)
{
    public static ShortcutMenuEntry Separator => new(null);
}

/// <summary>[S-1386] What a target turned out to be when it was looked at (never when it was opened).</summary>
public enum TargetHealth
{
    /// <summary>Not looked at: a web address, a shell location, a network path, or the answer was unclear.</summary>
    Unknown = 0,

    File = 1,

    Folder = 2,

    /// <summary>A local path that is not there. Drawn dimmed and struck through.</summary>
    Missing = 3,
}

/// <summary>
/// The Shortcuts widget's rules that are not layout: search, the item menu, the Windows-location
/// switches, the Recycle Bin badge and the broken-target check. Pure, so each is tested.
/// </summary>
public static class ShortcutRules
{

    /// <summary>
    /// [S-1385] Whether an item stays visible under a search: its shown name or its target holds the
    /// query, ignoring case (original S-43). An empty query hides nothing.
    /// </summary>
    public static bool Matches(string name, string target, string? query)
    {
        var q = (query ?? string.Empty).Trim();

        if (q.Length == 0)
        {
            return true;
        }

        return name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || target.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// [S-1387] The item menu for a right-click (original S-26): Open first ("Open all" when the
    /// click landed on a multi-selection), type-specific actions for a single item, then Rename and
    /// Remove last. Rename is off while several items are selected; a refused target cannot be
    /// opened but can still be renamed and removed. No moves: the order is dragged, or moved with
    /// Alt+Shift+Up / Down ([S-3802]).
    /// </summary>
    public static IReadOnlyList<ShortcutMenuEntry> Menu(LaunchTarget target, int selected)
        => Menu(target, selected, layout: null);

    /// <summary>
    /// [S-1405] The item menu, for an app layout too (original S-26): "Open" launches it (off while
    /// no pane opens anything), "Edit layout…" replaces the target-specific actions, and Rename and
    /// Remove are as for any item. [S-4864] An item pointing at the Recycle Bin also offers "Empty
    /// Recycle Bin…", off unless <paramref name="canEmptyBin"/> (<see cref="RecycleBinState.CanEmpty"/>).
    /// </summary>
    public static IReadOnlyList<ShortcutMenuEntry> Menu(LaunchTarget target, int selected, LayoutNode? layout, bool canEmptyBin = false)
    {
        if (selected > 1)
        {
            return
            [
                new(ShortcutCommand.OpenAll),
                ShortcutMenuEntry.Separator,
                new(ShortcutCommand.Rename, Disabled: true),
                new(ShortcutCommand.Remove),
            ];
        }

        var openable = layout is null ? target.IsValid : LayoutTree.AssignedCount(layout) > 0;
        var entries = new List<ShortcutMenuEntry> { new(ShortcutCommand.Open, Disabled: !openable), ShortcutMenuEntry.Separator };

        switch (layout is null ? target.Kind : LaunchKind.Refused)
        {
            case LaunchKind.Refused when layout is not null:
                entries.Add(new(ShortcutCommand.EditLayout));
                break;

            case LaunchKind.Web:
                entries.Add(new(ShortcutCommand.CopyUrl));
                break;

            case LaunchKind.LocalPath:
                entries.Add(new(ShortcutCommand.OpenFileLocation));
                entries.Add(new(ShortcutCommand.CopyPath));
                break;

            case LaunchKind.ShellNamespace:
                if (IsRecycleBin(target.Value))
                {
                    entries.Add(new(ShortcutCommand.EmptyRecycleBin, Disabled: !canEmptyBin));
                }

                entries.Add(new(ShortcutCommand.CopyPath));
                break;
        }

        entries.Add(ShortcutMenuEntry.Separator);
        entries.Add(new(ShortcutCommand.Rename));
        entries.Add(new(ShortcutCommand.Remove));
        return entries;
    }

    /// <summary>
    /// [S-3802] Move one item a place up (-1) or down (+1): the keyboard's twin of the drag
    /// ([S-1410]). Returns whether anything moved: an end, an unknown id or no step writes nothing.
    /// </summary>
    public static bool Move(List<ShortcutItem> items, string id, int delta)
    {
        ArgumentNullException.ThrowIfNull(items);
        return DeskListOrder.Move(items, items.FindIndex(item => item.Id == id), delta);
    }

    /// <summary>[S-1388] Whether any item points at the Windows location <paramref name="shellName"/>.</summary>
    public static bool HasLocation(IEnumerable<ShortcutItem> items, string shellName)
    {
        var target = LaunchTargets.ShellTarget(shellName);
        return items.Any(item => LaunchTargets.SameTarget(item.Target, target));
    }

    /// <summary>
    /// [S-1388] Turn a Windows-location switch on or off (original S-16): on appends one item for it
    /// when none points there; off removes <i>every</i> item that points there, so the switch always
    /// says what the widget holds. Returns whether the list changed.
    /// </summary>
    public static bool SetLocation(List<ShortcutItem> items, string shellName, bool on)
    {
        var target = LaunchTargets.ShellTarget(shellName);

        if (on)
        {
            if (HasLocation(items, shellName))
            {
                return false;
            }

            items.Add(ShortcutItem.Create(target));
            return true;
        }

        return items.RemoveAll(item => LaunchTargets.SameTarget(item.Target, target)) > 0;
    }

    /// <summary>[S-1389] Whether an item is the Recycle Bin.</summary>
    public static bool IsRecycleBin(string? target) => LaunchTargets.SameTarget(target, RecycleBinState.Target);

    /// <summary>
    /// [S-1389] The badge on the Recycle Bin's icon (original S-75): its item count, capped at
    /// "99+"; null — no badge — when it is empty or its state is not known.
    /// </summary>
    public static string? BadgeText(RecycleBinState? state)
    {
        if (state is not { } bin || bin.IsEmpty)
        {
            return null;
        }

        return bin.Items > 99 ? "99+" : bin.Items.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// [S-1386] Look at a target without opening it (original S-44): only a probeable local path is
    /// asked about; everything else is <see cref="TargetHealth.Unknown"/> and is never drawn broken.
    /// </summary>
    public static TargetHealth Check(LaunchTarget target, Func<string, TargetHealth> probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        if (target.Kind != LaunchKind.LocalPath || !LaunchTargets.MayProbe(target))
        {
            return TargetHealth.Unknown;
        }

        return probe(target.Value);
    }

    /// <summary>
    /// [S-1405] The details view's kind column for an app layout, which is also what a layout
    /// nobody named is called.
    /// </summary>
    public static string LayoutKindLabel() => LayoutKindLabel(Strings.Current);

    /// <inheritdoc cref="LayoutKindLabel()"/>
    public static string LayoutKindLabel(Locale locale) => Strings.Get(locale, Str.Shortcut_ChipLayout);

    /// <summary>[S-1384] The details view's kind column.</summary>
    public static string KindLabel(LaunchTarget target, TargetHealth health) => KindLabel(Strings.Current, target, health);

    /// <inheritdoc cref="KindLabel(LaunchTarget, TargetHealth)"/>
    public static string KindLabel(Locale locale, LaunchTarget target, TargetHealth health) => target.Kind switch
    {
        LaunchKind.Web => Strings.Get(locale, Str.WidgetsC_Shortcuts_Kind_Web),
        LaunchKind.ShellNamespace => ShortcutsStr.WidgetsC_Shortcuts_Kind_Location.Text(locale),
        LaunchKind.LocalPath => health switch
        {
            TargetHealth.Folder => Strings.Get(locale, Str.Shortcut_KindFolder),
            TargetHealth.File => Strings.Get(locale, Str.Shortcut_ChipFile),
            TargetHealth.Missing => ShortcutsStr.WidgetsC_Shortcuts_Kind_Missing.Text(locale),
            _ => ShortcutsStr.WidgetsC_Shortcuts_Kind_Path.Text(locale),
        },
        _ => ShortcutsStr.WidgetsC_Shortcuts_Kind_Refused.Text(locale),
    };
}
