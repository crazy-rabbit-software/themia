using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>[S-5551] Themia's own lines in the desktop's background menu.</summary>
public enum DesktopMenuCommand
{
    /// <summary>Enter edit mode, as the card menu's way in and Ctrl+Alt+W do.</summary>
    EditWidgets,

    /// <summary>Enter edit mode and open Add widget where the click was ([S-1744]'s picker at the click).</summary>
    AddWidget,
}

/// <summary>
/// [S-5551] One line of the shell's background menu as it stands after the shell filled it: a
/// separator, or a command with its canonical verb (null when the handler gives none).
/// </summary>
public readonly record struct ShellMenuLine(bool Separator, string? Verb);

/// <summary>[S-5551] Where Themia's group goes in the shell's menu: before line <see cref="Index"/>, with a separator before and after it as needed.</summary>
public readonly record struct DesktopMenuSlot(int Index, bool SeparatorBefore, bool SeparatorAfter);

/// <summary>
/// [S-5551] [S-5552] The desktop's background menu around the shell's own lines, as pure rules: which
/// lines Themia adds and where, and whether the command the user chose was one of New ›'s.
/// </summary>
public static class DesktopBackground
{
    /// <summary>The verb of New ›'s Folder line, which every New › holds.</summary>
    public const string NewFolderVerb = "NewFolder";

    /// <summary>[S-5551] Themia's lines, in their order: the way into edit mode, then Add widget.</summary>
    public static IReadOnlyList<DesktopMenuCommand> ThemiaLines { get; } = [DesktopMenuCommand.EditWidgets, DesktopMenuCommand.AddWidget];

    /// <summary>
    /// [S-5551] Where Themia's group goes: where Windows puts an app's lines on the desktop's menu, the
    /// group right after Paste and Paste shortcut (the shell's "paste" and "pastelink"), before New ›
    /// and Display settings; with no paste line, first. A separator stands between the group and the
    /// shell's lines on either side, never two in a row and none at an end.
    /// </summary>
    public static DesktopMenuSlot Slot(IReadOnlyList<ShellMenuLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var paste = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].Separator && lines[i].Verb is { } verb
                && (verb.Equals("paste", StringComparison.OrdinalIgnoreCase) || verb.Equals("pastelink", StringComparison.OrdinalIgnoreCase)))
            {
                paste = i;
            }
        }

        var index = 0;
        if (paste >= 0)
        {
            index = paste + 1;
            while (index < lines.Count && !lines[index].Separator)
            {
                index++;
            }
        }

        return new DesktopMenuSlot(
            index,
            SeparatorBefore: index > 0 && !lines[index - 1].Separator,
            SeparatorAfter: index < lines.Count && !lines[index].Separator);
    }

    /// <summary>
    /// [S-5552] Whether the chosen command is one of New ›'s: its submenu (the verbs of the lines beside
    /// it, <paramref name="siblingVerbs"/>) holds New › Folder. A top-level command has no siblings here.
    /// </summary>
    public static bool IsNewCommand(IReadOnlyList<string?>? siblingVerbs)
        => siblingVerbs is not null && siblingVerbs.Any(v => string.Equals(v, NewFolderVerb, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// [S-5524] Whether edit mode's Delete for <paramref name="selection"/> takes widgets off this view
    /// only ("Remove from this view"): every selected widget is a desktop item's, whose file stays.
    /// A selection with any other widget says Delete, as before.
    /// </summary>
    public static bool RemovesFromView(IEnumerable<WidgetConfig?> selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var any = false;
        foreach (var config in selection)
        {
            if (config is not IDesktopItemConfig)
            {
                return false;
            }

            any = true;
        }

        return any;
    }
}
