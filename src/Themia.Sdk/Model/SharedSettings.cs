using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>
/// [S-11750] One setting that several kinds hold over one config contract (a place, the accounts, a
/// drive, a span): read off any config that has it, compared between two, and copied from one onto
/// another. What is shared is decided by the contract, never by a line's words: a group's page shows
/// it once and sets it on every member that holds it ([S-11751]). It stays each widget's own setting
/// ([S-3590]): the group's row is the same setting set on several widgets at once, not a second home.
/// </summary>
public abstract class SharedSetting
{
    protected SharedSetting(string key) => Key = key;

    /// <summary>The setting's name, the same in every language: what its lines are marked with ([S-11753]).</summary>
    public string Key { get; }

    /// <summary>Whether <paramref name="config"/> holds the setting.</summary>
    public abstract bool Holds(WidgetConfig config);

    /// <summary>Whether two configs that hold it hold the same value; false where either does not hold it.</summary>
    public abstract bool Same(WidgetConfig a, WidgetConfig b);

    /// <summary>Write <paramref name="from"/>'s value onto <paramref name="to"/>: whether <paramref name="to"/> changed.</summary>
    public abstract bool Copy(WidgetConfig from, WidgetConfig to);

    public override string ToString() => Key;
}

/// <summary>[S-11750] A shared setting over the contract <typeparamref name="T"/>, optionally only where <c>where</c> says.</summary>
public sealed class SharedSetting<T> : SharedSetting
    where T : class
{
    private readonly Func<T, T, bool> _same;
    private readonly Action<T, T> _copy;
    private readonly Func<T, bool>? _where;

    public SharedSetting(string key, Func<T, T, bool> same, Action<T, T> copy, Func<T, bool>? where = null)
        : base(key)
    {
        _same = same ?? throw new ArgumentNullException(nameof(same));
        _copy = copy ?? throw new ArgumentNullException(nameof(copy));
        _where = where;
    }

    public override bool Holds(WidgetConfig config) => config is T held && (_where?.Invoke(held) ?? true);

    public override bool Same(WidgetConfig a, WidgetConfig b)
        => Holds(a) && Holds(b) && _same((T)(object)a, (T)(object)b);

    public override bool Copy(WidgetConfig from, WidgetConfig to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (!Holds(from) || !Holds(to) || Same(from, to))
        {
            return false;
        }

        _copy((T)(object)from, (T)(object)to);
        return true;
    }
}

/// <summary>[S-11751] How a shared setting reads over several configs: how many hold it, and whether they disagree.</summary>
public readonly record struct SharedReading(int Holders, bool Mixed)
{
    /// <summary>Two or more hold it: a group's page shows it once.</summary>
    public bool Shared => Holders >= 2;
}

/// <summary>
/// [S-11750]..[S-11753] The registry of the settings several kinds share, each over its contract, and
/// the rules over a set of configs: which are shared (held by two or more), whether they agree, and
/// writing one value to every config that holds it. Pure; the host runs a write as one mutation.
/// </summary>
public static class SharedSettings
{
    /// <summary>[S-2848] [S-2850] The place of every widget about one (the weather family, the sky): Use my location, the place and its name.</summary>
    public static SharedSetting<IPlaced> Place { get; } = new(
        "place",
        (a, b) => a.UseDeviceLocation == b.UseDeviceLocation
            && (a.UseDeviceLocation || (a.Latitude == b.Latitude && a.Longitude == b.Longitude)),
        (from, to) =>
        {
            to.Latitude = from.Latitude;
            to.Longitude = from.Longitude;
            to.PlaceName = from.PlaceName;
            to.UseDeviceLocation = from.UseDeviceLocation;
        });

    /// <summary>[S-561] [S-567] The calendar widgets' accounts and, of each, the calendars shown and their colours.</summary>
    public static SharedSetting<CalendarConfig> CalendarAccounts { get; } = new(
        "calendarAccounts",
        (a, b) => a.AccountIds.SequenceEqual(b.AccountIds, StringComparer.Ordinal)
            && SameLists(a.Calendars, b.Calendars)
            && SameMap(a.CalendarColors, b.CalendarColors),
        (from, to) =>
        {
            to.AccountIds = [.. from.AccountIds];
            to.Calendars = from.Calendars.ToDictionary(p => p.Key, p => p.Value.ToList(), StringComparer.Ordinal);
            to.CalendarColors = new Dictionary<string, string>(from.CalendarColors, StringComparer.Ordinal);
        });

    /// <summary>[S-541] The mail widgets' accounts.</summary>
    public static SharedSetting<IChoosesAccounts> MailAccounts { get; } = Accounts("mailAccounts", HostFeatures.Mail);

    /// <summary>[S-7063] The contacts widgets' accounts.</summary>
    public static SharedSetting<IChoosesAccounts> ContactAccounts { get; } = Accounts("contactAccounts", HostFeatures.Contacts);

    /// <summary>[S-3321] The screen-time widgets' period.</summary>
    public static SharedSetting<IScreenTimePeriod> ScreenTimePeriod { get; } = new("screenTimePeriod", (a, b) => a.Period == b.Period, (from, to) => to.Period = from.Period);

    /// <summary>[S-1822] The disk activity atoms' drive: one, or every drive together.</summary>
    public static SharedSetting<IDiskScope> DiskScope { get; } = new(
        "diskScope",
        (a, b) => a.Scope == b.Scope && string.Equals(a.Drive ?? string.Empty, b.Drive ?? string.Empty, StringComparison.OrdinalIgnoreCase),
        (from, to) =>
        {
            to.Scope = from.Scope;
            to.Drive = from.Drive ?? string.Empty;
        });

    /// <summary>[S-11752] The drive space atoms' drive.</summary>
    public static SharedSetting<IDriveChoice> Drive { get; } = new(
        "drive",
        (a, b) => string.Equals(a.Drive ?? string.Empty, b.Drive ?? string.Empty, StringComparison.OrdinalIgnoreCase),
        (from, to) => to.Drive = from.Drive ?? string.Empty);

    /// <summary>[S-2508] The ping atoms' host.</summary>
    public static SharedSetting<IPingHost> PingHost { get; } = new(
        "pingHost",
        (a, b) => string.Equals(PingHosts.Effective(a), PingHosts.Effective(b), StringComparison.OrdinalIgnoreCase),
        (from, to) => to.Host = from.Host);

    /// <summary>[S-433] The GPU atoms' adapter.</summary>
    public static SharedSetting<IGpuAdapter> GpuAdapter { get; } = new("gpuAdapter", (a, b) => a.Adapter == b.Adapter, (from, to) => to.Adapter = from.Adapter);

    /// <summary>[S-1121] How much history the history atoms' charts span.</summary>
    public static SharedSetting<ISpansHistory> HistorySpan { get; } = new(
        "historySpan",
        (a, b) => a.GraphSpanSeconds == b.GraphSpanSeconds,
        (from, to) => to.GraphSpanSeconds = from.GraphSpanSeconds);

    /// <summary>[S-5143] The exchange-rate atoms' pair.</summary>
    public static SharedSetting<ExchangePairConfig> ExchangePair { get; } = new(
        "exchangePair",
        (a, b) => a.BaseCurrency == b.BaseCurrency && a.QuoteCurrency == b.QuoteCurrency,
        (from, to) =>
        {
            to.BaseCurrency = from.BaseCurrency;
            to.QuoteCurrency = from.QuoteCurrency;
        });

    /// <summary>[S-3790] The stock atoms' symbol: its market and its name with it.</summary>
    public static SharedSetting<StockQuoteConfig> StockSymbol { get; } = new(
        "stockSymbol",
        (a, b) => string.Equals(a.Symbol, b.Symbol, StringComparison.Ordinal) && a.Market == b.Market,
        (from, to) =>
        {
            to.Symbol = from.Symbol;
            to.Market = from.Market;
            to.DisplayName = from.DisplayName;
        });

    /// <summary>[S-2778] [S-1308] The stock atoms' period and whether it asks for extended hours.</summary>
    public static SharedSetting<StockQuoteConfig> StockTimeframe { get; } = new(
        "stockTimeframe",
        (a, b) => a.Range == b.Range && a.ShowExtendedHours == b.ShowExtendedHours,
        (from, to) =>
        {
            to.Range = from.Range;
            to.ShowExtendedHours = from.ShowExtendedHours;
        });

    /// <summary>[S-2449] A one-readout widget's Size.</summary>
    public static SharedSetting<IReadoutSized> ReadoutSize { get; } = new(
        "readoutSize",
        (a, b) => ReadoutSizes.Normalise(a.Size) == ReadoutSizes.Normalise(b.Size),
        (from, to) => to.Size = ReadoutSizes.Normalise(from.Size));

    /// <summary>[S-2440] How many rows a list shows.</summary>
    public static SharedSetting<IKitRows> ListRows { get; } = new("listRows", (a, b) => a.Rows == b.Rows, (from, to) => to.Rows = from.Rows);

    /// <summary>[S-2440] How many lines each row of a list has.</summary>
    public static SharedSetting<IKitRows> ListLines { get; } = new("listLines", (a, b) => a.Lines == b.Lines, (from, to) => to.Lines = from.Lines, rows => rows.OffersLines);

    /// <summary>[S-11400] How many columns a list's rows stand in.</summary>
    public static SharedSetting<IKitColumns> ListColumns { get; } = new("listColumns", (a, b) => a.Columns == b.Columns, (from, to) => to.Columns = from.Columns);

    /// <summary>[S-1464] When the music atoms paint nothing.</summary>
    public static SharedSetting<IHidesWhenIdle> HideWhenIdle { get; } = new("hideWhenIdle", (a, b) => a.HideWhenIdle == b.HideWhenIdle, (from, to) => to.HideWhenIdle = from.HideWhenIdle);

    /// <summary>[S-11752] The sound atoms' output device.</summary>
    public static SharedSetting<IAudioOutput> AudioOutput { get; } = new(
        "audioOutput",
        (a, b) => string.Equals(a.DeviceId, b.DeviceId, StringComparison.Ordinal),
        (from, to) =>
        {
            to.DeviceId = from.DeviceId;
            to.DeviceName = from.DeviceName;
        });

    /// <summary>[S-11750] Every shared setting, in the order a group's page shows them: what a widget is about first, then how it shows it.</summary>
    public static IReadOnlyList<SharedSetting> All { get; } =
    [
        Place, CalendarAccounts, MailAccounts, ContactAccounts, DiskScope, Drive, PingHost, GpuAdapter, AudioOutput,
        ExchangePair, StockSymbol, StockTimeframe, ScreenTimePeriod, HistorySpan, ReadoutSize, ListRows, ListLines, ListColumns, HideWhenIdle,
    ];

    /// <summary>The shared setting named <paramref name="key"/>, or null.</summary>
    public static SharedSetting? Find(string? key) => All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));

    /// <summary>[S-11751] How <paramref name="setting"/> reads over <paramref name="configs"/>: how many hold it, and whether any two differ.</summary>
    public static SharedReading Read(SharedSetting setting, IEnumerable<WidgetConfig> configs)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(configs);

        WidgetConfig? first = null;
        var holders = 0;
        var mixed = false;
        foreach (var config in configs)
        {
            if (!setting.Holds(config))
            {
                continue;
            }

            holders++;
            if (first is null)
            {
                first = config;
            }
            else if (!setting.Same(first, config))
            {
                mixed = true;
            }
        }

        return new SharedReading(holders, mixed);
    }

    /// <summary>[S-11751] The shared settings two or more of <paramref name="configs"/> hold, in <see cref="All"/>'s order.</summary>
    public static IReadOnlyList<SharedSetting> Among(IReadOnlyList<WidgetConfig> configs)
    {
        ArgumentNullException.ThrowIfNull(configs);
        return [.. All.Where(s => configs.Count(s.Holds) >= 2)];
    }

    /// <summary>
    /// [S-11751] Set <paramref name="from"/>'s value of <paramref name="setting"/> on every one of
    /// <paramref name="configs"/> that holds it (the others are left alone): how many changed. A value
    /// the configs disagreed on is the one value they all hold after.
    /// </summary>
    public static int Spread(SharedSetting setting, WidgetConfig from, IEnumerable<WidgetConfig> configs)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(configs);
        return configs.Count(config => !ReferenceEquals(config, from) && setting.Copy(from, config));
    }

    private static SharedSetting<IChoosesAccounts> Accounts(string key, FeatureId feature) => new(
        key,
        (a, b) => a.ChosenAccounts.SequenceEqual(b.ChosenAccounts, StringComparer.Ordinal),
        (from, to) => to.ChosenAccounts = [.. from.ChosenAccounts],
        held => held.AccountFeature == feature);

    private static bool SameLists(Dictionary<string, List<string>> a, Dictionary<string, List<string>> b)
        => a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var other) && p.Value.SequenceEqual(other, StringComparer.Ordinal));

    private static bool SameMap(Dictionary<string, string> a, Dictionary<string, string> b)
        => a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var other) && string.Equals(p.Value, other, StringComparison.OrdinalIgnoreCase));
}
