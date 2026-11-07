namespace Themia.Core.Widgets;

/// <summary>[S-2001] How a card-level click's target is opened. A closed set: each is its own gate at the launcher.</summary>
public enum CardOpenKind
{
    /// <summary>
    /// A target <see cref="LaunchTargets"/> accepts: an https page, a local folder or program, a
    /// <c>shell:</c> name (a packaged app's <c>shell:AppsFolder\&lt;AUMID&gt;</c> included).
    /// </summary>
    Target = 1,

    /// <summary>A page of Windows Settings (<c>ms-settings:&lt;page&gt;</c>).</summary>
    Settings = 2,

    /// <summary>An app by the AppUserModelID another process reported (the media session's): the launcher's own app gate.</summary>
    App = 3,
}

/// <summary>
/// [S-2001] What a click on a widget's card opens: the thing the widget is about (Task Manager for a
/// meter, the forecast for the weather, the folder for a folder). Built only through the factories,
/// each of which checks its value, so a <see cref="CardOpen"/> that exists is one the launcher will
/// take; the launcher checks the literal value again all the same.
/// </summary>
/// <param name="Kind">How it is opened.</param>
/// <param name="Value">Exactly what is handed to the launcher.</param>
public sealed record CardOpen
{
    private CardOpen(CardOpenKind kind, string value)
    {
        Kind = kind;
        Value = value;
    }

    public CardOpenKind Kind { get; }

    public string Value { get; }

    /// <summary>The Settings scheme, with its colon.</summary>
    public const string SettingsScheme = "ms-settings:";

    /// <summary>The longest Settings page name accepted (the longest real one is under 40).</summary>
    public const int MaxSettingsPage = 64;

    /// <summary>The longest app id accepted, as Windows bounds an AppUserModelID.</summary>
    public const int MaxAppId = 128;

    /// <summary>[S-2002] Windows' Clock app (alarms, timers, stopwatch, focus sessions), by its Apps-folder item.</summary>
    public static CardOpen ClockApp { get; } = Target(@"shell:AppsFolder\Microsoft.WindowsAlarms_8wekyb3d8bbwe!App")!;

    /// <summary>[S-2002] Task Manager, as the fixed program in the system folder.</summary>
    public static CardOpen? TaskManager => SystemProgram("Taskmgr.exe");

    /// <summary>
    /// [S-2002] A target <see cref="LaunchTargets"/> accepts, as it normalises it, or null. A web
    /// page must be https: a widget's click never opens a page in the clear.
    /// </summary>
    public static CardOpen? Target(string? target)
    {
        var classified = LaunchTargets.Classify(target);

        if (!classified.IsValid
            || (classified.Kind == LaunchKind.Web && !classified.Value.StartsWith("https://", StringComparison.Ordinal)))
        {
            return null;
        }

        return new CardOpen(CardOpenKind.Target, classified.Value);
    }

    /// <summary>[S-2002] An https page, or null for anything else (a path, a shell name, http).</summary>
    public static CardOpen? Web(string? url)
        => Target(url) is { } open && LaunchTargets.Classify(open.Value).Kind == LaunchKind.Web ? open : null;

    /// <summary>[S-2002] A folder or file on this machine, or a shell location, or null (a web page is refused).</summary>
    public static CardOpen? Place(string? path)
        => Target(path) is { } open && LaunchTargets.Classify(open.Value).Kind is LaunchKind.LocalPath or LaunchKind.ShellNamespace ? open : null;

    /// <summary>
    /// [S-2002] A program in the Windows system folder, named by its bare file name (<c>Taskmgr.exe</c>),
    /// or null for a name that is a path, carries a separator or is not an <c>.exe</c>.
    /// </summary>
    public static CardOpen? SystemProgram(string file)
    {
        if (string.IsNullOrWhiteSpace(file)
            || !file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || file.IndexOfAny(['\\', '/', ':']) >= 0
            || file.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var system = Environment.SystemDirectory;
        return string.IsNullOrEmpty(system) ? null : Place(Path.Combine(system, file));
    }

    /// <summary>
    /// [S-2002] A page of Windows Settings by its name (<c>sound</c>, <c>bluetooth</c>): lower-case
    /// ASCII letters, digits and dashes only, so the URI is a fixed page and never a query.
    /// </summary>
    public static CardOpen? Settings(string page)
    {
        if (string.IsNullOrEmpty(page) || page.Length > MaxSettingsPage
            || !page.All(static c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-')
            || page[0] == '-')
        {
            return null;
        }

        return new CardOpen(CardOpenKind.Settings, SettingsScheme + page);
    }

    /// <summary>
    /// [S-2003] An app by its AppUserModelID as another process reported it (untrusted text): a
    /// packaged app's id becomes its Apps-folder item (a <see cref="Target"/>); a desktop app's own
    /// id (<c>Spotify.exe</c>, <c>Chrome</c>) passes only as ASCII letters, digits and <c>. - _</c>,
    /// for the launcher's app gate. Null for anything else.
    /// </summary>
    public static CardOpen? App(string? appId)
    {
        var id = appId?.Trim() ?? string.Empty;

        if (id.Length is 0 or > MaxAppId)
        {
            return null;
        }

        if (LaunchTargets.IsPackagedAppId(id))
        {
            return Target(LaunchTargets.ShellPrefix + LaunchTargets.AppsFolderName + @"\" + id);
        }

        if (id[0] is '.' or '-' || id.Contains("..", StringComparison.Ordinal)
            || !id.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_'))
        {
            return null;
        }

        return new CardOpen(CardOpenKind.App, id);
    }
}
