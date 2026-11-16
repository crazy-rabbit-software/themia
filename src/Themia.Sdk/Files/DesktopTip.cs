using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Sdk;

/// <summary>
/// [S-5610] What the shell says of a desktop item beyond its name, for its tip: its type's name as
/// Explorer writes it ("Text Document"), a file's size, when it was last written and made (local time),
/// and where a shortcut file points. Anything not known is null.
/// </summary>
public readonly record struct DesktopItemFacts(string? Type, long? Size, DateTime? Modified, DateTime? Created, string? LinkTarget = null);

/// <summary>
/// [S-5610] The tip a desktop icon shows under the pointer, as Explorer's desktop: only where there is
/// something to say. A file says its type, its size and when it was modified; a folder when it was
/// made; a shortcut where it points ("Location: …"), by the target's own name and never a raw
/// <c>shell:</c> or <c>ms-settings:</c> string. A name cut short under the icon leads the tip in full.
/// With the name shown whole and nothing more to say there is no tip at all (null). Pure.
/// </summary>
public static class DesktopTip
{
    /// <summary>
    /// The tip of a desktop item: <paramref name="facts"/> as the host read them (null: not read, or a
    /// shell item, which is no file), <paramref name="nameCut"/> whether its name is cut short as drawn.
    /// </summary>
    public static string? Item(Locale locale, DesktopItem item, DesktopItemFacts? facts, bool nameCut)
    {
        ArgumentNullException.ThrowIfNull(item);
        var lines = new List<string>();
        if (nameCut)
        {
            lines.Add(item.Name);
        }

        if (facts is { } known && item.Kind != DesktopItemKind.ShellItem)
        {
            if (item.Kind == DesktopItemKind.Shortcut && Place(locale, LaunchTargets.Classify(known.LinkTarget)) is { } place)
            {
                // Explorer's tip for a shortcut says where it points, nothing of the link file itself.
                lines.Add(Strings.Format(locale, Str.Desktop_Tip_Location, ("place", place)));
            }
            else if (item.Kind == DesktopItemKind.Folder)
            {
                if (known.Created is { } made)
                {
                    lines.Add(Strings.Format(locale, Str.Desktop_Tip_Created, ("date", Stamp(locale, made))));
                }
            }
            else
            {
                if (known.Type is { Length: > 0 } type)
                {
                    lines.Add(Strings.Format(locale, Str.Desktop_Tip_Type, ("type", type)));
                }

                if (known.Size is { } size and >= 0)
                {
                    lines.Add(Strings.Format(locale, Str.Desktop_Tip_Size, ("size", Formatting.Bytes(locale, size))));
                }

                if (known.Modified is { } written)
                {
                    lines.Add(Strings.Format(locale, Str.Desktop_Tip_Modified, ("date", Stamp(locale, written))));
                }
            }
        }

        return lines.Count == 0 ? null : string.Join('\n', lines);
    }

    /// <summary>
    /// The tip of an own icon drawn as <paramref name="name"/> that points at <paramref name="target"/>:
    /// "Location: " and the target's place (<see cref="Place"/>), left out where that is the name
    /// already under the icon (an icon on Documents called Documents has nothing more to say).
    /// </summary>
    public static string? Own(Locale locale, string name, LaunchTarget target, bool nameCut)
    {
        ArgumentNullException.ThrowIfNull(name);
        var lines = new List<string>();
        if (nameCut)
        {
            lines.Add(name);
        }

        if (Place(locale, target) is { } place && !string.Equals(place, name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            lines.Add(Strings.Format(locale, Str.Desktop_Tip_Location, ("place", place)));
        }

        return lines.Count == 0 ? null : string.Join('\n', lines);
    }

    /// <summary>
    /// Where a target is, in words a person reads: a local path as Explorer writes a shortcut's
    /// location, "name (folder)"; a web address as it is; a Windows location and a Settings page by
    /// their names (never the <c>shell:</c> or <c>ms-settings:</c> string: one with no name of its own
    /// says nothing). Null for a refused target and for nothing to say.
    /// </summary>
    public static string? Place(Locale locale, LaunchTarget target)
    {
        switch (target.Kind)
        {
            case LaunchKind.Web:
                return target.Value;
            case LaunchKind.LocalPath:
                var path = target.Value.TrimEnd('\\', '/');
                var cut = path.LastIndexOfAny(['\\', '/']);
                return cut <= 0 || cut >= path.Length - 1
                    ? target.Value
                    : $"{path[(cut + 1)..]} ({(path[..cut].EndsWith(':') ? path[..(cut + 1)] : path[..cut])})";
            case LaunchKind.ShellNamespace:
                return LaunchTargets.ShellLabel(target.Value, locale);
            case LaunchKind.Settings:
                return LaunchTargets.SettingsDisplayName(target.Value, locale);
            default:
                return null;
        }
    }

    /// <summary>A date and its time of day, as the user's formats write them.</summary>
    private static string Stamp(Locale locale, DateTime local)
        => $"{LocaleFormat.Date(locale, local, DateStyle.Short)} {LocaleFormat.Time(locale, local)}";
}
