using System.Text.Json.Serialization;

namespace Themia.Sdk;

/// <summary>[S-13004] A kind of file a fence's rule takes, by Explorer's own groups (the extensions in <see cref="FenceRules"/>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FenceFileType>))]
public enum FenceFileType
{
    Images,
    Documents,
    Installers,
    Archives,
    Videos,
    Music,

    /// <summary>A shortcut (<c>.lnk</c>, <c>.url</c>): the item's kind, never its target's.</summary>
    Shortcuts,

    /// <summary>A folder on disk.</summary>
    Folders,
}

/// <summary>
/// [S-13004] A fence's rules, kept small as Fences' own: <b>New items of type…</b> (<see cref="Types"/> and the
/// user's own <see cref="Extensions"/>) and <b>Name contains…</b> (<see cref="NameContains"/>). A new desktop
/// item that matches any of them arrives in the fence instead of on the free desktop. Stored in the fence's config.
/// </summary>
public sealed class FenceRuleSet
{
    /// <summary>The kinds of file the fence takes; empty for none.</summary>
    public List<FenceFileType> Types { get; set; } = [];

    /// <summary>The user's own extensions, each <c>.ext</c> in lower case (<see cref="FenceRules.Extension"/>); empty for none.</summary>
    public List<string> Extensions { get; set; } = [];

    /// <summary>Text a new item's name holds (case aside) to come here; empty for none.</summary>
    public string NameContains { get; set; } = string.Empty;

    /// <summary>Whether any rule is set.</summary>
    [JsonIgnore]
    public bool Any => Types.Count > 0 || Extensions.Count > 0 || NameContains.Length > 0;

    public FenceRuleSet Clone() => new() { Types = [.. Types], Extensions = [.. Extensions], NameContains = NameContains };
}

/// <summary>[S-13004] The fence rules as pure functions: what matches, and a stored set made whole.</summary>
public static class FenceRules
{
    /// <summary>The longest name text a rule keeps.</summary>
    public const int MaxText = 100;

    /// <summary>How many of the user's own extensions a rule keeps.</summary>
    public const int MaxExtensions = 32;

    /// <summary>The longest extension, its dot included.</summary>
    public const int MaxExtension = 16;

    private static readonly Dictionary<FenceFileType, string[]> Groups = new()
    {
        [FenceFileType.Images] = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic", ".heif", ".tif", ".tiff", ".svg", ".ico", ".avif", ".raw", ".psd"],
        [FenceFileType.Documents] = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf", ".odt", ".ods", ".odp", ".md", ".csv", ".one", ".xps"],
        [FenceFileType.Installers] = [".exe", ".msi", ".msix", ".msixbundle", ".appx", ".appxbundle", ".msp"],
        [FenceFileType.Archives] = [".zip", ".7z", ".rar", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".cab", ".iso"],
        [FenceFileType.Videos] = [".mp4", ".mkv", ".mov", ".avi", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg"],
        [FenceFileType.Music] = [".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma", ".opus", ".mid", ".midi"],
    };

    /// <summary>The extensions of <paramref name="type"/>, lower case with their dot; none for Shortcuts and Folders (matched by kind).</summary>
    public static IReadOnlyList<string> ExtensionsOf(FenceFileType type) => Groups.TryGetValue(type, out var list) ? list : [];

    /// <summary>
    /// [S-13004] Whether <paramref name="item"/> matches <paramref name="rules"/>: a folder for Folders, a shortcut for
    /// Shortcuts, a file whose extension is one of a chosen type's or the user's own, or a name holding the text (case
    /// aside). A shell item (This PC, the Recycle Bin) matches nothing; a shortcut matches by its kind and its name,
    /// never by the <c>.lnk</c> it is stored as.
    /// </summary>
    public static bool Matches(FenceRuleSet? rules, DesktopItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (rules is null || !rules.Any || item.Kind == DesktopItemKind.ShellItem)
        {
            return false;
        }

        if (rules.NameContains.Length > 0 && item.Name.Contains(rules.NameContains, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        switch (item.Kind)
        {
            case DesktopItemKind.Folder:
                return rules.Types.Contains(FenceFileType.Folders);
            case DesktopItemKind.Shortcut:
                return rules.Types.Contains(FenceFileType.Shortcuts);
        }

        var extension = ExtensionOfKey(item.Key);
        if (extension.Length == 0)
        {
            return false;
        }

        return rules.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            || rules.Types.Any(type => ExtensionsOf(type).Contains(extension, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>A key's extension, lower case with its dot, or empty (a folder, a name with none, a shell item).</summary>
    public static string ExtensionOfKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var name = key.TrimEnd('\\', '/');
        var slash = name.LastIndexOfAny(['\\', '/']);
        name = slash >= 0 ? name[(slash + 1)..] : name;
        var dot = name.LastIndexOf('.');
        return dot > 0 && dot < name.Length - 1 ? name[dot..].ToLowerInvariant() : string.Empty;
    }

    /// <summary>
    /// [S-13004] What the user typed as an extension, made one: <c>"PDF"</c>, <c>"*.pdf"</c> and <c>".pdf"</c> are
    /// <c>".pdf"</c>; null for one Windows would refuse in a file name, one with a dot inside, or one too long.
    /// </summary>
    public static string? Extension(string? typed)
    {
        var text = (typed ?? string.Empty).Trim().TrimStart('*').Trim();
        if (text.StartsWith('.'))
        {
            text = text[1..];
        }

        if (text.Length == 0 || text.Length + 1 > MaxExtension || text.Contains('.')
            || text.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || "\\/:*?\"<>|".Contains(c)))
        {
            return null;
        }

        return "." + text.ToLowerInvariant();
    }

    /// <summary>The user's typed list ("pdf, docx; *.txt") as extensions, each once, in order; what is not one is left out.</summary>
    public static List<string> ParseExtensions(string? typed)
    {
        var list = new List<string>();
        foreach (var part in (typed ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            if (Extension(part) is { } one && !list.Contains(one, StringComparer.OrdinalIgnoreCase) && list.Count < MaxExtensions)
            {
                list.Add(one);
            }
        }

        return list;
    }

    /// <summary>
    /// [S-13002] A stored rule set made whole: a null set or list is empty, an unknown type or one listed twice goes, an
    /// extension is made one or goes (at most <see cref="MaxExtensions"/>), the name text is trimmed, its control
    /// characters go and it is cut at <see cref="MaxText"/>. Each repair is said.
    /// </summary>
    public static IReadOnlyList<string> Repair(FenceRuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var repairs = new List<string>();
        rules.Types ??= [];
        rules.Extensions ??= [];
        rules.NameContains ??= string.Empty;

        var types = rules.Types.Where(Enum.IsDefined).Distinct().ToList();
        if (types.Count != rules.Types.Count)
        {
            repairs.Add($"a fence's rule listed {rules.Types.Count - types.Count} unknown or repeated file type(s); dropped");
            rules.Types = types;
        }

        var extensions = new List<string>();
        foreach (var typed in rules.Extensions)
        {
            if (Extension(typed) is { } one && !extensions.Contains(one, StringComparer.OrdinalIgnoreCase) && extensions.Count < MaxExtensions)
            {
                extensions.Add(one);
            }
        }

        if (!extensions.SequenceEqual(rules.Extensions, StringComparer.Ordinal))
        {
            repairs.Add("a fence's rule held extensions that were not one, repeated or too many; made whole");
            rules.Extensions = extensions;
        }

        var text = new string([.. rules.NameContains.Where(c => !char.IsControl(c))]).Trim();
        if (text.Length > MaxText)
        {
            text = text[..MaxText];
        }

        if (!string.Equals(text, rules.NameContains, StringComparison.Ordinal))
        {
            repairs.Add("a fence's name rule was trimmed");
            rules.NameContains = text;
        }

        return repairs;
    }
}
