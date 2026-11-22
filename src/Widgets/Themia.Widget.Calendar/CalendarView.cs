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

    private static readonly string ChevronLeft = Glyphs.ChevronLeft;
    private static readonly string ChevronRight = Glyphs.ChevronRight;

    private readonly Grid _content = new();
    private readonly KitStatePanel _state;
    /// <summary>[S-12481] The agenda's state over its rows' top end; the other styles' is on their line.</summary>
    private readonly KitStatus _status;

    /// <summary>
    /// Whether the period's line or a state's row stands over the face (the face then starts right under it, not on the
    /// card's top edge): every style but the agenda has its line ([S-569]).
    /// </summary>
    private bool _stated;

    /// <summary>[S-569] [S-12481] The period and back and forward, the content's first line (every style but the agenda).</summary>
    private readonly ToolLine _line = new();
    private readonly Border _back;
    private readonly Border _forward;
    private readonly FontIcon _lineProblem = ToolLine.Problem(string.Empty);
    private readonly FrameworkElement _lineBusy = ToolLine.Busy();
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
    /// <summary>
    /// [S-7077] The office widgets' one feed: the loads one at a time, the screen they call for, the data of the last that
    /// had any, and the stale rule's outcomes ([S-3651], [S-12481]).
    /// </summary>
    private readonly OfficeFeed<CalendarSlice> _feed;
    private string _requestKey = string.Empty;
    private bool _detached;

    /// <summary>[S-6913] What the view asks of the host: the launcher, settings › Accounts.</summary>
    private readonly IWidgetHost _widgetHost;

    public CalendarView(IWidgetHost host)
    {
        _widgetHost = host;
        _feed = new OfficeFeed<CalendarSlice>(LoadAsync, SyncSubject.Calendar, static locale => Strings.Get(locale, Str.WidgetsA_Calendar_ReadFailed));
        _feed.Changed += OnFeed;
        _state = new(Glyphs.Calendar, host) { Visibility = Visibility.Collapsed };
        _status = new KitStatus(host);

        // [S-569] [S-12481] The period's line: its title, then the problem glyph, the ring, back and forward.
        _line.Margin = Family.SideInset;
        _line.Parts.ButtonsPaired = true; // [S-16232] back and forward give way together
        _line.Parts.NameGivesWayFirst = true; // [S-16232] the period's name is cut before the arrows go
        _line.Parts.Add(LinePartKind.Problem, _lineProblem);
        _line.Parts.Add(LinePartKind.Busy, _lineBusy);
        _back = _line.Parts.Add(LinePartKind.Button, ToolLine.Button(ChevronLeft, CalendarStr.WidgetsA_Calendar_Previous.Text(), () => Navigate(-1)));
        _forward = _line.Parts.Add(LinePartKind.Button, ToolLine.Button(ChevronRight, CalendarStr.Common_Next.Text(), () => Navigate(1)));
        _lineProblem.Tapped += (_, args) =>
        {
            if (Ready && Screen.OffersAccounts)
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

        // [S-12832] [S-12834] A state (no account, a problem) is one compact row in the period line's place, on the card's
        // top edge (whole cells, as every state's row), the face kept under it.
        SetRow(_state, 0);
        Children.Add(_line);
        Children.Add(_content);
        Children.Add(rows);
        Children.Add(_state);
        _status.Place(this);
    }

    /// <summary>[S-7077] The screen the last load calls for, worded now ([S-1109]).</summary>
    private Themia.Sdk.Kit.KitScreen Screen => _feed.Screen(Strings.Current);

    /// <summary>Whether the style's events are on screen (not a state).</summary>
    private bool Ready => Screen.Kind == Themia.Sdk.Kit.KitScreenKind.Items;

    private IReadOnlyList<CalendarEvent> Events => _feed.Data?.Events ?? [];

    private IReadOnlyList<Birthday> Birthdays => _feed.Data?.Birthdays ?? [];

    private IReadOnlyList<CalendarTarget> Targets => _feed.Data?.Targets ?? [];

    private IReadOnlyDictionary<string, ProviderId> Providers => _feed.Data?.Providers ?? new Dictionary<string, ProviderId>();

    /// <summary>[S-569] [S-12481] Whether the style shows the period's line: every style but the agenda.</summary>
    private bool ShowsLine => Layout != CalendarStyle.Agenda;

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
        _feed.Start();
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
            if (Ready && AwayFromToday)
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

    private WidgetAction Refresh() => new(Glyphs.Refresh, Themia.Sdk.Kit.KitFreshness.RefreshLabel(Strings.Current, _feed.Outcomes, _clock()), () => _ = RefreshAsync(force: true));

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _feed.Stop();
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
    /// [S-7077] Ask for the range on screen through the office feed: one load at a time, one asked for meanwhile runs after
    /// it, so the widget can never settle on a range it has moved away from.
    /// </summary>
    private Task RefreshAsync(bool force) => _detached ? Task.CompletedTask : _feed.RefreshAsync(force);

    /// <summary>One load of the range the style draws, read when it goes out.</summary>
    private async Task<OfficeLoad<CalendarSlice>> LoadAsync(bool force, CancellationToken token)
    {
        // [S-569] Until the user pages away, the widget stays on today, across midnight too.
        if (_followToday)
        {
            _anchor = Today;
        }

        _requestKey = RequestKey();
        if (_data is not { } data)
        {
            return OfficeLoad<CalendarSlice>.Of(OfficeLoadState.NoAccounts);
        }

        var range = Range();
        return (await data.LoadAsync(_config, range.First, range.Last, force, token).ConfigureAwait(true)).AsLoad();
    }

    /// <summary>A load went out (the ring) or came back (drawn).</summary>
    private void OnFeed()
    {
        if (_detached)
        {
            return;
        }

        if (_feed.Busy)
        {
            UpdateState();
            return;
        }

        Render();
    }

    /// <summary>
    /// [S-3651] [S-7042] [S-12481] The state, on the period's line (or over the agenda's top end): the problem glyph while events are on screen that some account's
    /// last sync could not improve on, one line per account and problem (a click on it opens Accounts
    /// where only the user can mend it); the ring while a load is out.
    /// </summary>
    private void UpdateState()
    {
        var screen = Screen;
        var ready = screen.Kind == Themia.Sdk.Kit.KitScreenKind.Items;
        var lines = ready ? Themia.Sdk.Kit.KitFreshness.Lines(Strings.Current, _feed.Outcomes, _clock()) : [];
        if (ShowsLine && _navigable)
        {
            _status.Clear();
            _line.Parts.Want(_lineProblem, lines.Count > 0);
            ToolTipService.SetToolTip(_lineProblem, lines.Count > 0 ? string.Join("\n", lines) : null);
            _line.Parts.Want(_lineBusy, _feed.Busy);
            _line.Parts.Want(_back, true);
            _line.Parts.Want(_forward, true);
            return;
        }

        foreach (var part in new FrameworkElement[] { _lineProblem, _lineBusy, _back, _forward })
        {
            _line.Parts.Want(part, false);
        }

        _status.Show(lines, _feed.Busy, ready && screen.OffersAccounts);
    }

    // -----------------------------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-568] [S-14502] What the card shows of its period: a week's days what its width holds
    /// (<see cref="CalendarSizing.WeekDaysIn"/>), a new one's before it is measured; the config's period else.
    /// </summary>
    private CalendarPeriod Shown => Layout == CalendarStyle.Week
        ? _config.Period with { WeekDays = _area.IsMeasured ? CalendarSizing.WeekDaysIn(_area.Width, _config.WeekTitles, _area.Grid(XamlRoot?.RasterizationScale ?? 1)) : CalendarPeriod.WeekDefaultDays }
        : _config.Period;

    /// <summary>[S-568] The days the layout covers, which is what is drawn.</summary>
    private (DateOnly First, DateOnly Last) Range() => CalendarRules.ViewRange(Layout, _anchor, Today, Shown);

    /// <summary>Whether a day is in the period on screen, so the today button can hide when it would do nothing.</summary>
    private bool InView(DateOnly day) => CalendarRules.InView(Layout, _anchor, Today, Shown, day);

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

        _anchor = CalendarRules.Step(Layout, _anchor, direction, Shown);

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
        if (_detached || !Ready)
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
        var first = CalendarRules.DayItems(Events, [], day, TimeZoneInfo.Local).Select(i => i.Event).FirstOrDefault();

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

        var screen = Screen;
        var ready = screen.Kind == Themia.Sdk.Kit.KitScreenKind.Items;
        // The agenda and the narrow week ([S-2285]) draw their rows themselves; no other style has any.
        if (!ready || Layout is not (CalendarStyle.Agenda or CalendarStyle.Week))
        {
            HideAgenda();
        }
        // [S-568] [S-12481] The period is its line's label; the agenda has no line.
        _line.Label = ShowsLine ? CalendarRules.Title(Layout, _anchor, Today, Shown, Strings.Current) : string.Empty;
        UpdateState();

        // [S-12832] (owner, 2026-11-19: "even if there is no rain in the next two hours or even if there is no cpu data
        // available it should show the default values ... also for other widgets") No account, an account gone or a
        // problem is one compact row in the period line's place; the style's face stays under it with nothing on it
        // (the month, today marked). Loading shows the face and the line's ring.
        var stated = true;
        switch (screen.Kind)
        {
            case Themia.Sdk.Kit.KitScreenKind.NoAccounts:
                _state.Show(_config.SetupSentence ?? OfficeProblems.Message(FetchStatus.NotConfigured, SyncSubject.Calendar, Strings.Current), offerAccounts: true);
                break;

            case Themia.Sdk.Kit.KitScreenKind.AccountsGone or Themia.Sdk.Kit.KitScreenKind.Problem:
                _state.Show(screen);
                break;

            default:
                _state.Hide();
                stated = false;
                break;
        }

        _line.Visibility = stated || !ShowsLine ? Visibility.Collapsed : Visibility.Visible;

        var width = _area.Width;

        // [S-569] [S-12481] The line (or the state's row in its place) takes its room off the top of the box the style
        // is drawn in; the month lays its rows on the card's grid from where it starts under the card's top edge.
        // [S-12834] Both are whole cells: the line's band ends on a grid line, the state's row on the card's top edge too.
        // [S-14101] The state's row stands in the line's place and keeps its band, so the weeks fill the card the sizing
        // made for that band (no cell left over at the foot).
        var cells = _area.Grid(XamlRoot?.RasterizationScale ?? 1);
        var over = ShowsLine ? Math.Max(LineRules.Band(cells), stated ? LineRules.EdgeRow(cells) - FamilySpace.Inset : 0) : stated ? LineRules.EdgeRow(cells) - FamilySpace.Inset : 0;
        var height = _area.Height - over;

        // The status stands where the kit puts it; the month moves it into its weekday line.
        _status.StandAt(null);

        // [S-16231] The month lays itself from the view's top (its grid stands its top under it, past the line's row); every
        // other style is drawn in the row under the line.
        var month = Layout is not (CalendarStyle.Week or CalendarStyle.Day or CalendarStyle.Year or CalendarStyle.Agenda);
        SetRow(_content, month ? 0 : 1);
        SetRowSpan(_content, month ? 2 : 1);

        // [S-569] Something stands over the face: the period's line, or a state's row in its place.
        _stated = stated || ShowsLine;

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
                // [S-569] [S-12832] The month's weekday line right under the period's line (or a state's row in its place).
                var monthTop = FamilySpace.ContentTop + over;
                DrawMonth(width, _area.Height + FamilySpace.ContentTop - monthTop, monthTop);
                break;
        }

        // After drawing: the agenda only knows its pages once it has fitted them.
        ShowNavigation(ready);
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
        _otherMarks.Clear();
        _links.Set(LinkTopics.HoveredEvent, null);
        _days.Clear();
        _rowDays.Clear();
        _days.Point(null);
        _content.Children.Clear();
        _content.RowDefinitions.Clear();
        _content.ColumnDefinitions.Clear();
    }

    // ---- shared pieces ------------------------------------------------------------------------

    private bool ShowColors => CalendarRules.ShowColors(Targets, _config.CalendarColors);

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
        ProviderId? provider = Providers.TryGetValue(item.AccountId, out var known) ? known : null;
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
        => item.WebLink is not null || (Providers.TryGetValue(item.AccountId, out var provider) && provider == HostProviders.Microsoft);

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
