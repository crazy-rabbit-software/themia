using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>What a launch target turned out to be, once it had been checked.</summary>
public enum LaunchKind
{
    /// <summary>It is not something this app will open. See the <see cref="LaunchRefusal"/>.</summary>
    Refused = 0,

    /// <summary>An absolute <c>http</c> or <c>https</c> URL.</summary>
    Web = 1,

    /// <summary>A fully qualified path to a file or folder on this machine or a UNC share.</summary>
    LocalPath = 2,

    /// <summary>A Windows shell namespace name, e.g. <c>shell:RecycleBinFolder</c>.</summary>
    ShellNamespace = 3,
}

/// <summary>Why a launch target was refused. Every refusal has a sentence a person can act on.</summary>
public enum LaunchRefusal
{
    None = 0,

    /// <summary>Nothing was given.</summary>
    Empty = 1,

    /// <summary>Longer than anything that could name a real target.</summary>
    TooLong = 2,

    /// <summary>It carries a control character — a newline, a tab, or the NUL that truncates at the API.</summary>
    ControlCharacters = 3,

    /// <summary>It names a scheme, and the scheme is not <c>http</c> or <c>https</c>.</summary>
    UnsupportedScheme = 4,

    /// <summary>It says it is a URL but is not one, or it carries credentials.</summary>
    MalformedUrl = 5,

    /// <summary>A path that is relative, or drive-relative, so it means different things from different places.</summary>
    NotRooted = 6,

    /// <summary>A path holding a character Windows will not accept, or a wildcard.</summary>
    InvalidPathCharacters = 7,

    /// <summary><c>shell:</c> with nothing after it.</summary>
    EmptyShellName = 8,

    /// <summary>A shell name holding something other than a plain namespace name or a class id.</summary>
    InvalidShellName = 9,
}

/// <summary>
/// One checked launch target: what it is, the exact string to hand to the shell, and — when it was
/// refused — why.
/// </summary>
/// <param name="Kind">What it turned out to be.</param>
/// <param name="Value">The string to launch. Empty whenever <paramref name="Kind"/> is Refused.</param>
/// <param name="Refusal">Why it was refused, or <see cref="LaunchRefusal.None"/>.</param>
public readonly record struct LaunchTarget(LaunchKind Kind, string Value, LaunchRefusal Refusal)
{
    public bool IsValid => Kind != LaunchKind.Refused;

    internal static LaunchTarget Refuse(LaunchRefusal refusal)
        => new(LaunchKind.Refused, string.Empty, refusal);
}

/// <summary>
/// The trust boundary in front of the shell.
/// <para>
/// Four widgets in this app open things: an app launcher, a shortcut list, a search box and a
/// clipboard history. Between them they turn a JSON file in the user's profile into a list of
/// things the desktop will execute on a click — which is a persistence and execution primitive, and
/// is only safe if exactly one function decides what may be opened. This is that function, and it
/// is pure so it can be tested against the values an attacker would actually try.
/// </para>
/// <para>
/// The rule is an <b>allow list, not a deny list</b>. Three shapes are accepted — an absolute
/// http/https URL, a fully qualified local or UNC path, and a <c>shell:</c> namespace name — and
/// everything else is refused by default, including every scheme nobody thought of. A deny list of
/// "javascript, data, file" would have been wrong the day Windows registered the next protocol
/// handler.
/// </para>
/// <para>
/// Nothing here is ever <i>corrected</i>. A relative path is refused rather than resolved against
/// the process's working directory, which is an implementation detail of how the app was started; a
/// bare word is refused rather than turned into <c>https://word</c>, because guessing is how a
/// widget opens something the user never named.
/// </para>
/// </summary>
public static class LaunchTargets
{
    /// <summary>The hard Win32 ceiling for a path. Nothing longer can name a target.</summary>
    public const int MaxPathLength = 32767;

    /// <summary>
    /// The longest URL accepted. Browsers stop somewhere near here and a URL this long in a widget
    /// config is a payload rather than a link.
    /// </summary>
    public const int MaxUrlLength = 2048;

    /// <summary>The prefix that marks a Windows shell namespace name.</summary>
    public const string ShellPrefix = "shell:";

    /// <summary>
    /// The shell names this app puts a friendly label on. Not a permission list — any well-formed
    /// shell name is accepted — only the ones worth showing by name rather than by URI. The labels
    /// are string ids, so each location reads as Explorer names it in the user's language.
    /// </summary>
    private static readonly Dictionary<string, Str> ShellLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RecycleBinFolder"] = Str.WidgetsC_Location_RecycleBin,
        ["MyComputerFolder"] = Str.WidgetsC_Location_ThisPc,
        ["Downloads"] = Str.WidgetsC_Location_Downloads,
        ["Personal"] = Str.WidgetsC_Location_Documents,
        ["Desktop"] = Str.WidgetsC_Location_Desktop,
        ["My Pictures"] = Str.WidgetsC_Location_Pictures,
        ["My Music"] = Str.WidgetsC_Location_Music,
        ["My Video"] = Str.WidgetsC_Location_Videos,
        ["UsersFilesFolder"] = Str.WidgetsC_Location_Home,
        ["ControlPanelFolder"] = Str.WidgetsC_Location_ControlPanel,
        ["NetworkPlacesFolder"] = Str.WidgetsC_Location_Network,
        ["Profile"] = Str.WidgetsC_Location_UserFolder,
    };

    /// <summary>The shell names offered as one-click additions in the Shortcuts settings.</summary>
    public static IReadOnlyList<string> WellKnownShellNames { get; } =
    [
        "RecycleBinFolder",
        "MyComputerFolder",
        "Downloads",
        "Personal",
        "Desktop",
        "My Pictures",
        "UsersFilesFolder",
        "ControlPanelFolder",
        "NetworkPlacesFolder",
    ];

    /// <summary>
    /// Decide what a target is, or refuse it. The only way anything in this app reaches the shell.
    /// </summary>
    public static LaunchTarget Classify(string? raw)
    {
        var value = Clean(raw);

        if (value.Length == 0)
        {
            return LaunchTarget.Refuse(LaunchRefusal.Empty);
        }

        if (value.Length > MaxPathLength)
        {
            return LaunchTarget.Refuse(LaunchRefusal.TooLong);
        }

        // Before anything is parsed: a control character in a launch target is never legitimate,
        // and a NUL would truncate the string at the API so that what is checked and what is
        // launched are two different strings.
        if (HasControlCharacter(value))
        {
            return LaunchTarget.Refuse(LaunchRefusal.ControlCharacters);
        }

        if (value.StartsWith(ShellPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ClassifyShell(value[ShellPrefix.Length..]);
        }

        var scheme = SchemeOf(value);

        if (scheme is not null)
        {
            return scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
                || scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
                ? ClassifyWeb(value)
                : LaunchTarget.Refuse(LaunchRefusal.UnsupportedScheme);
        }

        return ClassifyPath(value);
    }

    /// <summary>A sentence for a refusal, shown under the box that produced it.</summary>
    public static string RefusalMessage(LaunchRefusal refusal, Locale? locale = null)
    {
        if (refusal == LaunchRefusal.None)
        {
            return string.Empty;
        }

        var id = refusal switch
        {
            LaunchRefusal.Empty => Str.WidgetsC_Launch_Refusal_Empty,
            LaunchRefusal.TooLong => Str.WidgetsC_Launch_Refusal_TooLong,
            LaunchRefusal.ControlCharacters => Str.WidgetsC_Launch_Refusal_ControlCharacters,
            LaunchRefusal.UnsupportedScheme => Str.WidgetsC_Launch_Refusal_UnsupportedScheme,
            LaunchRefusal.MalformedUrl => Str.WidgetsC_Launch_Refusal_MalformedUrl,
            LaunchRefusal.NotRooted => Str.WidgetsC_Launch_Refusal_NotRooted,
            LaunchRefusal.InvalidPathCharacters => Str.WidgetsC_Launch_Refusal_InvalidPathCharacters,
            LaunchRefusal.EmptyShellName => Str.WidgetsC_Launch_Refusal_EmptyShellName,
            LaunchRefusal.InvalidShellName => Str.WidgetsC_Launch_Refusal_InvalidShellName,
            _ => Str.WidgetsC_Launch_Refusal_Other,
        };

        return Strings.Get(locale ?? Strings.Current, id);
    }

    /// <summary>
    /// What to call a target in a list: the user's own label when there is one, and otherwise the
    /// most human part of the target itself. [S-1381] A file keeps its extension only when
    /// <paramref name="showExtension"/> asks for it (original S-48); a label always wins.
    /// </summary>
    public static string DisplayName(string? label, string? target, bool showExtension = false)
    {
        var chosen = (label ?? string.Empty).Trim();

        if (chosen.Length > 0)
        {
            return chosen;
        }

        var value = Clean(target);
        var classified = Classify(value);

        return classified.Kind switch
        {
            LaunchKind.Web => HostName(classified.Value, value),
            LaunchKind.ShellNamespace => ShellDisplayName(classified.Value),
            LaunchKind.LocalPath => PathDisplayName(classified.Value, showExtension),

            // A refused target still has to say something, and the raw string is the only honest
            // thing left: a blank row would hide the item that needs fixing.
            _ => value,
        };
    }

    /// <summary>The friendly label for a shell name, or the name itself.</summary>
    public static string ShellDisplayName(string target, Locale? locale = null)
    {
        var value = Clean(target);

        var name = value.StartsWith(ShellPrefix, StringComparison.OrdinalIgnoreCase)
            ? value[ShellPrefix.Length..]
            : value;

        return ShellLabels.TryGetValue(name, out var label) ? Strings.Get(locale ?? Strings.Current, label) : name;
    }

    /// <summary>
    /// [S-1386] Whether a classified local path is on another machine (a UNC path).
    /// Such a path is opened when the user asks, but never <i>probed</i> unasked — no icon lookup and
    /// no existence check — because touching a UNC path makes Windows connect to that server and
    /// offer it the user's credentials, and the path came from a file anything can rewrite.
    /// </summary>
    public static bool IsNetworkPath(LaunchTarget target)
        => target.Kind == LaunchKind.LocalPath
            && (target.Value.StartsWith(@"\\", StringComparison.Ordinal) || target.Value.StartsWith("//", StringComparison.Ordinal));

    /// <summary>
    /// [S-1386] Whether a target may be looked at without being opened: its icon read, its existence
    /// checked. A shell location always; a local path unless it is a network path; a web address
    /// never (its icon is the site's own favicon, fetched through the network guard instead).
    /// </summary>
    public static bool MayProbe(LaunchTarget target) => target.Kind switch
    {
        LaunchKind.ShellNamespace => true,
        LaunchKind.LocalPath => !IsNetworkPath(target),
        _ => false,
    };

    /// <summary>
    /// Whether two targets name the same thing once classified, ignoring case — how a Windows
    /// location switch finds every item that points at it.
    /// </summary>
    public static bool SameTarget(string? a, string? b)
    {
        var left = Classify(a);
        var right = Classify(b);

        return left.IsValid && right.IsValid && left.Kind == right.Kind
            && string.Equals(left.Value, right.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The <c>shell:</c> target for a well-known name, ready to store.</summary>
    public static string ShellTarget(string name) => ShellPrefix + name.Trim();

    // ---- the three shapes ----------------------------------------------------------------------

    private static LaunchTarget ClassifyWeb(string value)
    {
        if (value.Length > MaxUrlLength)
        {
            return LaunchTarget.Refuse(LaunchRefusal.TooLong);
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return LaunchTarget.Refuse(LaunchRefusal.MalformedUrl);
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return LaunchTarget.Refuse(LaunchRefusal.UnsupportedScheme);
        }

        if (string.IsNullOrEmpty(uri.Host))
        {
            return LaunchTarget.Refuse(LaunchRefusal.MalformedUrl);
        }

        // "https://real.example.com@attacker.example/" reads as the first host and goes to the
        // second. Credentials in a URL are a phishing shape and nothing else, so they are refused
        // rather than stripped.
        if (uri.UserInfo.Length > 0)
        {
            return LaunchTarget.Refuse(LaunchRefusal.MalformedUrl);
        }

        // The normalised form, not the typed one: what is checked and what is launched must be the
        // same string, and Uri's normalisation is the one both agree on.
        return new LaunchTarget(LaunchKind.Web, uri.AbsoluteUri, LaunchRefusal.None);
    }

    private static LaunchTarget ClassifyPath(string value)
    {
        // One path validator in the app: the folder widget's, which already knows what Windows
        // accepts, that a colon belongs at index 1 and nowhere else, and that a relative path must
        // never be resolved against the process.
        var result = FolderPath.Validate(value);

        if (result.IsValid)
        {
            return new LaunchTarget(LaunchKind.LocalPath, result.Path, LaunchRefusal.None);
        }

        return LaunchTarget.Refuse(result.Problem switch
        {
            FolderPathProblem.Empty => LaunchRefusal.Empty,
            FolderPathProblem.TooLong => LaunchRefusal.TooLong,
            FolderPathProblem.NotRooted => LaunchRefusal.NotRooted,
            _ => LaunchRefusal.InvalidPathCharacters,
        });
    }

    private static LaunchTarget ClassifyShell(string name)
    {
        if (name.Length == 0)
        {
            return LaunchTarget.Refuse(LaunchRefusal.EmptyShellName);
        }

        // A shell name is a namespace name or a class id, and nothing else. No separators, so it
        // cannot be steered out of the namespace and into a path; no quotes, ampersands or percent
        // signs, so there is nothing in it a shell could take as syntax.
        foreach (var c in name)
        {
            var allowed = char.IsAsciiLetterOrDigit(c)
                || c is ' ' or '-' or '_' or '.' or '{' or '}' or ':';

            if (!allowed)
            {
                return LaunchTarget.Refuse(LaunchRefusal.InvalidShellName);
            }
        }

        if (name.Contains("..", StringComparison.Ordinal))
        {
            return LaunchTarget.Refuse(LaunchRefusal.InvalidShellName);
        }

        // The prefix is normalised to lower case so two spellings of the same location compare
        // equal; the name keeps its case, because "My Pictures" is how the shell spells it.
        return new LaunchTarget(LaunchKind.ShellNamespace, ShellPrefix + name, LaunchRefusal.None);
    }

    // ---- pieces --------------------------------------------------------------------------------

    /// <summary>
    /// The scheme this value declares, or null when it declares none.
    /// <para>
    /// The colon at index 1 is the drive separator, never a scheme: <c>C:\Windows</c> is a path and
    /// a one-letter scheme is not a thing this app will open anyway. Everything else before the
    /// first colon has to be a legal scheme to count as one, so <c>\\server\share\a:b</c> is still
    /// a path.
    /// </para>
    /// </summary>
    private static string? SchemeOf(string value)
    {
        var colon = value.IndexOf(':', StringComparison.Ordinal);

        if (colon <= 1)
        {
            return null;
        }

        for (var i = 0; i < colon; i++)
        {
            var c = value[i];
            var legal = i == 0
                ? char.IsAsciiLetter(c)
                : char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.';

            if (!legal)
            {
                return null;
            }
        }

        return value[..colon];
    }

    private static string HostName(string absoluteUrl, string fallback)
    {
        if (!Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri))
        {
            return fallback;
        }

        var host = uri.Host;

        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    /// <summary>
    /// A path's own name: the file without its extension, or the folder's name. The extension is
    /// dropped by default because a launcher row says what it opens, not what format it is in.
    /// </summary>
    private static string PathDisplayName(string path, bool showExtension)
    {
        var trimmed = path.TrimEnd('\\', '/');

        if (trimmed.Length == 0 || trimmed.EndsWith(':'))
        {
            return FolderPath.DisplayName(path);
        }

        var name = FolderPath.DisplayName(trimmed);

        if (showExtension)
        {
            return name;
        }

        var dot = name.LastIndexOf('.');

        return dot > 0 ? name[..dot] : name;
    }

    private static bool HasControlCharacter(string value)
    {
        foreach (var c in value)
        {
            if (c < ' ' || c == (char)0x7F)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Trim, and drop the quotes Explorer's "Copy as path" wraps around everything it puts on the
    /// clipboard — which is the single most likely thing to be pasted into a target box.
    /// </summary>
    internal static string Clean(string? raw) => (raw ?? string.Empty).Trim().Trim('"').Trim();
}
