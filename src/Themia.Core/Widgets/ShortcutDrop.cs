using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>What an Internet shortcut (<c>.url</c>) file says: its <c>URL=</c> and <c>IconFile=</c>.</summary>
public readonly record struct InternetShortcut(string? Url, string? IconFile);

/// <summary>What a drop added, and how many of the things dropped were not taken (refused or already there).</summary>
public sealed record ShortcutDropResult(IReadOnlyList<ShortcutItem> Items, int Skipped);

/// <summary>
/// [S-1400]..[S-1402] What a drop onto the Shortcuts widget becomes (original S-28..S-30, S-34,
/// S-73): one item per dropped file or folder, a <c>.lnk</c> by what it points at, a <c>.url</c>
/// by its address, and a browser's link by its URL when no files came with it. Pure: reading the
/// files and resolving the links are the caller's, passed in.
/// <para>
/// Nothing dropped is trusted: every target goes through <see cref="LaunchTargets.Classify"/>, and
/// one the rule refuses is skipped (counted) rather than added. A <c>.url</c> pointing at a scheme
/// the rule does not open (<c>steam://</c>) is kept as the <c>.url</c> <i>file</i> — a local path,
/// which the shell opens through the protocol's own handler — rather than widening the rule.
/// </para>
/// </summary>
public static class ShortcutDrop
{
    /// <summary>The longest <c>.url</c> file read: they are a few hundred bytes; anything bigger is not one.</summary>
    public const int MaxUrlFileLength = 16 * 1024;

    /// <summary>
    /// [S-1401] Read an Internet shortcut (original S-29): the <c>URL=</c> and <c>IconFile=</c> of its
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
    /// [S-1400] The items a drop adds. Files win (original S-34): the URLs are consulted only when
    /// no path came with the drop. A target the widget already holds, or that appears twice in one
    /// drop, is added once.
    /// </summary>
    /// <param name="paths">Dropped file-system paths.</param>
    /// <param name="urls">Dropped web addresses.</param>
    /// <param name="existing">What the widget holds now.</param>
    /// <param name="resolveLink">A <c>.lnk</c>'s target, or null to keep the <c>.lnk</c> itself (it failed, or it carries arguments the target alone would lose).</param>
    /// <param name="readText">A <c>.url</c> file's text, or null when it cannot be read.</param>
    public static ShortcutDropResult FromDrop(
        IReadOnlyList<string> paths,
        IReadOnlyList<string> urls,
        IEnumerable<ShortcutItem> existing,
        Func<string, string?> resolveLink,
        Func<string, string?> readText)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(urls);
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(resolveLink);
        ArgumentNullException.ThrowIfNull(readText);

        var held = existing.Select(item => item.Target).ToList();
        var added = new List<ShortcutItem>();
        var skipped = 0;

        void Take(ShortcutItem? item)
        {
            if (item is null || held.Any(target => LaunchTargets.SameTarget(target, item.Target)))
            {
                skipped++;
                return;
            }

            held.Add(item.Target);
            added.Add(item);
        }

        if (paths.Count > 0)
        {
            foreach (var path in paths)
            {
                Take(FromPath(path, resolveLink, readText));
            }
        }
        else
        {
            foreach (var url in urls)
            {
                Take(LaunchTargets.Classify(url) is { Kind: LaunchKind.Web } web ? ShortcutItem.Create(web.Value) : null);
            }
        }

        return new ShortcutDropResult(added, skipped);
    }

    /// <summary>[S-1401] One dropped path as an item, or null when the rule refuses it.</summary>
    public static ShortcutItem? FromPath(string path, Func<string, string?> resolveLink, Func<string, string?> readText)
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
            // [S-1401] The file's own name is the label: a custom-protocol target has no good name of
            // its own (original S-29).
            var shortcut = ParseUrlFile(readText(file));

            if (LaunchTargets.Classify(shortcut.Url) is { Kind: LaunchKind.Web } web)
            {
                return ShortcutItem.Create(web.Value, stem);
            }

            var item = ShortcutItem.Create(file, stem);

            if (shortcut.Url is not null && LaunchTargets.Classify(shortcut.IconFile) is { Kind: LaunchKind.LocalPath } icon)
            {
                item.IconPath = icon.Value;
            }

            return item;
        }

        if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            // [S-1401] A link is kept by what it points at (original S-30), named as the link was —
            // "Visual Studio Code", not "Code". Unresolvable, or a target the rule refuses: the link.
            if (resolveLink(file) is { } resolved && LaunchTargets.Classify(resolved) is { Kind: LaunchKind.LocalPath } target)
            {
                return ShortcutItem.Create(target.Value, stem);
            }

            return ShortcutItem.Create(file, stem);
        }

        return ShortcutItem.Create(file);
    }

    /// <summary>
    /// [S-1410] Move <paramref name="ids"/> (in their current order) to just before
    /// <paramref name="beforeId"/>, or to the end when it is null (original S-37: over a row, before
    /// it; over empty space, at the end). "Before a dragged item" means before the first item after
    /// it that is not being dragged. Returns whether the order changed — a drop that changes nothing
    /// (over the dragged item itself) writes nothing.
    /// </summary>
    public static bool Reorder(List<ShortcutItem> items, IReadOnlyCollection<string> ids, string? beforeId)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return false;
        }

        if (beforeId is not null)
        {
            var from = items.FindIndex(item => item.Id == beforeId);

            if (from < 0)
            {
                return false;
            }

            beforeId = items.Skip(from).FirstOrDefault(item => !ids.Contains(item.Id))?.Id;
        }

        var moving = items.Where(item => ids.Contains(item.Id)).ToList();

        if (moving.Count == 0)
        {
            return false;
        }

        var rest = items.Where(item => !ids.Contains(item.Id)).ToList();
        var at = beforeId is null ? rest.Count : rest.FindIndex(item => item.Id == beforeId);
        rest.InsertRange(at, moving);

        if (rest.Select(item => item.Id).SequenceEqual(items.Select(item => item.Id), StringComparer.Ordinal))
        {
            return false;
        }

        items.Clear();
        items.AddRange(rest);
        return true;
    }

    /// <summary>
    /// [S-1410] Where a drag over the item at <paramref name="index"/> lands: before it when the
    /// pointer is in its first half (<paramref name="fraction"/> &lt; 0.5 along the reading
    /// direction), else before the next one (null past the last). <paramref name="order"/> is the
    /// visible order; <paramref name="index"/> -1 is empty space (the end).
    /// </summary>
    public static string? InsertBefore(IReadOnlyList<string> order, int index, double fraction)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (index < 0 || index >= order.Count)
        {
            return null;
        }

        var at = fraction < 0.5 ? index : index + 1;
        return at < order.Count ? order[at] : null;
    }
}
