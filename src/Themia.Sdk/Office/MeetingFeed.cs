using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11407] [S-16962] The meetings as the meeting widgets read them (Meeting and Meetings): the office widgets' one
/// feed ([S-7077], <see cref="OfficeFeed{T}"/>) over the calendars' seam (<see cref="ICalendarData"/>: the offline store
/// and the shared sync) for the window <see cref="NextMeetingRules.Window"/> gives, its outcome as the kit's screen, and
/// the meeting shown (<see cref="NextMeetingRules.Pick"/>) and those after it (<see cref="NextMeetingRules.After"/>) from
/// the same events, so the two atoms agree to the meeting. Pure: the views own one each and draw what it says after
/// <see cref="Changed"/>.
/// </summary>
public sealed class MeetingFeed
{
    private readonly Func<CalendarConfig> _config;
    private readonly Func<DateOnly, (DateOnly First, DateOnly Last)> _window;
    private readonly OfficeFeed<CalendarSlice> _feed;
    private ICalendarData? _data;
    private IReadOnlyList<CalendarEvent>? _events;

    /// <param name="config">The widget's config as it is now (its accounts and calendars).</param>
    /// <param name="window">[S-16962] The local days to ask for from today; null: <see cref="NextMeetingRules.Window"/>.</param>
    public MeetingFeed(Func<CalendarConfig> config, Func<DateOnly, (DateOnly First, DateOnly Last)>? window = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _window = window ?? NextMeetingRules.Window;
        _feed = new OfficeFeed<CalendarSlice>(LoadAsync, SyncSubject.Calendar, static locale => Strings.Get(locale, Str.WidgetsA_Calendar_ReadFailed));
        _feed.Changed += () => Changed?.Invoke();
    }

    /// <summary>A load went out or finished, or the data changed state: draw again.</summary>
    public event Action? Changed;

    /// <summary>[S-2050] The clock the window and the pick read: the real one, or a preview's sample moment.</summary>
    public Func<DateTimeOffset> Clock { get; set; } = static () => DateTimeOffset.Now;

    /// <summary>[S-7077] The screen the last load calls for.</summary>
    public KitScreen Screen(Locale locale) => _feed.Screen(locale);

    /// <summary>A load is out.</summary>
    public bool Busy => _feed.Busy;

    /// <summary>[S-7042] The stale rule's outcomes while meetings are on screen.</summary>
    public IReadOnlyList<KitOutcome> Outcomes => _feed.Outcomes;

    /// <summary>The events of the last load (a preview may replace them).</summary>
    public IReadOnlyList<CalendarEvent> Events
    {
        get => _events ?? _feed.Data?.Events ?? [];
        set => _events = value;
    }

    /// <summary>Each account's provider, for opening an event in its calendar ([S-1268]).</summary>
    public IReadOnlyDictionary<string, ProviderId> Providers => _feed.Data?.Providers ?? new Dictionary<string, ProviderId>();

    /// <summary>[S-4821] The meeting that matters now.</summary>
    public CalendarEvent? Shown(DateTimeOffset now) => NextMeetingRules.Pick(Events, now);

    /// <summary>[S-2160] The meetings after the one shown, in order; none when nothing is shown.</summary>
    public IReadOnlyList<CalendarEvent> After(DateTimeOffset now, TimeZoneInfo zone)
        => Shown(now) is { } shown ? NextMeetingRules.After(Events, shown, now, zone) : [];

    /// <summary>The data it reads from now on (null: none, the widget has no calendars to read).</summary>
    public void Use(ICalendarData? data)
    {
        if (_data is not null)
        {
            _data.Changed -= OnDataChanged;
        }

        _data = data;

        if (_data is not null)
        {
            _data.Changed += OnDataChanged;
        }
    }

    /// <summary>Stop: no load starts or reports after this.</summary>
    public void Stop()
    {
        _feed.Stop();
        Use(null);
    }

    /// <summary>Start again after <see cref="Stop"/>.</summary>
    public void Start() => _feed.Start();

    private void OnDataChanged() => _ = RefreshAsync(force: false);

    /// <summary>One load at a time; one asked for meanwhile runs after it. Never throws: a failure is the problem screen.</summary>
    public Task RefreshAsync(bool force) => _feed.RefreshAsync(force);

    private async Task<OfficeLoad<CalendarSlice>> LoadAsync(bool force, CancellationToken token)
    {
        if (_data is not { } data)
        {
            return OfficeLoad<CalendarSlice>.Of(OfficeLoadState.NoAccounts);
        }

        var (first, last) = _window(DateOnly.FromDateTime(Clock().LocalDateTime));
        var slice = await data.LoadAsync(_config(), first, last, force, token).ConfigureAwait(true);
        return slice.AsLoad();
    }
}
