using System.Globalization;
using System.Text;
using System.Text.Json;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tasks;

namespace Themia.Core.Widgets;

/// <summary>[S-1920] Where on a widget a click opens its app.</summary>
public enum LaunchArea
{
    /// <summary>Nothing opens: no app makes sense for this widget.</summary>
    None = 0,

    /// <summary>The whole card, less the widget's own buttons, sliders, rows and boxes.</summary>
    Body = 1,

    /// <summary>The header (icon and title) only: the body is the widget's own list or canvas.</summary>
    Header = 2,
}

/// <summary>[S-1921] How a launch target is opened. A closed set: each one is its own gate at the launcher.</summary>
public enum WidgetLaunchMethod
{
    /// <summary>A program in the Windows system folder, named by file (Task Manager).</summary>
    Program = 1,

    /// <summary>A page of Windows Settings (<c>ms-settings:</c>).</summary>
    Settings = 2,

    /// <summary>An app's own protocol (<c>ms-clock:</c>, <c>ms-todo:</c>, <c>outlookcal:</c>).</summary>
    Protocol = 3,

    /// <summary>An https page, built from the widget's own data.</summary>
    Web = 4,

    /// <summary>A folder, shown in File Explorer.</summary>
    Folder = 5,

    /// <summary>The app that owns the media session, by its app user model id.</summary>
    MediaApp = 6,
}

/// <summary>[S-1922] What a launch opens, in words: the tooltip and accessible text of the click target.</summary>
public enum WidgetLaunchTitle
{
    TaskManager,
    NetworkSettings,
    PowerSettings,
    SoundSettings,
    VolumeMixer,
    BluetoothSettings,
    Clock,
    PlayingApp,
    Forecast,
    Quote,
    Website,
    Stream,
    Calendar,
    Mail,
    Contacts,
    Tasks,
    Folder,
}

/// <summary>[S-1921] One thing a click on a widget opens.</summary>
/// <param name="Method">How it is opened.</param>
/// <param name="Value">The program file, URI, URL, folder or app id, exactly as it will be launched.</param>
/// <param name="Title">What it is, for the tooltip.</param>
/// <param name="AppName">The playing app's name, for <see cref="WidgetLaunchTitle.PlayingApp"/>; empty otherwise.</param>
public sealed record WidgetLaunchTarget(WidgetLaunchMethod Method, string Value, WidgetLaunchTitle Title, string AppName = "");

/// <summary>A signed-in account, as much of it as choosing a web page needs.</summary>
public sealed record LaunchAccount(string Id, AccountProvider Provider);

/// <summary>
/// [S-1923] What a widget knows only while it runs: the playing app, the folder it is showing, the
/// site of its one feed. Every field is checked again by <see cref="WidgetLaunch"/>; empty means unknown.
/// </summary>
public sealed record WidgetLiveLaunch(string MediaAppId = "", string MediaAppName = "", string Folder = "", string Site = "")
{
    public static WidgetLiveLaunch None { get; } = new();
}

/// <summary>[S-1921] Everything outside the config that decides the target.</summary>
public sealed record WidgetLaunchContext
{
    /// <summary>The signed-in accounts, in the order the Accounts page lists them.</summary>
    public IReadOnlyList<LaunchAccount> Accounts { get; init; } = [];

    /// <summary>Whether an app has registered a protocol (<c>ms-todo</c>, <c>outlookcal</c>) on this PC.</summary>
    public Func<string, bool> HasProtocol { get; init; } = static _ => false;

    /// <summary>The UI language, for a web page offered in more than one.</summary>
    public Locale Locale { get; init; } = Locale.En;

    public WidgetLiveLaunch Live { get; init; } = WidgetLiveLaunch.None;
}

/// <summary>What the launcher did with a target.</summary>
public enum WidgetLaunchResult
{
    Opened = 0,

    /// <summary>The target failed its gate: nothing was handed to the shell.</summary>
    Refused = 1,

    /// <summary>The shell was asked and said no, or the app could not be found.</summary>
    Failed = 2,
}

/// <summary>[S-1924] The one thing that opens a widget's target; the App's is the shell, a test's is a fake.</summary>
public interface IWidgetLauncher
{
    WidgetLaunchResult Launch(WidgetLaunchTarget target);
}

/// <summary>
/// [S-1920]..[S-1929] What clicking a widget opens: Task Manager for a meter, the sound settings for
/// the volume, the playing app for music, the forecast for the weather. One table, pure, so every
/// widget is decided the same way and the decision is tested without a desktop.
/// <para>
/// Nothing here opens anything a stranger chose. A program is a fixed file in the system folder; a
/// Settings page or protocol is a fixed string in this file; a web page is an https URL built from
/// the widget's own config or the data it fetched, checked by <see cref="LaunchTargets"/>; a folder is
/// a validated local path; an app id passes <see cref="MediaApps"/>. <see cref="IsLaunchable"/> is the
/// same gate, re-applied by the launcher to the literal target.
/// </para>
/// </summary>
public static class WidgetLaunch
{
    /// <summary>[S-1921] The only program a widget opens.</summary>
    public const string TaskManager = "Taskmgr.exe";

    public const string NetworkPage = "ms-settings:network-status";
    public const string PowerPage = "ms-settings:powersleep";
    public const string SoundPage = "ms-settings:sound";
    public const string MixerPage = "ms-settings:apps-volume";
    public const string BluetoothPage = "ms-settings:bluetooth";

    public const string ClockProtocol = "ms-clock";
    public const string ToDoProtocol = "ms-todo";
    public const string CalendarProtocol = "outlookcal";

    private static readonly HashSet<string> SettingsPages = new(StringComparer.Ordinal)
    {
        NetworkPage, PowerPage, SoundPage, MixerPage, BluetoothPage,
    };

    private static readonly HashSet<string> Protocols = new(StringComparer.Ordinal)
    {
        ClockProtocol + ":", ToDoProtocol + ":", CalendarProtocol + ":",
    };

    /// <summary>[S-1920] Where a click opens this kind of widget.</summary>
    public static LaunchArea AreaOf(WidgetKind kind) => kind switch
    {
        WidgetKind.Cpu or WidgetKind.Memory or WidgetKind.Disk or WidgetKind.Gpu
            or WidgetKind.Network or WidgetKind.Ping or WidgetKind.Battery
            or WidgetKind.Volume or WidgetKind.Bluetooth
            or WidgetKind.Music or WidgetKind.Clock
            or WidgetKind.WeatherNow or WidgetKind.WeatherForecast
            or WidgetKind.Stocks or WidgetKind.VideoStream => LaunchArea.Body,

        WidgetKind.Mixer or WidgetKind.Email or WidgetKind.Calendar or WidgetKind.Contacts
            or WidgetKind.Tasks or WidgetKind.Rss or WidgetKind.Folder or WidgetKind.Gallery => LaunchArea.Header,

        _ => LaunchArea.None,
    };

    /// <summary>
    /// [S-1925] Whether a left click opens the target: outside edit mode, on a real desktop card
    /// (never a debug render's preview), inside the kind's area and not on a control of the widget's
    /// own. In edit mode a click selects and follows the settings window ([S-1745]) instead.
    /// </summary>
    public static bool OpensOnClick(bool editMode, bool preview, LaunchArea area, bool inHeader, bool onControl)
        => !editMode && !preview && !onControl && area switch
        {
            LaunchArea.Body => true,
            LaunchArea.Header => inHeader,
            _ => false,
        };

    /// <summary>
    /// [S-1921] What a click on this widget opens, or null when nothing makes sense: the kind has no
    /// app, the widget is not set up ([S-1860]), or the data the target needs is missing or refused.
    /// </summary>
    public static WidgetLaunchTarget? For(WidgetKind kind, WidgetConfig config, WidgetLaunchContext context)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(context);

        if (config.Kind != kind || AreaOf(kind) == LaunchArea.None)
        {
            return null;
        }

        if (WidgetSetup.Needed(config, WidgetSetup.Offered(context.Accounts.Select(a => a.Provider))))
        {
            return null;
        }

        var target = kind switch
        {
            WidgetKind.Cpu or WidgetKind.Memory or WidgetKind.Disk or WidgetKind.Gpu
                => new WidgetLaunchTarget(WidgetLaunchMethod.Program, TaskManager, WidgetLaunchTitle.TaskManager),
            WidgetKind.Network or WidgetKind.Ping => Settings(NetworkPage, WidgetLaunchTitle.NetworkSettings),
            WidgetKind.Battery => Settings(PowerPage, WidgetLaunchTitle.PowerSettings),
            WidgetKind.Volume => Settings(SoundPage, WidgetLaunchTitle.SoundSettings),
            WidgetKind.Mixer => Settings(MixerPage, WidgetLaunchTitle.VolumeMixer),
            WidgetKind.Bluetooth => Settings(BluetoothPage, WidgetLaunchTitle.BluetoothSettings),
            WidgetKind.Clock => new WidgetLaunchTarget(WidgetLaunchMethod.Protocol, ClockProtocol + ":", WidgetLaunchTitle.Clock),
            WidgetKind.Music => Media(context.Live),
            WidgetKind.WeatherNow or WidgetKind.WeatherForecast => Weather((WeatherConfig)config, context.Locale),
            WidgetKind.Stocks => Quote((StocksConfig)config),
            WidgetKind.VideoStream => Web(((VideoStreamConfig)config).ResolvedUrl is { IsValid: true } url ? url.Url : null, WidgetLaunchTitle.Stream),
            WidgetKind.Rss => Web(context.Live.Site, WidgetLaunchTitle.Website),
            WidgetKind.Email => Mail((EmailConfig)config, context),
            WidgetKind.Calendar => Calendar((CalendarConfig)config, context),
            WidgetKind.Contacts => Contacts((ContactsConfig)config, context),
            WidgetKind.Tasks => Tasks((TasksConfig)config, context),
            WidgetKind.Folder => Folder(context.Live.Folder.Length > 0 ? context.Live.Folder : ((FolderConfig)config).Path),
            WidgetKind.Gallery => config is GalleryConfig { Source: GallerySource.Folder } gallery ? Folder(gallery.Path) : null,
            _ => null,
        };

        return target is not null && IsLaunchable(target) ? target : null;
    }

    /// <summary>
    /// [S-1924] The gate, on the literal target: a fixed program, a fixed Settings page or protocol, an
    /// https URL <see cref="LaunchTargets"/> accepts, a local folder, or a well-formed app id.
    /// </summary>
    public static bool IsLaunchable(WidgetLaunchTarget? target)
    {
        if (target is null || string.IsNullOrEmpty(target.Value) || target.Value.Any(char.IsControl))
        {
            return false;
        }

        return target.Method switch
        {
            WidgetLaunchMethod.Program => string.Equals(target.Value, TaskManager, StringComparison.Ordinal),
            WidgetLaunchMethod.Settings => SettingsPages.Contains(target.Value),
            WidgetLaunchMethod.Protocol => Protocols.Contains(target.Value),
            WidgetLaunchMethod.Web => IsHttpsPage(target.Value),
            WidgetLaunchMethod.Folder => LaunchTargets.Classify(target.Value) is { Kind: LaunchKind.LocalPath or LaunchKind.ShellNamespace } folder
                && string.Equals(folder.Value, target.Value, StringComparison.Ordinal),
            WidgetLaunchMethod.MediaApp => MediaApps.KindOf(target.Value) != MediaAppKind.Invalid,
            _ => false,
        };
    }

    /// <summary>[S-1924] Open a target through <paramref name="launcher"/>, refusing one that fails the gate.</summary>
    public static WidgetLaunchResult Open(WidgetLaunchTarget? target, IWidgetLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        return IsLaunchable(target) ? launcher.Launch(target!) : WidgetLaunchResult.Refused;
    }

    /// <summary>[S-1922] The tooltip and accessible text: "Open Task Manager", "Open Spotify".</summary>
    public static string Text(WidgetLaunchTarget target, Locale? locale = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        var language = locale ?? Strings.Current;

        if (target.Title == WidgetLaunchTitle.PlayingApp)
        {
            return target.AppName.Trim().Length > 0
                ? Strings.Format(language, Str.Shell_Launch_PlayingApp, ("app", target.AppName.Trim()))
                : Strings.Get(language, Str.Shell_Launch_PlayingAppUnnamed);
        }

        return Strings.Get(language, target.Title switch
        {
            WidgetLaunchTitle.TaskManager => Str.Shell_Launch_TaskManager,
            WidgetLaunchTitle.NetworkSettings => Str.Shell_Launch_NetworkSettings,
            WidgetLaunchTitle.PowerSettings => Str.Shell_Launch_PowerSettings,
            WidgetLaunchTitle.SoundSettings => Str.Shell_Launch_SoundSettings,
            WidgetLaunchTitle.VolumeMixer => Str.Shell_Launch_VolumeMixer,
            WidgetLaunchTitle.BluetoothSettings => Str.Shell_Launch_BluetoothSettings,
            WidgetLaunchTitle.Clock => Str.Shell_Launch_Clock,
            WidgetLaunchTitle.Forecast => Str.Shell_Launch_Forecast,
            WidgetLaunchTitle.Quote => Str.Shell_Launch_Quote,
            WidgetLaunchTitle.Website => Str.Shell_Launch_Website,
            WidgetLaunchTitle.Stream => Str.Shell_Launch_Stream,
            WidgetLaunchTitle.Calendar => Str.Shell_Launch_Calendar,
            WidgetLaunchTitle.Mail => Str.Shell_Launch_Mail,
            WidgetLaunchTitle.Contacts => Str.Shell_Launch_Contacts,
            WidgetLaunchTitle.Tasks => Str.Shell_Launch_Tasks,
            _ => Str.Shell_Launch_Folder,
        });
    }

    // ---- the targets ----------------------------------------------------------------------------

    private static WidgetLaunchTarget Settings(string page, WidgetLaunchTitle title)
        => new(WidgetLaunchMethod.Settings, page, title);

    /// <summary>[S-1926] The app of the current media session; nothing when nothing is loaded.</summary>
    private static WidgetLaunchTarget? Media(WidgetLiveLaunch live)
        => MediaApps.KindOf(live.MediaAppId) == MediaAppKind.Invalid
            ? null
            : new WidgetLaunchTarget(WidgetLaunchMethod.MediaApp, live.MediaAppId.Trim(), WidgetLaunchTitle.PlayingApp, live.MediaAppName ?? string.Empty);

    /// <summary>An https page, or nothing: an http page, a refused URL and an empty one all open nothing.</summary>
    private static WidgetLaunchTarget? Web(string? url, WidgetLaunchTitle title)
    {
        var checkedUrl = WebInput.Url(url);
        if (!checkedUrl.IsValid)
        {
            return null;
        }

        // The shell gets the normalised form, so what the gate checks and what is launched agree.
        var web = LaunchTargets.Classify(checkedUrl.Url);
        return web.Kind == LaunchKind.Web && web.Value.StartsWith("https://", StringComparison.Ordinal)
            ? new WidgetLaunchTarget(WidgetLaunchMethod.Web, web.Value, title)
            : null;
    }

    /// <summary>
    /// [S-1927] MSN Weather's forecast for the widget's own coordinates, in the UI language's market,
    /// in the widget's unit. The place travels as MSN's own <c>loc</c> parameter (base64 JSON with
    /// the coordinates), because a name alone lands on whichever town MSN guesses.
    /// </summary>
    private static WidgetLaunchTarget? Weather(WeatherConfig weather, Locale locale)
    {
        if (!weather.HasLocation)
        {
            return null;
        }

        var name = weather.LocationName.Split(',', 2)[0].Trim();
        if (name.Length == 0)
        {
            name = weather.Place.Trim();
        }

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("l", name);
            json.WriteString("x", weather.Longitude!.Value.ToString("0.####", CultureInfo.InvariantCulture));
            json.WriteString("y", weather.Latitude!.Value.ToString("0.####", CultureInfo.InvariantCulture));
            json.WriteEndObject();
        }

        var loc = Convert.ToBase64String(buffer.ToArray());
        var unit = weather.Unit == TemperatureUnit.Fahrenheit ? "F" : "C";
        var url = $"https://www.msn.com/{Market(locale)}/weather/forecast/in-{Slug(name)}?loc={Uri.EscapeDataString(loc)}&weadegreetype={unit}";

        return Web(url, WidgetLaunchTitle.Forecast);
    }

    /// <summary>[S-1927] The symbol's own page: Yahoo Finance for a listed symbol, CoinGecko for a coin.</summary>
    private static WidgetLaunchTarget? Quote(StocksConfig stocks)
    {
        var symbol = stocks.EffectiveSymbol;
        if (symbol.Length == 0)
        {
            return null;
        }

        var url = stocks.Market == QuoteMarket.Crypto
            ? $"https://www.coingecko.com/en/coins/{Uri.EscapeDataString(symbol)}"
            : $"https://finance.yahoo.com/quote/{Uri.EscapeDataString(symbol)}/";

        return Web(url, WidgetLaunchTitle.Quote);
    }

    /// <summary>[S-1928] The inbox on the web of the first chosen account that has one; IMAP has none.</summary>
    private static WidgetLaunchTarget? Mail(EmailConfig mail, WidgetLaunchContext context)
        => Provider(mail.EffectiveAccountIds, context.Accounts, AccountFeatures.Mail, [AccountProvider.Microsoft, AccountProvider.Google]) switch
        {
            AccountProvider.Microsoft => Web("https://outlook.office.com/mail/", WidgetLaunchTitle.Mail),
            AccountProvider.Google => Web("https://mail.google.com/", WidgetLaunchTitle.Mail),
            _ => null,
        };

    /// <summary>[S-1928] The calendar on the web; for CalDAV alone, the calendar app when one is registered.</summary>
    private static WidgetLaunchTarget? Calendar(CalendarConfig calendar, WidgetLaunchContext context)
        => Provider(calendar.AccountIds, context.Accounts, AccountFeatures.Calendar, [AccountProvider.Microsoft, AccountProvider.Google]) switch
        {
            AccountProvider.Microsoft => Web("https://outlook.office.com/calendar/", WidgetLaunchTitle.Calendar),
            AccountProvider.Google => Web("https://calendar.google.com/", WidgetLaunchTitle.Calendar),
            not null when context.HasProtocol(CalendarProtocol)
                => new WidgetLaunchTarget(WidgetLaunchMethod.Protocol, CalendarProtocol + ":", WidgetLaunchTitle.Calendar),
            _ => null,
        };

    /// <summary>[S-1928] The address book on the web; CardDAV has none.</summary>
    private static WidgetLaunchTarget? Contacts(ContactsConfig contacts, WidgetLaunchContext context)
        => Provider(contacts.AccountIds, context.Accounts, AccountFeatures.Contacts, [AccountProvider.Microsoft, AccountProvider.Google]) switch
        {
            AccountProvider.Microsoft => Web("https://outlook.office.com/people/", WidgetLaunchTitle.Contacts),
            AccountProvider.Google => Web("https://contacts.google.com/", WidgetLaunchTitle.Contacts),
            _ => null,
        };

    /// <summary>[S-1928] Microsoft To Do (the app when installed, else the web), Google Tasks on the web; a list on this PC has no app.</summary>
    private static WidgetLaunchTarget? Tasks(TasksConfig tasks, WidgetLaunchContext context)
    {
        if (tasks.Source != TaskSource.Account)
        {
            return null;
        }

        IReadOnlyList<string> ids = string.IsNullOrWhiteSpace(tasks.AccountId) ? [] : [tasks.AccountId];

        return Provider(ids, context.Accounts, AccountFeatures.Tasks, []) switch
        {
            AccountProvider.Microsoft when context.HasProtocol(ToDoProtocol)
                => new WidgetLaunchTarget(WidgetLaunchMethod.Protocol, ToDoProtocol + ":", WidgetLaunchTitle.Tasks),
            AccountProvider.Microsoft => Web("https://to-do.office.com/tasks/", WidgetLaunchTitle.Tasks),
            AccountProvider.Google => Web("https://tasks.google.com/", WidgetLaunchTitle.Tasks),
            _ => null,
        };
    }

    /// <summary>[S-1929] A folder the widget shows, in File Explorer.</summary>
    private static WidgetLaunchTarget? Folder(string? path)
        => LaunchTargets.Classify(path) is { Kind: LaunchKind.LocalPath or LaunchKind.ShellNamespace } folder
            ? new WidgetLaunchTarget(WidgetLaunchMethod.Folder, folder.Value, WidgetLaunchTitle.Folder)
            : null;

    /// <summary>
    /// The provider to open: of the accounts the widget reads (all with the feature when it names
    /// none), the first whose provider is in <paramref name="preferred"/>, else the first at all.
    /// Null with no such account.
    /// </summary>
    private static AccountProvider? Provider(
        IReadOnlyList<string> ids, IReadOnlyList<LaunchAccount> accounts, AccountFeatures feature, IReadOnlyList<AccountProvider> preferred)
    {
        var chosen = ids.Count == 0
            ? accounts.Where(a => (a.Provider.Features() & feature) != 0).ToList()
            : ids.Select(id => accounts.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal)))
                .OfType<LaunchAccount>()
                .Where(a => (a.Provider.Features() & feature) != 0)
                .ToList();

        if (chosen.Count == 0)
        {
            return null;
        }

        return chosen.FirstOrDefault(a => preferred.Contains(a.Provider))?.Provider ?? chosen[0].Provider;
    }

    private static bool IsHttpsPage(string url)
        => LaunchTargets.Classify(url) is { Kind: LaunchKind.Web } web
            && web.Value.StartsWith("https://", StringComparison.Ordinal)
            && string.Equals(web.Value, url, StringComparison.Ordinal);

    /// <summary>MSN's market for a UI language.</summary>
    private static string Market(Locale locale) => locale switch
    {
        Locale.De => "de-de",
        Locale.Es => "es-es",
        Locale.Fr => "fr-fr",
        Locale.It => "it-it",
        Locale.Ja => "ja-jp",
        Locale.Pl => "pl-pl",
        Locale.Pt => "pt-br",
        Locale.Zh => "zh-cn",
        _ => "en-us",
    };

    /// <summary>A place name as one URL path segment: letters and digits kept, every run of anything else one hyphen.</summary>
    private static string Slug(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return Uri.EscapeDataString(slug.Length == 0 ? "here" : slug);
    }
}

/// <summary>What an app user model id names.</summary>
public enum MediaAppKind
{
    Invalid = 0,

    /// <summary><c>PackageFamilyName!AppId</c>: a Store / MSIX app.</summary>
    Packaged = 1,

    /// <summary>A desktop app's own id, usually its exe (<c>Spotify.exe</c>, <c>Chrome</c>).</summary>
    Desktop = 2,
}

/// <summary>
/// [S-1926] The app of a media session, from the <c>SourceAppUserModelId</c> another process
/// reported: untrusted text, so only a plain id passes — ASCII letters, digits, <c>. - _</c> and one
/// <c>!</c> for a packaged app — and nothing longer than Windows allows an id to be.
/// </summary>
public static class MediaApps
{
    /// <summary>The longest app user model id Windows accepts.</summary>
    public const int MaxLength = 128;

    public static MediaAppKind KindOf(string? appId)
    {
        var id = appId?.Trim() ?? string.Empty;

        if (id.Length is 0 or > MaxLength
            || !id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or '!')
            || id.Contains("..", StringComparison.Ordinal))
        {
            return MediaAppKind.Invalid;
        }

        var bang = id.IndexOf('!', StringComparison.Ordinal);
        if (bang < 0)
        {
            return id[0] == '.' ? MediaAppKind.Invalid : MediaAppKind.Desktop;
        }

        var family = id[..bang];
        var app = id[(bang + 1)..];
        var underscore = family.IndexOf('_', StringComparison.Ordinal);

        return app.Length > 0 && !app.Contains('!', StringComparison.Ordinal) && underscore > 0 && underscore < family.Length - 1
            ? MediaAppKind.Packaged
            : MediaAppKind.Invalid;
    }

    /// <summary>
    /// The process name a desktop app's id points at (<c>Spotify.exe</c> → <c>Spotify</c>), or null
    /// for a packaged or refused id.
    /// </summary>
    public static string? ProcessName(string? appId)
    {
        if (KindOf(appId) != MediaAppKind.Desktop)
        {
            return null;
        }

        var id = appId!.Trim();
        return id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && id.Length > 4 ? id[..^4] : id;
    }

    /// <summary>The Start menu entry for an id, which the shell activates: <c>shell:AppsFolder\{id}</c>.</summary>
    public static string? AppsFolderTarget(string? appId)
        => KindOf(appId) == MediaAppKind.Invalid ? null : @"shell:AppsFolder\" + appId!.Trim();
}
