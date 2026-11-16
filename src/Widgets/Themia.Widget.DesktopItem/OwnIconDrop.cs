using Themia.Core.Widgets;

namespace Themia.Widget.DesktopItem;

/// <summary>What an Internet shortcut (<c>.url</c>) file says: its <c>URL=</c> and <c>IconFile=</c>.</summary>
public readonly record struct InternetShortcut(string? Url, string? IconFile);

/// <summary>[S-9508] What a dropped thing makes a waiting shortcut: its target, the name it brings (empty: the target's own) and the icon a <c>.url</c> named.</summary>
public sealed record DroppedTarget(string Target, string Label = "", string? IconPath = null)
{
    /// <summary>Set <paramref name="config"/> up as this: its target, its icon, and its name unless the user already gave it one.</summary>
    public void ApplyTo(DesktopItemConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.SetTarget(Target);
        config.IconPath = IconPath;
        if (config.Label.Length == 0)
        {
            config.Label = Label;
        }
    }
}

/// <summary>
/// [S-9508] What a drop onto a shortcut that still waits for setup makes it: the first dropped thing
/// the rule accepts, a file or folder by its path, a <c>.lnk</c> by what it points at, a <c>.url</c> by
/// its address, and a browser's link by its URL when no files came with it. Pure: reading the files
/// and resolving the links are the caller's, passed in.
/// <para>
/// Nothing dropped is trusted: every target goes through <see cref="LaunchTargets.Classify"/>, and
/// one the rule refuses is passed over. A <c>.url</c> pointing at a scheme the rule does not open
/// (<c>steam://</c>) is kept as the <c>.url</c> <i>file</i> — a local path, which the shell opens
/// through the protocol's own handler — rather than widening the rule.
/// </para>
/// </summary>
public static class OwnIconDrop
{
    /// <summary>The longest <c>.url</c> file read: they are a few hundred bytes; anything bigger is not one.</summary>
    public const int MaxUrlFileLength = 16 * 1024;

    /// <summary>
    /// Read an Internet shortcut: the <c>URL=</c> and <c>IconFile=</c> of its
    /// <c>[InternetShortcut]</c> section only, keys in any case, values trimmed; blank values are
    /// absent.
    /// </summary>
    public static InternetShortcut ParseUrlFile(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return default;
        }

        string? url = null;
        string? icon = null;
        var inSection = false;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimEnd('\r');

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inSection = string.Equals(line, "[InternetShortcut]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSection)
            {
                continue;
            }

            var eq = line.IndexOf('=', StringComparison.Ordinal);

            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();

            if (value.Length == 0)
            {
                continue;
            }

            if (string.Equals(key, "URL", StringComparison.OrdinalIgnoreCase))
            {
                url ??= value;
            }
            else if (string.Equals(key, "IconFile", StringComparison.OrdinalIgnoreCase))
            {
                icon ??= value;
            }
        }

        return new InternetShortcut(url, icon);
    }

    /// <summary>
    /// [S-9508] What a drop makes the shortcut: the first of the dropped things the rule accepts, or null
    /// when it accepts none. Files win: the URLs are consulted only when no path came with the drop.
    /// </summary>
    /// <param name="paths">Dropped file-system paths.</param>
    /// <param name="urls">Dropped web addresses.</param>
    /// <param name="resolveLink">A <c>.lnk</c>'s target, or null to keep the <c>.lnk</c> itself (it failed, or it carries arguments or an icon the target alone would lose).</param>
    /// <param name="readText">A <c>.url</c> file's text, or null when it cannot be read.</param>
    public static DroppedTarget? FromDrop(
        IReadOnlyList<string> paths,
        IReadOnlyList<string> urls,
        Func<string, string?> resolveLink,
        Func<string, string?> readText)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(urls);
        ArgumentNullException.ThrowIfNull(resolveLink);
        ArgumentNullException.ThrowIfNull(readText);

        if (paths.Count > 0)
        {
            return paths.Select(path => FromPath(path, resolveLink, readText)).FirstOrDefault(dropped => dropped is not null);
        }

        return urls
            .Select(url => LaunchTargets.Classify(url) is { Kind: LaunchKind.Web } web ? new DroppedTarget(web.Value) : null)
            .FirstOrDefault(dropped => dropped is not null);
    }

    /// <summary>
    /// [S-9508] A Start menu app dropped as a shell item (no path): the Start menu's own shortcut of that
    /// name when there is one, else a packaged app's Apps-folder item
    /// (<c>shell:AppsFolder\&lt;AUMID&gt;</c>, from the item's parsing name) named as Start names it. The
    /// first of <paramref name="apps"/> that is either; null when none is.
    /// </summary>
    /// <param name="apps">The dropped items: the name Start shows, and the shell's parsing name.</param>
    /// <param name="shortcutOf">The Start menu shortcut of that name, or null.</param>
    /// <param name="resolveLink">As in <see cref="FromDrop"/>.</param>
    /// <param name="readText">As in <see cref="FromDrop"/>.</param>
    public static DroppedTarget? FromStartMenu(
        IReadOnlyList<(string Name, string ParsingName)> apps,
        Func<string, string?> shortcutOf,
        Func<string, string?> resolveLink,
        Func<string, string?> readText)
    {
        ArgumentNullException.ThrowIfNull(apps);
        ArgumentNullException.ThrowIfNull(shortcutOf);

        foreach (var (name, parsingName) in apps)
        {
            var dropped = shortcutOf(name) is { Length: > 0 } shortcut
                ? FromPath(shortcut, resolveLink, readText)
                : LaunchTargets.AppsFolderTarget(parsingName) is { } packaged
                    ? new DroppedTarget(packaged, name)
                    : null;

            if (dropped is not null)
            {
                return dropped;
            }
        }

        return null;
    }

    /// <summary>[S-9508] One dropped path as a shortcut, or null when the rule refuses it.</summary>
    public static DroppedTarget? FromPath(string path, Func<string, string?> resolveLink, Func<string, string?> readText)
    {
        ArgumentNullException.ThrowIfNull(resolveLink);
        ArgumentNullException.ThrowIfNull(readText);

        if (LaunchTargets.Classify(path) is not { Kind: LaunchKind.LocalPath } local)
        {
            return null;
        }

        var file = local.Value;
        var stem = Path.GetFileNameWithoutExtension(file.TrimEnd('\\'));
        var extension = Path.GetExtension(file);

        if (extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
        {
            // The file's own name is the label: a custom-protocol target has no good name of its own.
            var shortcut = ParseUrlFile(readText(file));

            if (LaunchTargets.Classify(shortcut.Url) is { Kind: LaunchKind.Web or LaunchKind.Settings } web)
            {
                return new DroppedTarget(web.Value, stem);
            }

            return new DroppedTarget(file, stem,
                shortcut.Url is not null && LaunchTargets.Classify(shortcut.IconFile) is { Kind: LaunchKind.LocalPath } icon ? icon.Value : null);
        }

        if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            // A link is kept by what it points at, named as the link was ("Visual Studio Code", not
            // "Code"). Unresolvable, or a target the rule refuses: the link itself.
            if (resolveLink(file) is { } resolved && LaunchTargets.Classify(resolved) is { Kind: LaunchKind.LocalPath } target)
            {
                return new DroppedTarget(target.Value, stem);
            }

            return new DroppedTarget(file, stem);
        }

        return new DroppedTarget(file);
    }
}
