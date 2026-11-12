using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Widgets;

/// <summary>[S-11407] Which of the meeting atoms' states is on screen.</summary>
public enum MeetingScreen
{
    Loading,
    NoAccounts,
    AccountsGone,
    Problem,
    Ready,
}

/// <summary>
/// [S-11407] The meetings ahead as the meeting atoms read them (Next meeting and After this): one load
/// of the calendars' seam (<see cref="ICalendarData"/>: the offline store and the shared sync) for the
/// window <see cref="NextMeetingRules.Window"/> gives, one at a time (one asked for meanwhile runs after
/// it), its outcome as a screen, and the meeting shown (<see cref="NextMeetingRules.Pick"/>) and those
/// after it (<see cref="NextMeetingRules.After"/>) from the same events, so the two atoms agree to the
/// meeting. Pure: the views own one each and draw what it says after <see cref="Changed"/>.
/// </summary>
public sealed class MeetingFeed
{
    private const string Scope = "meetings";

    private readonly Func<CalendarConfig> _config;
    private ICalendarData? _data;
    private bool _refreshing;
    private bool _again;
    private bool _stopped;

    /// <param name="config">The widget's config as it is now (its accounts and calendars).</param>
    public MeetingFeed(Func<CalendarConfig> config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    /// <summary>A load has finished, or the data changed state: draw again.</summary>
    public event Action? Changed;

    /// <summary>[S-2050] The clock the window and the pick read: the real one, or a preview's sample moment.</summary>
    public Func<DateTimeOffset> Clock { get; set; } = static () => DateTimeOffset.Now;

    public MeetingScreen Screen { get; private set; } = MeetingScreen.Loading;

    /// <summary>The events of the last load (a preview may replace them).</summary>
    public IReadOnlyList<CalendarEvent> Events { get; set; } = [];

    /// <summary>Each account's provider, for opening an event in its calendar ([S-1268]).</summary>
    public IReadOnlyDictionary<string, ProviderId> Providers { get; private set; } = new Dictionary<string, ProviderId>();

    /// <summary>The problem's sentence in the current language (<see cref="MeetingScreen.Problem"/>).</summary>
    public Func<string> ProblemText { get; private set; } = static () => string.Empty;

    /// <summary>The problem is one only the user can mend on the Accounts page.</summary>
    public bool ProblemOffersAccounts { get; private set; }

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
        _stopped = true;
        Use(null);
    }

    /// <summary>Start again after <see cref="Stop"/>.</summary>
    public void Start() => _stopped = false;

    private void OnDataChanged() => _ = RefreshAsync(force: false);

    /// <summary>One load at a time; one asked for meanwhile runs after it. Never throws: a failure is the problem screen.</summary>
    public async Task RefreshAsync(bool force)
    {
        if (_stopped)
        {
            return;
        }

        if (_refreshing)
        {
            _again = true;
            return;
        }

        if (_data is not { } data)
        {
            Screen = MeetingScreen.NoAccounts;
            Changed?.Invoke();
            return;
        }

        var (first, last) = NextMeetingRules.Window(DateOnly.FromDateTime(Clock().LocalDateTime));
        _refreshing = true;

        try
        {
            var slice = await data.LoadAsync(_config(), first, last, force, CancellationToken.None).ConfigureAwait(true);
            if (_stopped)
            {
                return;
            }

            Take(slice);
        }
        catch (Exception ex)
        {
            // Reached from a ticker callback and a menu click: nothing above may see it.
            Log.Error(Scope, "could not read the calendar", ex);
            Screen = MeetingScreen.Problem;
            ProblemText = static () => Str.WidgetsA_Calendar_ReadFailed.Text();
            ProblemOffersAccounts = false;
        }
        finally
        {
            _refreshing = false;
        }

        Changed?.Invoke();

        if (_again && !_stopped)
        {
            _again = false;
            await RefreshAsync(force: false).ConfigureAwait(true);
        }
    }

    private void Take(CalendarSlice slice)
    {
        switch (slice.State)
        {
            case CalendarSliceState.NoAccounts:
                Screen = MeetingScreen.NoAccounts;
                return;

            case CalendarSliceState.AccountsGone:
                Screen = MeetingScreen.AccountsGone;
                return;
        }

        if (!slice.AnyData && slice.Problems.Count > 0)
        {
            var worst = slice.Problems.OrderByDescending(p => SyncProblems.NeedsUser(p.Problem)).First();
            Screen = MeetingScreen.Problem;
            ProblemText = () => SyncProblems.Message(worst.Problem, SyncSubject.Calendar, Strings.Current);
            ProblemOffersAccounts = SyncProblems.NeedsUser(worst.Problem);
            return;
        }

        Screen = MeetingScreen.Ready;
        Events = slice.Events;
        Providers = slice.Providers;
    }
}
