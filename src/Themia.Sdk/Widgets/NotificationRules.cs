namespace Themia.Core.Widgets;

/// <summary>[S-4401] One notification from Windows' list, as the widget shows it.</summary>
/// <param name="Id">Windows' id (<c>UserNotification.Id</c>): what dismissing names.</param>
/// <param name="AppId">The sender's AppUserModelID; empty where Windows gave none.</param>
/// <param name="AppName">The sender's name as Windows gave it; empty where it gave none.</param>
/// <param name="Title">The toast's first line.</param>
/// <param name="Body">The rest of its text, lines kept.</param>
/// <param name="Arrived">When Windows took it.</param>
public sealed record NotificationItem(uint Id, string AppId, string AppName, string Title, string Body, DateTimeOffset Arrived);

/// <summary>[S-4402] One app's notifications, newest first.</summary>
public sealed record NotificationGroup(string AppId, string AppName, IReadOnlyList<NotificationItem> Items)
{
    public DateTimeOffset Newest => Items.Count == 0 ? DateTimeOffset.MinValue : Items[0].Arrived;
}

/// <summary>[S-4403] Whether Windows lets Themia read its notification list.</summary>
public enum NotificationAccess
{
    /// <summary>Not asked yet: nothing is drawn but the frame.</summary>
    Pending = 0,

    Allowed = 1,

    /// <summary>Turned off for Themia (or for every app) on Windows' privacy page.</summary>
    Denied = 2,

    /// <summary>Never decided: a packaged Themia may ask once, with Windows' own prompt.</summary>
    Unspecified = 3,

    /// <summary>This Windows has no listener (Windows 10 before 1607, or Windows answered nothing).</summary>
    Unsupported = 4,
}

/// <summary>[S-4404] What a notifications widget shows at a size.</summary>
/// <param name="Face">The smallest tile: the bell and how many, nothing else.</param>
/// <param name="Grouped">Under app headings (icon, name, clear) rather than flat with an icon per row.</param>
/// <param name="BodyLines">Lines of each notification's body under its title (0: the title alone).</param>
/// <param name="Folded">Notifications an app shows before "See more" when grouped.</param>
public readonly record struct NotificationPlan(bool Face, bool Grouped, int BodyLines, int Folded);

/// <summary>
/// [S-4400] The notification list's rules: grouping, the words a toast carries, the sender's name when
/// Windows gives none, the ladder, and which notifications are still shown while Windows catches up
/// with a dismissal. Pure, so they are tested without a notification.
/// </summary>
public static class NotificationRules
{
    /// <summary>[S-4402] An app shows its newest three before "See more (n)", as Windows' notification center.</summary>
    public const int FoldAt = 3;

    /// <summary>The longest title and body kept: a toast is a few lines, never a document.</summary>
    public const int MostTitle = 200;

    public const int MostBody = 600;

    /// <summary>The most notifications read: Windows' own center keeps about this many.</summary>
    public const int MostItems = 100;

    /// <summary>
    /// [S-4402] By app, the apps in the order of their newest, each app's newest first. One app is one
    /// AUMID, compared without case, as Windows compares them.
    /// </summary>
    public static IReadOnlyList<NotificationGroup> Groups(IEnumerable<NotificationItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return [.. items
            .GroupBy(item => item.AppId, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var sorted = Newest(group);
                var named = sorted.FirstOrDefault(item => item.AppName.Length > 0)?.AppName;
                return new NotificationGroup(sorted[0].AppId, named ?? FallbackName(sorted[0].AppId), sorted);
            })
            .OrderByDescending(group => group.Newest)
            .ThenBy(group => group.AppId, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Newest first; the higher id first between two of the same moment (Windows' ids grow).</summary>
    public static IReadOnlyList<NotificationItem> Newest(IEnumerable<NotificationItem> items)
        => [.. items.OrderByDescending(item => item.Arrived).ThenByDescending(item => item.Id)];

    /// <summary>
    /// [S-4401] A toast's words: the first non-blank text is the title, the rest the body, one per line.
    /// Each is trimmed and cut to <see cref="MostTitle"/>/<see cref="MostBody"/>; control characters go.
    /// </summary>
    public static (string Title, string Body) Text(IEnumerable<string?> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var lines = texts.Select(Clean).Where(text => text.Length > 0).ToList();
        if (lines.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        return (Cut(lines[0], MostTitle), Cut(string.Join('\n', lines.Skip(1)), MostBody));
    }

    /// <summary>
    /// [S-4405] A name for a sender Windows named nothing (Karpo's <c>NotificationText.FallbackName</c>):
    /// "Windows" for Windows' own, else the last real word of a dotted or packaged id, else a program's
    /// file name.
    /// </summary>
    public static string FallbackName(string? appId)
    {
        var id = (appId ?? string.Empty).Trim();
        if (id.Length == 0)
        {
            return "Windows";
        }

        if (id.StartsWith("Windows.", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("Microsoft.Windows.", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows";
        }

        // A program's path (a desktop app's AUMID may be one): its file name.
        var slash = id.LastIndexOfAny(['\\', '/']);
        if (slash >= 0 && slash < id.Length - 1)
        {
            var file = id[(slash + 1)..];
            var dot = file.LastIndexOf('.');
            return dot > 0 ? file[..dot] : file;
        }

        // A package's AUMID, "Family_hash!App": the family's last real word.
        var bang = id.IndexOf('!', StringComparison.Ordinal);
        var family = bang > 0 ? id[..bang] : id;
        var underscore = family.IndexOf('_', StringComparison.Ordinal);
        if (underscore > 0)
        {
            family = family[..underscore];
        }

        var words = family.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = words.Length - 1; i >= 0; i--)
        {
            if (IsRealWord(words[i]))
            {
                return words[i];
            }
        }

        return family.Length > 0 ? family : "Windows";
    }

    /// <summary>
    /// [S-4406] What opens the sender: its Apps-folder item, which starts a packaged app and a Start
    /// menu desktop app alike. Null for an id that could carry anything a shell could read as syntax.
    /// </summary>
    public static string? AppTarget(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId) || appId.Length > 512 || appId.Contains("..", StringComparison.Ordinal)
            || appId[0] is '\\' or '/')
        {
            return null;
        }

        foreach (var c in appId)
        {
            var allowed = char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '!' or '{' or '}' or '\\' or ' ' or ':';
            if (!allowed)
            {
                return null;
            }
        }

        return PackagedAppsFolder + appId;
    }

    /// <summary>[S-4404] Below two compact rows of height the list is the bell and the count.</summary>
    public const double FaceBelow = 64;

    /// <summary>The Apps folder of the shell, where every app with an AUMID is an item.</summary>
    public const string PackagedAppsFolder = @"shell:AppsFolder\";

    /// <summary>
    /// [S-4404] The ladder: the bell and a count under 180 wide or two rows high; then the newest flat,
    /// titles alone while the height is compact, with each row's app icon; from a medium height grouped under app headings; the body gets a
    /// line from a small height, two from medium, three from large. Costs are line heights.
    /// </summary>
    public static NotificationPlan Plan(WidgetArea area)
    {
        if (!area.IsMeasured)
        {
            return new NotificationPlan(Face: false, Grouped: false, BodyLines: 0, Folded: FoldAt);
        }

        if (area.WidthClass == SizeClass.Compact || area.Height < FaceBelow)
        {
            // Under 180 wide a title does not fit beside a time; under two rows high there is no list.
            return new NotificationPlan(Face: true, Grouped: false, BodyLines: 0, Folded: FoldAt);
        }

        var bodyLines = area.HeightClass switch
        {
            SizeClass.Compact => 0,
            SizeClass.Regular => 1,
            SizeClass.Expanded => 2,
            _ => 3,
        };

        return new NotificationPlan(Face: false, Grouped: area.HeightClass >= SizeClass.Expanded, BodyLines: bodyLines, Folded: FoldAt);
    }

    /// <summary>
    /// [S-4407] What is shown: Windows' list less what was dismissed here and not yet gone from it. A
    /// dismissal shows at once; the hidden set forgets an id once Windows' list no longer has it, so it
    /// never grows and never hides a later notification.
    /// </summary>
    public static (IReadOnlyList<NotificationItem> Shown, HashSet<uint> StillHidden) Visible(
        IReadOnlyList<NotificationItem> fromWindows, IReadOnlySet<uint> hidden)
    {
        ArgumentNullException.ThrowIfNull(fromWindows);
        ArgumentNullException.ThrowIfNull(hidden);

        var present = fromWindows.Select(item => item.Id).ToHashSet();
        var still = hidden.Where(present.Contains).ToHashSet();
        return ([.. fromWindows.Where(item => !still.Contains(item.Id))], still);
    }

    /// <summary>
    /// [S-4402] The rows a group shows: all of it while open or short, else the newest
    /// <see cref="FoldAt"/>, and how many more "See more" opens.
    /// </summary>
    public static (IReadOnlyList<NotificationItem> Rows, int More) Fold(NotificationGroup group, bool open, int foldAt = FoldAt)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (open || group.Items.Count <= foldAt)
        {
            return (group.Items, 0);
        }

        return ([.. group.Items.Take(foldAt)], group.Items.Count - foldAt);
    }

    private static bool IsRealWord(string word)
        => word.Length > 1
            && !word.All(char.IsAsciiDigit)
            && !string.Equals(word, "App", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(word, "Desktop", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(word, "Exe", StringComparison.OrdinalIgnoreCase);

    private static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        Span<char> buffer = text.Length <= 1024 ? stackalloc char[text.Length] : new char[text.Length];
        var length = 0;
        foreach (var c in text)
        {
            if (c == '\n' || !char.IsControl(c))
            {
                buffer[length++] = c == '\t' ? ' ' : c;
            }
            else if (c == '\r')
            {
                continue;
            }
        }

        return new string(buffer[..length]).Trim();
    }

    private static string Cut(string text, int most) => text.Length <= most ? text : string.Concat(text.AsSpan(0, most - 1).TrimEnd(), "…");
}
