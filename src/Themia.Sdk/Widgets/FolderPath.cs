namespace Themia.Core.Widgets;

/// <summary>Why a configured folder path was refused.</summary>
public enum FolderPathProblem
{
    /// <summary>Nothing wrong with it.</summary>
    None = 0,

    /// <summary>Nothing was given, and there was no fallback to stand in for it.</summary>
    Empty = 1,

    /// <summary>It contains a character a Windows path cannot contain.</summary>
    InvalidCharacters = 2,

    /// <summary>It is relative, or drive-relative, so it names a different folder depending on where it is read from.</summary>
    NotRooted = 3,

    /// <summary>It is longer than any Windows path can be.</summary>
    TooLong = 4,
}

/// <summary>The outcome of checking a configured path.</summary>
/// <param name="Problem">What is wrong with it, or <see cref="FolderPathProblem.None"/>.</param>
/// <param name="Path">The path to use. Empty whenever <paramref name="Problem"/> is not None.</param>
/// <param name="UsedFallback">True when the configured value was blank and the fallback stood in.</param>
public readonly record struct FolderPathResult(FolderPathProblem Problem, string Path, bool UsedFallback)
{
    public bool IsValid => Problem == FolderPathProblem.None;
}

/// <summary>
/// The trust boundary in front of the filesystem call.
/// <para>
/// The folder path arrives from two places that deserve the same suspicion: a text box, and the
/// settings JSON in the user's profile, which anyone can hand-edit. So it is checked here, as a
/// pure function of its argument, before anything opens a handle with it.
/// </para>
/// <para>
/// The rule that matters most is the one about <b>not correcting</b> it. A relative path is
/// refused rather than resolved against the process's working directory: the working directory of
/// a desktop widget host is an implementation detail, so resolving "docs" against it would make
/// the widget show a folder the user never named, silently, and differently depending on how the
/// app was started. Refusing loudly is the only answer that stays true.
/// </para>
/// </summary>
public static class FolderPath
{
    /// <summary>
    /// The hard Win32 ceiling for a path, including the extended-length forms. Anything longer
    /// cannot name a folder under any setting, so it is refused here rather than at the API.
    /// <para>
    /// The much lower classic limit of 260 is <i>not</i> enforced here: whether it applies depends
    /// on the machine's long-path setting, which this function cannot see, and a path that is legal
    /// but unreachable is the filesystem's answer to give ("that folder does not exist"), not this
    /// one's.
    /// </para>
    /// </summary>
    public const int MaxLength = 32767;

    /// <summary>
    /// Characters Windows will not accept in a path. The wildcards are in the list on purpose:
    /// <c>*</c> and <c>?</c> are legal in a <i>search pattern</i>, and a configured path that
    /// reached an enumeration call carrying one would list something other than the folder it names.
    /// </summary>
    private static readonly char[] Invalid = ['<', '>', '"', '|', '?', '*'];

    /// <summary>
    /// The path to list, given what the config holds and where an empty value should point.
    /// </summary>
    /// <param name="configured">The value from the config. Blank means "use the fallback".</param>
    /// <param name="fallback">
    /// What a blank value means — the user's profile folder, so a newly placed widget shows
    /// something real rather than an error. Passed in rather than read from the environment so this
    /// stays a pure function.
    /// </param>
    public static FolderPathResult Resolve(string? configured, string fallback)
    {
        var trimmed = Clean(configured);

        if (trimmed.Length > 0)
        {
            return Validate(trimmed);
        }

        var cleanedFallback = Clean(fallback);

        if (cleanedFallback.Length == 0)
        {
            return new FolderPathResult(FolderPathProblem.Empty, string.Empty, false);
        }

        var result = Validate(cleanedFallback);
        return result with { UsedFallback = true };
    }

    /// <summary>Check one non-blank candidate. Returns the path unchanged, or says why not.</summary>
    public static FolderPathResult Validate(string candidate)
    {
        var path = Clean(candidate);

        if (path.Length == 0)
        {
            return new FolderPathResult(FolderPathProblem.Empty, string.Empty, false);
        }

        if (path.Length > MaxLength)
        {
            return new FolderPathResult(FolderPathProblem.TooLong, string.Empty, false);
        }

        if (!CharactersAreLegal(path))
        {
            return new FolderPathResult(FolderPathProblem.InvalidCharacters, string.Empty, false);
        }

        if (!IsFullyQualified(path))
        {
            return new FolderPathResult(FolderPathProblem.NotRooted, string.Empty, false);
        }

        return new FolderPathResult(FolderPathProblem.None, path, false);
    }

    /// <summary>
    /// What to call the folder in the header: its own name, or the root itself when it has none.
    /// </summary>
    public static string DisplayName(string path)
    {
        var trimmed = Clean(path).TrimEnd('\\', '/');

        if (trimmed.Length == 0)
        {
            return Clean(path);
        }

        // A drive root trims down to "C:", which is not a name anybody writes. Put the separator
        // back rather than showing a bare colon.
        if (trimmed.EndsWith(':'))
        {
            return trimmed + '\\';
        }

        var lastSeparator = trimmed.LastIndexOfAny(['\\', '/']);
        var name = lastSeparator < 0 ? trimmed : trimmed[(lastSeparator + 1)..];

        // "\\\\server" has nothing after the separators; the whole thing is the best name there is.
        return name.Length == 0 ? trimmed : name;
    }

    /// <summary>
    /// Trim whitespace, and the quotes Explorer's "Copy as path" wraps around everything it puts on
    /// the clipboard — which is the single most likely thing to be pasted into the box.
    /// </summary>
    private static string Clean(string? raw) => (raw ?? string.Empty).Trim().Trim('"').Trim();

    private static bool CharactersAreLegal(string path)
    {
        for (var i = 0; i < path.Length; i++)
        {
            var c = path[i];

            // Control characters, including the NUL that would truncate the string at the API.
            if (c < ' ' || c == (char)0x7F)
            {
                return false;
            }

            if (Array.IndexOf(Invalid, c) >= 0)
            {
                return false;
            }

            // A colon is the drive separator and nothing else. This also refuses the
            // extended-length forms ("\\?\C:\...") and NTFS alternate data streams, neither of
            // which belongs in a folder box.
            if (c == ':' && i != 1)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Windows' own definition, spelled out rather than delegated, because this is the check that
    /// decides whether anything is allowed to be resolved relative to the process.
    /// </summary>
    private static bool IsFullyQualified(string path)
    {
        // UNC: \\server\share.
        if (path.Length >= 2 && IsSeparator(path[0]) && IsSeparator(path[1]))
        {
            return true;
        }

        // Drive-rooted: C:\. "C:" alone is drive-*relative* and means "wherever that drive's
        // current directory happens to be", which is exactly the silent redirection to avoid.
        return path.Length >= 3
            && IsDriveLetter(path[0])
            && path[1] == ':'
            && IsSeparator(path[2]);
    }

    private static bool IsSeparator(char c) => c is '\\' or '/';

    private static bool IsDriveLetter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
