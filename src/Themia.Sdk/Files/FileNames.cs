using Themia.Core.Localization;

namespace Themia.Core.Files;

/// <summary>Why a name cannot be a Windows file name.</summary>
public enum FileNameProblem
{
    None = 0,
    Empty,
    DotSegment,
    InvalidCharacter,
    Reserved,
    TrailingDotOrSpace,
    TooLong,
}

/// <summary>
/// [S-1331] File-name rules for rename, new folder and new file: what Windows accepts, how a name is
/// shown with its extension hidden, and the next free "New folder (2)" style name. Pure.
/// </summary>
public static class FileNames
{
    /// <summary>The longest single name component NTFS stores.</summary>
    public const int MaxLength = 255;

    private static readonly char[] Invalid = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> Devices = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Whether <paramref name="name"/> can be one path component, and if not why.</summary>
    public static FileNameProblem Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return FileNameProblem.Empty;
        }

        if (name is "." or "..")
        {
            return FileNameProblem.DotSegment;
        }

        if (name.Length > MaxLength)
        {
            return FileNameProblem.TooLong;
        }

        foreach (var c in name)
        {
            if (c < 32 || Array.IndexOf(Invalid, c) >= 0)
            {
                return FileNameProblem.InvalidCharacter;
            }
        }

        if (name.EndsWith('.') || name.EndsWith(' '))
        {
            return FileNameProblem.TrailingDotOrSpace;
        }

        // "CON", and "CON.txt" too: Windows reserves the device name whatever follows the dot.
        var stem = name.Split('.')[0].TrimEnd();
        return Devices.Contains(stem) ? FileNameProblem.Reserved : FileNameProblem.None;
    }

    /// <summary>The sentence shown under a rename box, in <paramref name="locale"/> (default: the app's language).</summary>
    public static string Message(FileNameProblem problem, Locale? locale = null)
    {
        Str? id = problem switch
        {
            FileNameProblem.Empty => Str.WidgetsB_Folder_NameProblem_Empty,
            FileNameProblem.DotSegment => Str.WidgetsB_Folder_NameProblem_DotSegment,
            FileNameProblem.InvalidCharacter => Str.WidgetsB_Folder_NameProblem_InvalidCharacter,
            FileNameProblem.Reserved => Str.WidgetsB_Folder_NameProblem_Reserved,
            FileNameProblem.TrailingDotOrSpace => Str.WidgetsB_Folder_NameProblem_TrailingDotOrSpace,
            FileNameProblem.TooLong => Str.WidgetsB_Folder_NameProblem_TooLong,
            _ => null,
        };

        return id is { } key ? Strings.Get(locale ?? Strings.Current, key) : string.Empty;
    }

    /// <summary>
    /// [S-1321] How an item's name is shown: folders whole; files without the extension unless
    /// <paramref name="showExtension"/>. A dot-file (".gitignore") keeps its name: its "extension" is
    /// all of it.
    /// </summary>
    public static string Display(string name, bool isDirectory, bool showExtension)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (isDirectory || showExtension)
        {
            return name;
        }

        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    /// <summary>
    /// [S-1331] The full name a rename box's text stands for: with extensions hidden the box edits the
    /// stem and the original extension is put back, as Explorer does.
    /// </summary>
    public static string FromEdited(string original, string edited, bool isDirectory, bool showExtension)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(edited);

        var trimmed = edited.Trim();

        if (isDirectory || showExtension)
        {
            return trimmed;
        }

        var dot = original.LastIndexOf('.');
        return dot > 0 ? trimmed + original[dot..] : trimmed;
    }

    /// <summary>
    /// [S-1331] <paramref name="wanted"/>, or the first "stem (n).ext" not in <paramref name="taken"/>,
    /// the way Explorer names a second "New folder".
    /// </summary>
    public static string Unique(string wanted, IEnumerable<string> taken)
    {
        ArgumentNullException.ThrowIfNull(wanted);
        ArgumentNullException.ThrowIfNull(taken);

        var set = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);

        if (!set.Contains(wanted))
        {
            return wanted;
        }

        var dot = wanted.LastIndexOf('.');
        var stem = dot > 0 ? wanted[..dot] : wanted;
        var extension = dot > 0 ? wanted[dot..] : string.Empty;

        for (var n = 2; ; n++)
        {
            var candidate = $"{stem} ({n}){extension}";

            if (!set.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
