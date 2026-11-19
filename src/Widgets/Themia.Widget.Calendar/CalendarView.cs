using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Office;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Calendar;

namespace Themia.App.Widgets;

/// <summary>
/// [S-560] Events from the user's Microsoft, Google and CalDAV calendars, through Themia's own
/// accounts, with contacts' birthdays beside the events ([S-575]). [S-3640] One view for the
/// calendar's five styles — month, week, day, year and agenda — read from the config; a style change
/// re-draws, and the range, paging unit and keys follow it ([S-6005]).
/// <para>
/// [S-2700] The widget draws only its layout, at every size: a small month is a small month (dots,
/// then bare numbers), never an agenda or a single event in its place. Each layout's fit is Core
/// (<see cref="CalendarFit"/>); the drawing is in the partial files.
/// </para>
/// <para>
/// [S-2701] The view owns no connection and no cache: everything comes through one
/// <see cref="ICalendarData"/> load for the range its layout draws. What it keeps is the anchor day
/// being looked at, the agenda page, and the events and birthdays of the range it last asked for.
/// Polls every five minutes; a context-menu Refresh asks past every cache.
/// </para>
/// </summary>
internal sealed partial class CalendarView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys
{
    /// <inheritdoc />
    public CardOpen? Opens => CalendarModule.Opens(_config.AccountIds, _widgetHost.AccountsWith(HostFeatures.Calendar));

    private const string Scope = "calendar-widget";

    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(5);

    private static readonly string ChevronLeft = char.ConvertFromUtf32(0xE76B);
    private static readonly string ChevronRight = char.ConvertFromUtf32(0xE76C);

    private readonly Grid _content = new();
    private readonly KitStatePanel _state;
    /// <summary>[S-12481] The agenda's state over its rows' top end; the other styles' is on their line.</summary>
    private readonly KitStatus _status;

    /// <summary>[S-569] [S-12481] The period and back and forward, the content's first line (every style but the agenda).</summary>
    private readonly ToolLine _line = new();
    private readonly Border _back;
    private readonly Border _forward;
    private readonly FontIcon _lineProblem = ToolLine.Problem(string.Empty);
    private readonly ProgressRing _lineBusy = ToolLine.Busy();
    private readonly List<IDisposable> _hovers = [];
    private bool _navigable;

    private IWidgetSurface? _surface;
    private ICalendarData? _data;
    private IDisposable? _poll;
    private CalendarWidgetConfig _config = new();
    private DateOnly _anchor = DateOnly.FromDateTime(DateTime.Now);
    private bool _followToday = true;

    /// <summary>[S-2709] The agenda page on screen, and how many the last draw had.</summary>
    private int _agendaPage;
    private int _agendaPages = 1;

    /// <summary>The view's slot, from the card ([S-663]); unmeasured until the first layout pass.</summary>
    private WidgetArea _area;
    private Screen _screen = Screen.Loading;
    /// <summary>The problem in words, worded when shown so a language switch reaches it ([S-1109]).</summary>
    private Func<string> _problemText = () => string.Empty;
    private bool _problemOffersAccounts;
    /// <summary>[S-3651] Each account whose last sync could not improve on what is held: the one stale rule ([S-12481]).</summary>
    private IReadOnlyList<Themia.Sdk.Kit.KitOutcome> _outcomes = [];
    private bool _outcomesNeedUser;
    private IReadOnlyList<CalendarEvent> _events = [];
    private IReadOnlyList<Birthday> _birthdays = [];
    private IReadOnlyList<CalendarTarget> _targets = [];
    private IReadOnlyDictionary<string, ProviderId> _providers = new Dictionary<string, ProviderId>();
    private string _requestKey = string.Empty;
    private bool _refreshing;
    private bool _again;
    private bool _againForced;
    private bool _detached;

    /// <summary>[S-6913] What the view asks of the host: the launcher, ⚙ › Accounts.</summary>
    private readonly IWidgetHost _widgetHost;

    public CalendarView(IWidgetHost host)
    {
        _widgetHost = host;
        _state = new(Glyphs.Calendar, host) { Visibility = Visibility.Collapsed };
        _status = new KitStatus(host);

        // [S-569] [S-12481] The period's line: its title, then the problem glyph, the ring, back and forward.
        _line.Margin = Family.SideInset with { Bottom = FamilySpace.HeaderGap };
        _line.Parts.Add(LinePartKind.Problem, _lineProblem);
        _line.Parts.Add(LinePartKind.Busy, _lineBusy);
        _back = _line.Parts.Add(LinePartKind.Button, ToolLine.Button(ChevronLeft, CalendarStr.WidgetsA_Calendar_Previous.Text(), () => Navigate(-1)));
        _forward = _line.Parts.Add(LinePartKind.Button, ToolLine.Button(ChevronRight, CalendarStr.Common_Next.Text(), () => Navigate(1)));
        _lineProblem.Tapped += (_, args) =>
        {
            if (_outcomesNeedUser)
            {
                args.Handled = true;
                _widgetHost.ShowAccounts();
            }
        };

        foreach (var part in new FrameworkElement[] { _lineProblem, _lineBusy, _back, _forward })
        {
            _line.Parts.Want(part, false);
        }

        _days = FollowLinks();
        _agenda = NewAgenda(host);
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var rows = RowsHost();
        SetRow(_content, 1);
        SetRow(rows, 1);
        SetRow(_state, 1);
        Children.Add(_line);
        Children.Add(_content);
        Children.Add(rows);
        Children.Add(_state);
        _status.Place(this);
    }

    /// <summary>[S-569] [S-12481] Whether the style shows the period's line: every style but the agenda.</summary>
    private bool ShowsLine => Layout != CalendarStyle.Agenda;

    /// <summary>Which of the widget's states is on screen. Each one needs something different of the user.</summary>
    private enum Screen
    {
        Loading,
        NoAccounts,
        AccountsGone,
        Problem,
        Ready,
    }

    /// <summary>[S-2050] The clock the view reads: the real one, or a preview's sample moment.</summary>
    private Func<DateTimeOffset> _clock = static () => DateTimeOffset.Now;

    private DateOnly Today => DateOnly.FromDateTime(_clock().LocalDateTime);

    private CalendarStyle Layout => _config.Style;

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        Render();
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _config = config as CalendarWidgetConfig ?? new CalendarWidgetConfig();
        UseData(_widgetHost.Office is { } source ? new OfficeCalendarData(source) : null);

        Strings.Changed += OnLanguage;
        _agenda.Attach();

        // [S-3620] The view's links: a day selected there already is shown at once.
        JoinLinks(_surface?.Links);

        // [S-588] The ticker fires once on subscribing, which is the first load.
        _poll = ticker.Subscribe(Poll, () => _ = RefreshAsync(force: false));
    }

    /// <summary>[S-2701] Put a data seam in place, moving the change subscription with it.</summary>
    private void UseData(ICalendarData? data)
    {
        if (_data is not null)
        {
            _data.Changed -= OnAccountsChanged;
        }

        _data = data;

        if (_data is not null)
        {
            _data.Changed += OnAccountsChanged;
        }
    }

    /// <summary>[S-1109] Names, dates and states are worded again from what is in hand; nothing is refetched.</summary>
    private void OnLanguage(Locale locale) => Render();

    public void ApplyConfig(WidgetConfig config)
    {
        var before = Layout;
        _config = config as CalendarWidgetConfig ?? new CalendarWidgetConfig();

        if (Layout != before)
        {
            ChangedStyle();
        }

        // Only a change to what is asked for is worth a round trip; a colour or the agenda count
        // re-draws what is in hand.
        if (RequestKey() != _requestKey)
        {
            _ = RefreshAsync(force: false);
        }

        Render();
    }

    /// <summary>
    /// [S-6005] Another style: the agenda starts on its first page, a dated style anchors on the
    /// selected day (or keeps the anchor), and the view's links hear the new range ([S-3625]).
    /// </summary>
    private void ChangedStyle()
    {
        _agendaPage = 0;
        _agendaJump = null;

        if (_selected is { } day)
        {
            _anchor = CalendarShare.AnchorFor(Layout, _anchor, day);
        }

        _followToday = Layout == CalendarStyle.Agenda || InView(Today);

        if (Layout == CalendarStyle.Agenda)
        {
            _links.Set(LinkTopics.VisibleRange, null);
        }
        else
        {
            PublishRange();
        }
    }

    /// <summary>[S-561] Refresh. [S-3640] The layout is the widget's style, chosen on the strip or the properties menu.</summary>
    /// <summary>
    /// [S-569] The card menu: Today while the view is away from it (the line has room for back and
    /// forward only; the key is T), the agenda's pages back and forward ([S-12481]: it has no line), and
    /// Refresh with the age of what is on screen.
    /// </summary>
    public IReadOnlyList<WidgetAction> ContextActions
    {
        get
        {
            var actions = new List<WidgetAction>(4);
            if (_screen == Screen.Ready && AwayFromToday)
            {
                actions.Add(new(Glyphs.Calendar, CalendarStr.Calendar_Today.Text(), GoToday));
            }

            if (_navigable && !ShowsLine)
            {
                if (_agendaPage > 0)
                {
                    actions.Add(new(ChevronLeft, CalendarStr.WidgetsA_Calendar_Previous.Text(), () => Navigate(-1)));
                }

                if (_agendaPage < _agendaPages - 1)
                {
                    actions.Add(new(ChevronRight, CalendarStr.Common_Next.Text(), () => Navigate(1)));
                }
            }

            actions.Add(Refresh());
            return actions;
        }
    }

    private WidgetAction Refresh() => new(Glyphs.Refresh, Themia.Sdk.Kit.KitFreshness.RefreshLabel(Strings.Current, _outcomes, _clock()), () => _ = RefreshAsync(force: true));

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;
        _poll?.Dispose();
        _poll = null;
        UseData(null);
        LeaveLinks();

        _status.Detach();
        _state.Detach();
        ClearDrawing();
        _agenda.Detach();
    }

    // -----------------------------------------------------------------------------------------
    // Data
    // -----------------------------------------------------------------------------------------

    private void OnAccountsChanged() => _ = RefreshAsync(force: false);

    /// <summary>Everything that changes what is asked: accounts, calendars, birthdays and the range.</summary>
    private string RequestKey()
    {
        var range = Range();
        var calendars = string.Join(";", _config.Calendars.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={string.Join(",", kv.Value)}"));
        return $"{string.Join(",", _config.AccountIds)}|{calendars}|{_config.ShowBirthdays}|{range.First}|{range.Last}";
    }

    /// <summary>
    /// Ask for the range on screen. One at a time: a request arriving while one is out is
    /// remembered and run after it, so the widget can never settle on a range it has moved away from.
    /// </summary>
    private async Task RefreshAsync(bool force)
    {
        if (_detached)
        {
            return;
        }

        if (_refreshing)
        {
            _again = true;
            _againForced |= force;
            return;
        }

        // [S-569] Until the user pages away, the widget stays on today, across midnight too.
        if (_followToday)
        {
            _anchor = Today;
        }

        var data = _data;
        _requestKey = RequestKey();

        if (data is null)
        {
            _screen = Screen.NoAccounts;
            Render();
            return;
        }

        var range = Range();
        _refreshing = true;
        UpdateState();

        try
        {
            var slice = await data.LoadAsync(_config, range.First, range.Last, force, CancellationToken.None).ConfigureAwait(true);

            if (_detached)
            {
                return;
            }

            Take(slice);
        }
        catch (Exception ex)
        {
            // Reached from a ticker callback and a menu click: nothing above may see an exception.
            Log.Error(Scope, "could not refresh the calendar", ex);
            _screen = Screen.Problem;
            _problemText = () => Str.WidgetsA_Calendar_ReadFailed.Text();
            _problemOffersAccounts = false;
        }
        finally
        {
            _refreshing = false;
            UpdateState();
        }

        Render();

        if (_again && !_detached)
        {
            var forced = _againForced;
            _again = false;
            _againForced = false;
            await RefreshAsync(forced).ConfigureAwait(true);
        }
    }

    /// <summary>What a load said: the screen it calls for, and the data when there is any.</summary>
    private void Take(CalendarSlice slice)
    {
        switch (slice.State)
        {
            case CalendarSliceState.NoAccounts:
                _screen = Screen.NoAccounts;
                return;

            case CalendarSliceState.AccountsGone:
                _screen = Screen.AccountsGone;
                return;
        }

        _events = slice.Events;
        _birthdays = slice.Birthdays;
        _targets = slice.Targets;
        _providers = slice.Providers;

        var problems = slice.Problems;
        var labels = slice.Labels;
        var worst = problems.OrderByDescending(p => SyncProblems.NeedsUser(p.Problem)).FirstOrDefault();

        if (!slice.AnyData && problems.Count > 0)
        {
            _screen = Screen.Problem;
            var problem = worst.Problem;
            _problemText = () => SyncProblems.Message(problem, SyncSubject.Calendar, Strings.Current);
            _problemOffersAccounts = SyncProblems.NeedsUser(worst.Problem);
            _outcomes = [];
            return;
        }

        // [S-2664] [S-3651] Events from the store the last sync could not improve on say when they
        // are from, in the kit's one stale rule: "Work: No network · updated 10:42".
        _screen = Screen.Ready;
        _outcomes = CalendarFreshness.Outcomes(problems, labels);
        _outcomesNeedUser = problems.Any(p => SyncProblems.NeedsUser(p.Problem));
    }

    /// <summary>
    /// [S-3651] [S-7042] [S-12481] The state, on the period's line (or over the agenda's top end): the problem glyph while events are on screen that some account's
    /// last sync could not improve on, one line per account and problem (a click on it opens Accounts
    /// where only the user can mend it); the ring while a load is out.
    /// </summary>
    private void UpdateState()
    {
        var lines = _screen == Screen.Ready ? Themia.Sdk.Kit.KitFreshness.Lines(Strings.Current, _outcomes, _clock()) : [];
        if (ShowsLine && _navigable)
        {
            _status.Clear();
            _line.Parts.Want(_lineProblem, lines.Count > 0);
            ToolTipService.SetToolTip(_lineProblem, lines.Count > 0 ? string.Join("\n", lines) : null);
            _lineBusy.IsActive = _refreshing;
            _line.Parts.Want(_lineBusy, _refreshing);
            _line.Parts.Want(_back, true);
            _line.Parts.Want(_forward, true);
            return;
        }

        foreach (var part in new FrameworkElement[] { _lineProblem, _lineBusy, _back, _forward })
        {
            _line.Parts.Want(part, false);
        }

        _status.Show(lines, _refreshing, _outcomesNeedUser);
    }

    // -----------------------------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-568] The days the layout covers, which is what is fetched.</summary>
    private (DateOnly First, DateOnly Last) Range() => CalendarRules.ViewRange(Layout, _anchor, Today, _config.Period);

    /// <summary>Whether a day is in the period on screen, so the today button can hide when it would do nothing.</summary>
    private bool InView(DateOnly day) => CalendarRules.InView(Layout, _anchor, Today, _config.Period, day);

    /// <summary>Whether the today button would do anything: today is off screen, or the agenda is past its first page.</summary>
    private bool AwayFromToday => Layout == CalendarStyle.Agenda ? _agendaPage > 0 : !InView(Today);

    /// <summary>[S-569] / [S-2709] One unit back or forward: the layout's period, or an agenda page.</summary>
    private void Navigate(int direction)
    {
        if (Layout == CalendarStyle.Agenda)
        {
            _agendaPage = Math.Clamp(_agendaPage + Math.Sign(direction), 0, Math.Max(0, _agendaPages - 1));
            Render();
            return;
        }

        _anchor = CalendarRules.Step(Layout, _anchor, direction, _config.Period);

        // Paging back to the period holding today picks following it up again.
        _followToday = InView(Today);
        PublishRange();
        Render();
        _ = RefreshAsync(force: false);
    }

    /// <summary>
    /// [S-2710]/[S-806] A key from the shell's keyboard lease (the classic S-31..34), which the
    /// calendar holds once it is clicked. [S-3624] The cursor day is the selected day: Up / Down move
    /// it (and the other calendars on the view with it); Escape (also the lease ending) lets it go.
    /// Returns whether the key was used.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached || _screen != Screen.Ready)
        {
            return false;
        }

        if (key == WidgetKey.Escape)
        {
            // [S-3652] Escape lets the agenda's cursor go as well as the selected day.
            var released = Layout == CalendarStyle.Agenda && _agenda.HandleKey(key);
            if (_selected is null)
            {
                return released;
            }

            Select(null);
            return true;
        }

        // [S-3652] The agenda's cursor keys are its kit list's; paging and today stay the calendar's.
        if (Layout == CalendarStyle.Agenda && CalendarFit.Key(key, Layout) == CalendarKeyCommand.None)
        {
            return _agenda.HandleKey(key);
        }

        var command = CalendarFit.Key(key, Layout);
        switch (command)
        {
            case CalendarKeyCommand.Back:
                Navigate(-1);
                return true;

            case CalendarKeyCommand.Forward:
                Navigate(1);
                return true;

            case CalendarKeyCommand.WeekBack or CalendarKeyCommand.WeekForward:
                Select(CalendarShare.Step(_selected, SelectedInView, _anchor, command == CalendarKeyCommand.WeekBack ? -7 : 7));
                return true;

            case CalendarKeyCommand.Today:
                GoToday();
                return true;

            case CalendarKeyCommand.OpenFirst:
                return OpenFirst();

            default:
                return false;
        }
    }

    /// <summary>
    /// [S-2710] Enter: the selected day's first event (the anchor's when none is on screen; the week's
    /// chosen day's, [S-2284]); the agenda's is its list's ([S-3652]).
    /// </summary>
    private bool OpenFirst()
    {
        var (from, last) = Range();
        var day = Layout == CalendarStyle.Week
            ? CalendarFit.ChosenDay(_selected, from, last.DayNumber - from.DayNumber + 1, Today)
            : SelectedInView ? _selected!.Value : _anchor;
        var first = CalendarRules.DayItems(_events, [], day, TimeZoneInfo.Local).Select(i => i.Event).FirstOrDefault();

        if (first is null)
        {
            return false;
        }

        Open(first);
        return true;
    }

    private void GoToday()
    {
        _anchor = Today;
        _followToday = true;
        _agendaPage = 0;
        PublishRange();
        Render();
        _ = RefreshAsync(force: false);
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    // -----------------------------------------------------------------------------------------
    // Drawing
    // -----------------------------------------------------------------------------------------

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        ClearDrawing();

        var ready = _screen == Screen.Ready;
        // The agenda and the narrow week ([S-2285]) draw their rows themselves; no other style has any.
        if (!ready || Layout is not (CalendarStyle.Agenda or CalendarStyle.Week))
        {
            HideAgenda();
        }
        // [S-568] [S-12481] The period is its line's label; the agenda has no line.
        _line.Label = ready && ShowsLine ? CalendarRules.Title(Layout, _anchor, Today, _config.Period, Strings.Current) : string.Empty;
        UpdateState();

        switch (_screen)
        {
            case Screen.Loading:
                _state.Show(Str.Common_Loading.Text(), offerAccounts: false);
                ShowNavigation(false);
                return;

            case Screen.NoAccounts:
                // [S-1862] The card shows its Set up button over this.
                _state.Hide();
                ShowNavigation(false);
                return;

            case Screen.AccountsGone:
                _state.Show(SyncProblems.Message(SyncProblem.NoAccount, SyncSubject.Calendar, Strings.Current), offerAccounts: true, problem: true);
                ShowNavigation(false);
                return;

            case Screen.Problem:
                _state.Show(_problemText(), _problemOffersAccounts, problem: true);
                ShowNavigation(false);
                return;
        }

        _state.Hide();

        var width = _area.Width;

        // [S-569] [S-12481] The line takes its 24 off the top of the box the style is drawn in.
        var height = _area.Height - (ShowsLine ? CalendarSizing.NavLine : 0);

        switch (Layout)
        {
            case CalendarStyle.Week:
                DrawWeek(width, height);
                break;

            case CalendarStyle.Day:
                DrawDay(width, height);
                break;

            case CalendarStyle.Year:
                DrawYear(width, height);
                break;

            case CalendarStyle.Agenda:
                DrawAgenda(width, height);
                break;

            default:
                DrawMonth(width, height);
                break;
        }

        // After drawing: the agenda only knows its pages once it has fitted them.
        ShowNavigation(true);
    }

    /// <summary>
    /// [S-569] [S-12481] Back and forward: on the period's line in every style but the agenda, whose pages
    /// are the card menu's (and the keys') where there is a page to go to.
    /// </summary>
    private void ShowNavigation(bool ready)
    {
        _navigable = ready;
        UpdateState();
    }

    private void ClearDrawing()
    {
        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();

        // [S-3623] [S-11923] A chip or a day redrawn under the pointer never hears it leave: its hover goes with it.
        _eventElements.Clear();
        _links.Set(LinkTopics.HoveredEvent, null);
        _days.Clear();
        _rowDays.Clear();
        _days.Point(null);
        _content.Children.Clear();
        _content.RowDefinitions.Clear();
        _content.ColumnDefinitions.Clear();
    }

    // ---- shared pieces ------------------------------------------------------------------------

    private bool ShowColors => CalendarRules.ShowColors(_targets, _config.CalendarColors);

    /// <summary>[S-566] An event's calendar colour, resolved at draw time so a settings change repaints at once.</summary>
    /// <summary>[S-566] An event's calendar colour: a data colour ([S-6705]), never a typed one.</summary>
    private static Brush ColorOf(string hex) => Family.Data(hex);

    private Brush ColorOf(CalendarEvent item)
        => ColorOf(CalendarRules.ResolveColor(CalendarRules.ColorKey(item.AccountId, item.CalendarId), item.SourceIndex, _config.CalendarColors));

    /// <summary>
    /// An item's mark: its calendar's colour where colours are shown, the accent otherwise (a chart's
    /// role: the mark is a bar or a dot, never a text); a birthday's is the tertiary role (not a state).
    /// </summary>
    private Brush MarkOf(DayItem item, bool showColors)
        => item.Birthday is not null ? BirthdayBrush() : showColors ? ColorOf(item.Event!) : Accent();

    private static Brush Accent() => Family.Brush(ColourRole.Accent);

    private static Brush BirthdayBrush() => Family.Brush(ColourRole.TextTertiary);

    private void Open(CalendarEvent item)
    {
        ProviderId? provider = _providers.TryGetValue(item.AccountId, out var known) ? known : null;
        if (CalendarLinks.Open(item, provider, provider == HostProviders.Microsoft && _widgetHost.Launcher.OutlookInstalled) is { } link)
        {
            _widgetHost.Launcher.OpenCalendarLink(link);
        }
    }

    private void MakeOpenable(FrameworkElement element, CalendarEvent item)
    {
        Describe(element, item);

        if (Openable(item))
        {
            // [S-2004] The event's click is its own: it never reaches the card's open.
            element.Tapped += (_, args) =>
            {
                args.Handled = true;
                Open(item);
            };
        }
    }

    /// <summary>An event's element: its tooltip, its links across widgets ([S-3623]), and an item to the surface ([S-814]).</summary>
    private void Describe(FrameworkElement element, CalendarEvent item)
    {
        ToolTipService.SetToolTip(element, Describe(item));
        LinkEvent(element, item);
        WidgetItems.Mark(element); // [S-814]
    }

    /// <summary>[S-1268] Whether an event has somewhere to open: its web page, or a Microsoft event (Outlook or its page).</summary>
    private bool Openable(CalendarEvent item)
        => item.WebLink is not null || (_providers.TryGetValue(item.AccountId, out var provider) && provider == HostProviders.Microsoft);

    /// <summary>Everything a chip had to leave out, on hover.</summary>
    private static string Describe(CalendarEvent item)
    {
        var lines = new List<string> { Title(item) };

        if (item.AllDay)
        {
            var days = item.EndDay.DayNumber - item.FirstDay.DayNumber;
            lines.Add(CalendarStr.WidgetsA_Calendar_AllDayOn.Format(("date", days > 1
                ? CalendarRules.Range(item.FirstDay, item.EndDay.AddDays(-1), Strings.Current)
                : Formatting.ShortDate(item.FirstDay))));
        }
        else
        {
            var start = item.Start.ToLocalTime();
            var span = item.End > item.Start
                ? CalendarStr.WidgetsA_Calendar_SpanWithLength.Format(("from", Clock(item.Start)), ("to", Clock(item.End)), ("length", Formatting.Duration(item.End - item.Start)))
                : Clock(item.Start);
            lines.Add($"{Formatting.ShortDate(DateOnly.FromDateTime(start.DateTime))} · {span}");
        }

        if (item.Location is { } place)
        {
            lines.Add(place);
        }

        if (item.Organizer is { } who)
        {
            lines.Add(who);
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>A day's date and everything on it, for the hover of a cell that cannot show it all.</summary>
    private static string DayTooltip(DateOnly day, IReadOnlyList<DayItem> items)
    {
        var lines = new List<string> { CalendarStr.WidgetsA_Clock_Date.Format(("weekday", CalendarText.WeekdayName(day.DayOfWeek)), ("date", Formatting.ShortDate(day))) };

        foreach (var item in items)
        {
            if (item.Birthday is { } b)
            {
                lines.Add(Str.WidgetsA_Calendar_BirthdayOf.Format(("name", BirthdayRules.Label(b, day))));
            }
            else if (item.Event is { } e)
            {
                lines.Add($"{(e.AllDay ? Str.Calendar_AllDay.Text() : Clock(e.Start))}  {Title(e)}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>[S-2702] A weekday as much as its column holds.</summary>
    private static string WeekdayText(DayOfWeek day, WeekdayLabel label) => label switch
    {
        WeekdayLabel.Short => CalendarText.WeekdayShort(day),
        WeekdayLabel.Initials => CalendarText.WeekdayInitial(day),
        WeekdayLabel.Narrow => LocaleFormat.WeekdayName(Strings.Current, day, NameWidth.Narrow),
        _ => string.Empty,
    };

    private static string Title(CalendarEvent item) => string.IsNullOrWhiteSpace(item.Subject) ? Str.Calendar_NoTitle.Text() : item.Subject;

    /// <summary>
    /// [S-2717] Whether a one-line text runs past <paramref name="width"/>: a title that would take a
    /// second line if it had one. Measured as drawn (its step, weight and language).
    /// </summary>
    private static bool Overflows(TextBlock text, double width)
    {
        if (!(width > 0) || string.IsNullOrEmpty(text.Text))
        {
            return false;
        }

        var trimming = text.TextTrimming;
        text.Language = UiLanguage.Tag(Strings.Current);
        text.TextTrimming = TextTrimming.None;
        text.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var wide = text.DesiredSize.Width > width + LayoutMath.Tolerance;
        text.TextTrimming = trimming;
        return wide;
    }

    /// <summary>[S-2717] A title given its second line: it wraps by word and ends in an ellipsis after it.</summary>
    private static void WrapTitle(TextBlock text)
    {
        text.TextWrapping = TextWrapping.WrapWholeWords;
        text.MaxLines = Themia.Sdk.Kit.KitPages.MaxTitleLines;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
    }

    private static string Clock(DateTimeOffset moment) => Formatting.Clock24(moment);
}
