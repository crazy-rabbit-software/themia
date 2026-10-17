using System.Globalization;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Core.Widgets;

namespace Themia.Core.Sync;

/// <param name="AccountIds">The widget's accounts; empty means every mail account ([S-541]).</param>
public sealed record MailInterest(IReadOnlyList<string> AccountIds, MailFolder Folder, int Count);

/// <param name="AccountIds">Empty means every calendar account ([S-562]).</param>
/// <param name="Calendars">The widget's calendar choice per account ([S-567]).</param>
public sealed record CalendarInterest(IReadOnlyList<string> AccountIds, IReadOnlyDictionary<string, List<string>> Calendars, bool Birthdays);

/// <param name="AccountIds">Empty means every contacts account ([S-580]).</param>
public sealed record ContactsInterest(IReadOnlyList<string> AccountIds);

/// <param name="AccountId">Empty means the first task account ([S-590]).</param>
/// <param name="ListId">A list, Today, or empty for the account's default list.</param>
public sealed record TaskInterest(string AccountId, string ListId, bool ShowCompleted, TimeSpan Interval);

public sealed record FeedInterest(string Url, int MaxItems, TimeSpan Interval);

/// <summary>[S-2663] A weather widget's place and scale.</summary>
/// <param name="ForecastDays">The forecast's day count, or null for the conditions now (WeatherNow).</param>
public sealed record WeatherInterest(double Latitude, double Longitude, TemperatureUnit Unit, int? ForecastDays, TimeSpan Interval);

/// <summary>[S-2663] A Stocks widget's instrument and chart: everything that changes the quote asked for.</summary>
public sealed record QuoteInterest(string Symbol, QuoteMarket Market, QuoteRange Range, bool PrePost, TimeSpan Interval);

/// <summary>
/// [S-2608] What the sync services keep on the shelf: the needs of every widget in every view, shown
/// or not. A view the user switches to is already synced, so switching draws and asks nothing. The
/// cost is bounded by what the views hold, and it stops with the desktop: the cadence rides the shared
/// ticker, which stops while the desktop is hidden.
/// </summary>
public sealed record SyncInterests(
    IReadOnlyList<MailInterest> Mail,
    IReadOnlyList<CalendarInterest> Calendar,
    IReadOnlyList<ContactsInterest> Contacts,
    IReadOnlyList<TaskInterest> Tasks,
    IReadOnlyList<FeedInterest> Feeds,
    IReadOnlyList<WeatherInterest> Weather,
    IReadOnlyList<QuoteInterest> Quotes)
{
    public static SyncInterests None { get; } = new([], [], [], [], [], [], []);

    /// <summary>
    /// [S-3782] How often the signed-in accounts' mail, calendars and task lists are asked: one
    /// cadence for all of them, from Settings › Widgets.
    /// </summary>
    public TimeSpan AccountInterval { get; init; } = UpdateCadence.Interval(new RefreshPreferences(), UpdateSource.Accounts);

    /// <summary>[S-5101] Every air quality widget's place.</summary>
    public IReadOnlyList<AirInterest> Air { get; init; } = [];

    /// <summary>[S-5121] Every rain widget's place.</summary>
    public IReadOnlyList<RainInterest> Rain { get; init; } = [];

    /// <summary>[S-5147] The ECB table, when any exchange-rates widget is in a view.</summary>
    public RatesInterest? Rates { get; init; }

    /// <summary>
    /// Everything that changes what is synced, as one string: the state changes sixty times a second
    /// during a drag, and the services only need to hear when this does.
    /// </summary>
    public string Key()
    {
        var text = new System.Text.StringBuilder();
        text.Append("a|").Append(AccountInterval.Ticks).Append('#');

        foreach (var m in Mail)
        {
            text.Append("m|").AppendJoin(',', m.AccountIds).Append('|').Append(m.Folder.Wire()).Append('|').Append(m.Count).Append('#');
        }

        foreach (var c in Calendar)
        {
            text.Append("c|").AppendJoin(',', c.AccountIds).Append('|').Append(c.Birthdays).Append('|');
            foreach (var (account, ids) in c.Calendars.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                text.Append(account).Append('=').AppendJoin(',', ids).Append(';');
            }

            text.Append('#');
        }

        foreach (var p in Contacts)
        {
            text.Append("p|").AppendJoin(',', p.AccountIds).Append('#');
        }

        foreach (var t in Tasks)
        {
            text.Append("t|").Append(t.AccountId).Append('|').Append(t.ListId).Append('|').Append(t.ShowCompleted).Append('|').Append(t.Interval.Ticks).Append('#');
        }

        foreach (var f in Feeds)
        {
            text.Append("f|").Append(f.Url).Append('|').Append(f.MaxItems).Append('|').Append(f.Interval.Ticks).Append('#');
        }

        foreach (var w in Weather)
        {
            text.Append("w|").Append(w.Latitude.ToString("R", CultureInfo.InvariantCulture))
                .Append(',').Append(w.Longitude.ToString("R", CultureInfo.InvariantCulture))
                .Append('|').Append(w.Unit).Append('|').Append(w.ForecastDays).Append('|').Append(w.Interval.Ticks).Append('#');
        }

        foreach (var q in Quotes)
        {
            text.Append("q|").Append(q.Symbol).Append('|').Append(q.Market).Append('|').Append(q.Range).Append('|').Append(q.PrePost).Append('|').Append(q.Interval.Ticks).Append('#');
        }

        foreach (var a in Air)
        {
            text.Append("aq|").Append(a.Latitude.ToString("R", CultureInfo.InvariantCulture))
                .Append(',').Append(a.Longitude.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(a.Interval.Ticks).Append('#');
        }

        foreach (var r in Rain)
        {
            text.Append("rn|").Append(r.Latitude.ToString("R", CultureInfo.InvariantCulture))
                .Append(',').Append(r.Longitude.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(r.Interval.Ticks).Append('#');
        }

        if (Rates is { } rates)
        {
            text.Append("fx|").Append(rates.Interval.Ticks).Append('#');
        }

        return text.ToString();
    }

    public static SyncInterests From(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // [S-3770] [S-3780] The shared unit and cadences, from the state itself rather than from what
        // the app last put in force, so the sync follows a change on the same state change.
        var interests = new InterestsBuilder(state.Preferences.Refresh, RegionFormat.For(state.Preferences.Region).Temperature);
        foreach (var widget in state.Views.SelectMany(v => v.Widgets))
        {
            // [S-6906] Each widget says what it wants; the host names none.
            (widget.Config as IInterests)?.AddInterests(interests);
        }

        return interests.Build();
    }

    /// <summary>
    /// [S-2663] What a weather widget asks for, or null when it can ask nothing (no place: the
    /// widget's own "set up" state, never a request). The widget and the sync use this one rule.
    /// </summary>
    public static WeatherInterest? For(WeatherConfig config)
        => For(config, RegionFormat.Current.Temperature, UpdateCadence.Interval(UpdateSource.Weather));

    /// <summary>[S-2663] <see cref="For(WeatherConfig)"/> for a given scale and cadence ([S-3770], [S-3780]).</summary>
    public static WeatherInterest? For(WeatherConfig config, TemperatureUnit unit, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!WebInput.AreCoordinates(config.Latitude, config.Longitude))
        {
            return null;
        }

        // [S-5101] [S-5121] Only the two weather widgets ask for weather; their siblings ask their own.
        if (config is not (WeatherNowConfig or WeatherForecastConfig))
        {
            return null;
        }

        int? days = config is WeatherForecastConfig forecast ? forecast.EffectiveDays : null;
        return new WeatherInterest(config.Latitude!.Value, config.Longitude!.Value, unit, days, interval);
    }

    /// <summary>[S-2663] What a Stocks widget asks for, or null without a usable symbol.</summary>
    public static QuoteInterest? For(StocksConfig config) => For(config, UpdateCadence.Interval(UpdateSource.Markets));

    /// <summary>[S-2663] <see cref="For(StocksConfig)"/> at a given cadence ([S-3780]).</summary>
    public static QuoteInterest? For(StocksConfig config, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.EffectiveSymbol is { Length: > 0 } symbol
            ? new QuoteInterest(symbol, config.Market, config.Range, config.WantsPrePost, interval)
            : null;
    }
}
