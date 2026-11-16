using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>[S-5548] A command of the desktop's background menu.</summary>
public enum DesktopMenuCommand
{
    /// <summary>[S-65] "Unlock widgets": enter edit mode, as the card menu's line and Ctrl+Alt+W do.</summary>
    UnlockWidgets,

    /// <summary>Enter edit mode and open Add widget ([S-11707]).</summary>
    AddWidget,

    /// <summary>[S-5554] Sort by › Name: the display's items back into the grid in name order.</summary>
    SortByName,

    /// <summary>[S-5554] Sort by › Size.</summary>
    SortBySize,

    /// <summary>[S-5554] Sort by › Item type.</summary>
    SortByItemType,

    /// <summary>[S-5554] Sort by › Date modified.</summary>
    SortByDateModified,

    /// <summary>[S-5554] Refresh: read the desktop again, as F5 does ([S-5545]).</summary>
    Refresh,


    /// <summary>[S-5593] View › Large icons.</summary>
    LargeIcons,

    /// <summary>[S-5593] View › Medium icons.</summary>
    MediumIcons,

    /// <summary>[S-5593] View › Small icons.</summary>
    SmallIcons,

    /// <summary>[S-5594] View › Auto arrange icons: once, now: the icons packed from the top-left corner in column order.</summary>
    AutoArrangeIcons,

    /// <summary>The clipboard's files into the desktop folder, as Ctrl+V ([S-5545]).</summary>
    Paste,

    /// <summary>A shortcut to each of the clipboard's files (the shell's "pastelink").</summary>
    PasteShortcut,

    /// <summary>New › Folder.</summary>
    NewFolder,

    /// <summary>New › Shortcut: Windows' Create Shortcut wizard.</summary>
    NewShortcut,

    /// <summary>Windows Settings' Display page.</summary>
    DisplaySettings,

    /// <summary>Windows Settings' Personalisation page.</summary>
    Personalise,

    /// <summary>Windows Terminal in the desktop folder.</summary>
    OpenInTerminal,

    /// <summary>The shell's classic background menu.</summary>
    ShowMoreOptions,
}

/// <summary>[S-5548] The submenus of the desktop's background menu, each with its own title.</summary>
public enum DesktopMenuGroup
{
    /// <summary>View ›.</summary>
    View,

    /// <summary>Sort by ›.</summary>
    SortBy,

    /// <summary>New ›: Folder, Shortcut, then the shell's New file types, which the host appends.</summary>
    New,
}

/// <summary>
/// [S-5548] One line of the desktop's background menu: a command (with a check mark while
/// <see cref="Checked"/>, or with a radio's dot while <see cref="Chosen"/>: one of a set of which one
/// holds, as Explorer marks the icon size and the sort), a submenu (<see cref="Group"/>, its title) of
/// command lines (<see cref="Children"/>), or a separator (neither).
/// </summary>
public sealed record DesktopMenuEntry(DesktopMenuCommand? Command, DesktopMenuGroup? Group, IReadOnlyList<DesktopMenuEntry> Children, bool Checked)
{
    /// <summary>[S-5593] [S-5596] Null for a plain or checked line; for one of a set, whether it is the one that holds (it carries the dot).</summary>
    public bool? Chosen { get; init; }

    /// <summary>One of a set of which one holds: the dot while <paramref name="chosen"/>.</summary>
    public static DesktopMenuEntry Radio(DesktopMenuCommand command, bool chosen) => new(command, null, [], false) { Chosen = chosen };

    /// <summary>A divider between groups.</summary>
    public static DesktopMenuEntry Separator { get; } = new(null, null, [], false);

    /// <summary>Whether this is a divider.</summary>
    public bool IsSeparator => Command is null && Group is null;

    /// <summary>A plain command line.</summary>
    public static DesktopMenuEntry Of(DesktopMenuCommand command, bool @checked = false) => new(command, null, [], @checked);

    /// <summary>A submenu of command lines.</summary>
    public static DesktopMenuEntry Sub(DesktopMenuGroup group, params DesktopMenuEntry[] children) => new(null, group, children, false);
}

/// <summary>[S-5548] What the desktop's background menu depends on.</summary>
/// <param name="ClipboardHasFiles">Files are on the clipboard.</param>
/// <param name="HasTerminal">Windows Terminal is installed.</param>
/// <param name="IconSize">[S-5593] The desktop's icon size.</param>
/// <param name="Sort">[S-5596] What the icons were last sorted by.</param>
public readonly record struct DesktopBackgroundFacts(
    bool ClipboardHasFiles,
    bool HasTerminal,
    DesktopIconSize IconSize = DesktopIconSize.Medium,
    DesktopSortBy Sort = DesktopSortBy.Name)
{
    /// <summary>The facts of <paramref name="state"/>: its view settings and its last sort.</summary>
    public static DesktopBackgroundFacts Of(AppState state, bool clipboardHasFiles, bool hasTerminal)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new(clipboardHasFiles, hasTerminal, state.Preferences.DesktopIconSize, state.Desktop.Sort);
    }
}

/// <summary>
/// [S-5548] [S-5552] The desktop's background menu as pure rules: which lines it holds, and whether a
/// command chosen in the shell's classic menu was one of New ›'s.
/// </summary>
public static class DesktopBackground
{
    /// <summary>The verb of New ›'s Folder line, which every New › holds.</summary>
    public const string NewFolderVerb = "NewFolder";

    /// <summary>
    /// [S-5548] [S-5551] [S-5554] [S-5557] The background menu, as Windows 11's desktop shows it:
    /// **View ›** ([S-5593], [S-5594]: Large, Medium and Small icons, the one in force carrying the dot;
    /// Auto arrange icons, which packs the icons once, now: no setting, no check mark; the icons always
    /// show and always stand on the icon grid, [S-5595]), **Sort by ›** (Name, Size, Item type, Date
    /// modified, the last sort carrying the dot, [S-5596]) and
    /// **Refresh**; **Paste** and **Paste shortcut** while the clipboard holds files, and **New ›**
    /// (Folder, Shortcut; the host appends the shell's file types); **Display settings**,
    /// **Personalise** and, where Windows Terminal is installed, **Open in Terminal**; Themia's own
    /// **Unlock widgets** and **Add widget**, where an app's lines go; **Show more options** last. One
    /// separator between the groups, never two in a row and none at an end. Undo is not offered
    /// ([S-5545]).
    /// </summary>
    public static IReadOnlyList<DesktopMenuEntry> Menu(DesktopBackgroundFacts facts)
    {
        var lines = new List<DesktopMenuEntry>
        {
            DesktopMenuEntry.Sub(
                DesktopMenuGroup.View,
                DesktopMenuEntry.Radio(DesktopMenuCommand.LargeIcons, facts.IconSize == DesktopIconSize.Large),
                DesktopMenuEntry.Radio(DesktopMenuCommand.MediumIcons, facts.IconSize == DesktopIconSize.Medium),
                DesktopMenuEntry.Radio(DesktopMenuCommand.SmallIcons, facts.IconSize == DesktopIconSize.Small),
                DesktopMenuEntry.Separator,
                DesktopMenuEntry.Of(DesktopMenuCommand.AutoArrangeIcons)),
            DesktopMenuEntry.Sub(
                DesktopMenuGroup.SortBy,
                DesktopMenuEntry.Radio(DesktopMenuCommand.SortByName, facts.Sort == DesktopSortBy.Name),
                DesktopMenuEntry.Radio(DesktopMenuCommand.SortBySize, facts.Sort == DesktopSortBy.Size),
                DesktopMenuEntry.Radio(DesktopMenuCommand.SortByItemType, facts.Sort == DesktopSortBy.ItemType),
                DesktopMenuEntry.Radio(DesktopMenuCommand.SortByDateModified, facts.Sort == DesktopSortBy.DateModified)),
        };

        lines.Add(DesktopMenuEntry.Of(DesktopMenuCommand.Refresh));
        lines.Add(DesktopMenuEntry.Separator);
        if (facts.ClipboardHasFiles)
        {
            lines.Add(DesktopMenuEntry.Of(DesktopMenuCommand.Paste));
            lines.Add(DesktopMenuEntry.Of(DesktopMenuCommand.PasteShortcut));
        }

        lines.Add(DesktopMenuEntry.Sub(
            DesktopMenuGroup.New,
            DesktopMenuEntry.Of(DesktopMenuCommand.NewFolder),
            DesktopMenuEntry.Of(DesktopMenuCommand.NewShortcut)));
        lines.Add(DesktopMenuEntry.Separator);
        lines.Add(DesktopMenuEntry.Of(DesktopMenuCommand.DisplaySettings));
        lines.Add(DesktopMenuEntry.Of(DesktopMenuCommand.Personalise));
        if (facts.HasTerminal)
        {
            lines.Add(DesktopMenuEntry.Of(DesktopMenuCommand.OpenInTerminal));
        }

        lines.Add(DesktopMenuEntry.Separator);
        lines.Add(DesktopMenuEntry.Of(DesktopMenuCommand.UnlockWidgets));
        lines.Add(DesktopMenuEntry.Of(DesktopMenuCommand.AddWidget));
        lines.Add(DesktopMenuEntry.Separator);
        lines.Add(DesktopMenuEntry.Of(DesktopMenuCommand.ShowMoreOptions));
        return lines;
    }

    /// <summary>[S-5554] The criterion a Sort by line sorts by; null for any other command.</summary>
    public static DesktopSortBy? SortOf(DesktopMenuCommand command) => command switch
    {
        DesktopMenuCommand.SortByName => DesktopSortBy.Name,
        DesktopMenuCommand.SortBySize => DesktopSortBy.Size,
        DesktopMenuCommand.SortByItemType => DesktopSortBy.ItemType,
        DesktopMenuCommand.SortByDateModified => DesktopSortBy.DateModified,
        _ => null,
    };

    /// <summary>[S-5593] The icon size a View › size line sets; null for any other command.</summary>
    public static DesktopIconSize? SizeOf(DesktopMenuCommand command) => command switch
    {
        DesktopMenuCommand.LargeIcons => DesktopIconSize.Large,
        DesktopMenuCommand.MediumIcons => DesktopIconSize.Medium,
        DesktopMenuCommand.SmallIcons => DesktopIconSize.Small,
        _ => null,
    };

    /// <summary>
    /// [S-5552] Whether the command chosen in the shell's classic menu (Show more options) is one of
    /// New ›'s: its submenu (the verbs of the lines beside it, <paramref name="siblingVerbs"/>) holds
    /// New › Folder. A top-level command has no siblings here. The modern menu's New › knows its own lines.
    /// </summary>
    public static bool IsNewCommand(IReadOnlyList<string?>? siblingVerbs)
        => siblingVerbs is not null && siblingVerbs.Any(v => string.Equals(v, NewFolderVerb, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// [S-5524] Whether edit mode's Delete for <paramref name="selection"/> takes widgets off this view
    /// only ("Remove from this view"): every selected widget is a desktop item's, whose file stays.
    /// A selection with any other widget (an own icon among them, which is gone with its widget) says Delete.
    /// </summary>
    public static bool RemovesFromView(IEnumerable<WidgetConfig?> selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var any = false;
        foreach (var config in selection)
        {
            if (config is not IDesktopItemConfig { Item.Length: > 0 })
            {
                return false;
            }

            any = true;
        }

        return any;
    }
}
