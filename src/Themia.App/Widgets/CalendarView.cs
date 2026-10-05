using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Accounts;
using Themia.App.Services.Office;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-560] Events from the user's Microsoft, Google and CalDAV calendars, through Themia's own
/// accounts, as a month grid, a week, a day timeline or an agenda ([S-561]) — the classic
/// Calendar widget, with contacts' birthdays beside the events ([S-575]).
/// <para>
/// The view owns no connection and no cache: it asks the shared <see cref="IOfficeSource"/>, which
/// answers every widget from one request per calendar and holds failures sensibly ([S-588]). What
/// it keeps is the anchor day being looked at, and the events and birthdays of the range it last
/// asked for. Polls every five minutes; a context-menu Refresh asks past every cache.
/// </para>
/// <para>
/// Times are on a 24-hour clock and names are written out ([S-187]): the app builds with
/// <c>InvariantGlobalization</c>, so there is no culture to read either from.
/// </para>
/// </summary>
internal sealed partial class CalendarView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSurfaceAware, IWidgetContextActions, IWidgetSizeAware, IWidgetKeys
{
    private const string Scope = "calendar-widget";

    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(5);

    private static readonly string ChevronLeft = char.ConvertFromUtf32(0xE76B);
    private static readonly string ChevronRight = char.ConvertFromUtf32(0xE76C);
    private static readonly string GiftGlyph = char.ConvertFromUtf32(0xF133);

    private readonly Grid _content = new();
    private readonly OfficeStatePanel _state = new(Glyphs.Calendar);
    private readonly ProgressRing _busy = new();
    private readonly FontIcon _warning;
    private readonly Button _back;
    private readonly Button _forward;
    private readonly Button _todayButton;
    private readonly StackPanel _trailing;
    private readonly List<IDisposable> _hovers = [];
    private readonly Dictionary<string, Brush> _colorBrushes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AccountProvider> _providers = new(StringComparer.Ordinal);

    private IWidgetSurface? _surface;
    private IOfficeSource? _source;
    private IDisposable? _poll;
    private CalendarConfig _config = new();
    private DateOnly _anchor = Today;
    private bool _followToday = true;

    /// <summary>[S-1269] Whether keys have moved the cursor day, so the month marks it until the next page.</summary>
    private bool _cursorShown;

    /// <summary>The view's slot, from the card ([S-663]); unmeasured until the first layout pass.</summary>
    private WidgetArea _area;
    private Screen _screen = Screen.Loading;
    /// <summary>The problem in words, worded when shown so a language switch reaches it ([S-1109]).</summary>
    private Func<string> _problemText = () => string.Empty;
    private bool _problemOffersAccounts;
    private Func<string>? _warningText;
    private bool _warningOpensAccounts;
    private IReadOnlyList<CalendarEvent> _events = [];
    private IReadOnlyList<Birthday> _birthdays = [];
    private IReadOnlyList<CalendarTarget> _targets = [];
    private (DateOnly First, DateOnly Last)? _fetched;
    private string _requestKey = string.Empty;
    private bool _refreshing;
    private bool _again;
    private bool _againForced;
    private bool _detached;

    public CalendarView()
    {
        _busy.Width = Fluent.IconSize;
        _busy.Height = Fluent.IconSize;
        _busy.IsActive = false;
        _busy.Visibility = Visibility.Collapsed;

        _warning = Fluent.Icon(Glyphs.Warning, "SystemFillColorCautionBrush");
        _warning.Visibility = Visibility.Collapsed;
        _warning.Tapped += OnWarningTapped;

        // [S-569] Back, forward and today ride on the card's header, as the classic widget's did.
        _back = NavButton(ChevronLeft, Str.WidgetsA_Calendar_Previous.Text(), () => Navigate(-1));
        _forward = NavButton(ChevronRight, Str.Common_Next.Text(), () => Navigate(1));
        _todayButton = NavButton(Glyphs.Calendar, Str.Calendar_Today.Text(), GoToday);

        _trailing = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _warning, _busy, _back, _forward, _todayButton },
        };
        _busy.Margin = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0);
        _warning.Margin = new Thickness(0, 0, Fluent.Space1, 0);

        Children.Add(_content);
        Children.Add(_state);
    }

    /// <summary>Which of the widget's states is on screen. Each one needs something different of the user.</summary>
    private enum Screen
    {
        Loading,
        NoAccounts,
        AccountsGone,
        Problem,
        Ready,
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    /// <inheritdoc />
    public FrameworkElement? HeaderTrailing => _trailing;

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        Render();
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _config = config as CalendarConfig ?? new CalendarConfig();
        _source = OfficeSources.Current;

        if (_source is not null)
        {
            _source.Changed += OnAccountsChanged;
        }

        Strings.Changed += OnLanguage;

        // [S-588] The ticker fires once on subscribing, which is the first load.
        _poll = ticker.Subscribe(Poll, () => _ = RefreshAsync(force: false));
    }

    /// <summary>[S-1109] Names, dates and states are worded again from what is in hand; nothing is refetched.</summary>
    private void OnLanguage(Locale locale)
    {
        ToolTipService.SetToolTip(_back, Str.WidgetsA_Calendar_Previous.Text());
        ToolTipService.SetToolTip(_forward, Str.Common_Next.Text());
        ToolTipService.SetToolTip(_todayButton, Str.Calendar_Today.Text());
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as CalendarConfig ?? new CalendarConfig();

        // Only a change to what is asked for is worth a round trip; a colour or the agenda count
        // re-draws what is in hand.
        if (RequestKey() != _requestKey)
        {
            _ = RefreshAsync(force: false);
        }

        Render();
    }

    /// <summary>[S-561] Refresh. [S-1746] The view is how the widget looks: it is chosen in its settings only.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => [new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => _ = RefreshAsync(force: true))];

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

        if (_source is not null)
        {
            _source.Changed -= OnAccountsChanged;
            _source = null;
        }

        _warning.Tapped -= OnWarningTapped;
        _state.Detach();
        ClearDrawing();
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

        var source = _source;
        var accounts = source?.Accounts(AccountFeatures.Calendar) ?? [];
        _requestKey = RequestKey();

        if (source is null || accounts.Count == 0)
        {
            _screen = Screen.NoAccounts;
            Render();
            return;
        }

        var selected = CalendarRules.SelectAccounts(_config.AccountIds, [.. accounts.Select(a => a.Id)]);
        if (selected.Count == 0)
        {
            _screen = Screen.AccountsGone;
            Render();
            return;
        }

        var labels = accounts.ToDictionary(a => a.Id, a => a.Label, StringComparer.Ordinal);
        foreach (var account in accounts)
        {
            _providers[account.Id] = account.Provider;
        }

        var targets = CalendarRules.Targets(selected, _config.Calendars);
        var range = Range();
        var (from, to) = CalendarRules.FetchWindow(range.First, range.Last, TimeZoneInfo.Local);

        _refreshing = true;
        SetBusy(true);

        try
        {
            var eventTasks = targets.Select(t => source.EventsAsync(t, from, to, force, CancellationToken.None)).ToList();
            var birthdayTasks = _config.ShowBirthdays
                ? selected.Select(a => source.BirthdaysAsync(a, force, CancellationToken.None)).ToList()
                : [];

            await Task.WhenAll(eventTasks.Cast<Task>().Concat(birthdayTasks)).ConfigureAwait(true);

            if (_detached)
            {
                return;
            }

            var events = new List<CalendarEvent>();
            var problems = new List<(string Account, SyncProblem Problem)>();
            var anyData = false;

            for (var i = 0; i < targets.Count; i++)
            {
                var result = eventTasks[i].Result;
                if (result.Value is { } list)
                {
                    anyData = true;
                    events.AddRange(list.Select(e => e with { AccountId = targets[i].AccountId, CalendarId = targets[i].CalendarId, SourceIndex = i }));
                }

                if (result.Problem != SyncProblem.None)
                {
                    problems.Add((targets[i].AccountId, result.Problem));
                }
            }

            var birthdays = new List<Birthday>();
            for (var i = 0; i < birthdayTasks.Count; i++)
            {
                var result = birthdayTasks[i].Result;
                birthdays.AddRange(result.Value ?? []);

                // [S-575] A refused address book costs the birthdays, never the calendar: it is a
                // warning on the header, not an error over the events.
                if (result.Problem != SyncProblem.None)
                {
                    problems.Add((selected[i], result.Problem));
                }
            }

            _events = events;
            _birthdays = BirthdayRules.Distinct(birthdays);
            _targets = targets;
            _fetched = range;

            var worst = problems.OrderByDescending(p => SyncProblems.NeedsUser(p.Problem)).Select(p => (p.Account, p.Problem)).FirstOrDefault();

            if (!anyData && problems.Count > 0)
            {
                _screen = Screen.Problem;
                var problem = worst.Problem;
                _problemText = () => SyncProblems.Message(problem, SyncSubject.Calendar, Strings.Current);
                _problemOffersAccounts = SyncProblems.NeedsUser(worst.Problem);
                _warningText = null;
            }
            else
            {
                _screen = Screen.Ready;
                _warningText = problems.Count == 0
                    ? null
                    : () => string.Join(Environment.NewLine, problems.Distinct().Select(p =>
                        Str.WidgetsA_Office_AccountProblem.Format(
                            ("account", labels.TryGetValue(p.Account, out var label) ? label : p.Account),
                            ("problem", SyncProblems.Message(p.Problem, SyncSubject.Calendar, Strings.Current)))));
                _warningOpensAccounts = problems.Any(p => SyncProblems.NeedsUser(p.Problem));
            }
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
            SetBusy(false);
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

    private void SetBusy(bool busy)
    {
        _busy.IsActive = busy;
        _busy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    // -----------------------------------------------------------------------------------------
    // Navigation and views
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-1260] What the tile draws of the configured view at its size.</summary>
    private CalendarLayout Layout() => CalendarFit.Layout(_config.EffectiveView, _area.Width, _area.Height);

    /// <summary>[S-1262] How many days a strip of this width holds.</summary>
    private int StripDays() => CalendarFit.StripDays(_config.EffectiveView, _area.Width);

    /// <summary>[S-568] The days the current layout covers, which is what is fetched.</summary>
    private (DateOnly First, DateOnly Last) Range()
        => CalendarFit.Range(Layout(), _config.EffectiveView, _anchor, Today, _config.EffectiveWeekStart, StripDays());

    private void Navigate(int direction)
    {
        _anchor = CalendarFit.Step(Layout(), _anchor, direction, StripDays());

        // Paging back to the period holding today picks following it up again.
        _followToday = InView(Today);
        _cursorShown = false;
        Render();
        _ = RefreshAsync(force: false);
    }

    /// <summary>
    /// [S-1269]/[S-806] A key from the shell's keyboard lease (the classic S-31..34), which the
    /// calendar holds once it is clicked. Escape (also the lease ending) hides the cursor day.
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
            if (!_cursorShown)
            {
                return false;
            }

            _cursorShown = false;
            Render();
            return true;
        }

        var layout = Layout();
        var command = CalendarFit.Key(key, layout);
        switch (command)
        {
            case CalendarKeyCommand.Back:
                Navigate(-1);
                return true;

            case CalendarKeyCommand.Forward:
                Navigate(1);
                return true;

            case CalendarKeyCommand.WeekBack or CalendarKeyCommand.WeekForward:
                var before = Range();
                _anchor = _anchor.AddDays(command == CalendarKeyCommand.WeekBack ? -7 : 7);
                _followToday = _anchor == Today;
                _cursorShown = true;
                Render();
                if (Range() != before)
                {
                    _ = RefreshAsync(force: false);
                }

                return true;

            case CalendarKeyCommand.Today:
                _cursorShown = false;
                GoToday();
                return true;

            case CalendarKeyCommand.OpenFirst:
                return OpenFirst(layout);

            default:
                return false;
        }
    }

    /// <summary>[S-1269] Enter: the cursor day's first event, or the next one where the layout has no cursor.</summary>
    private bool OpenFirst(CalendarLayout layout)
    {
        var first = CalendarFit.Pages(layout)
            ? CalendarRules.DayItems(_events, [], _anchor, TimeZoneInfo.Local).Select(i => i.Event).FirstOrDefault()
            : CalendarFit.Next(_events, [], Today, TimeZoneInfo.Local, DateTimeOffset.Now)?.Item.Event;

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
        Render();
        _ = RefreshAsync(force: false);
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();

        // A resize can turn the month into the agenda or a strip ([S-1260]), which cover other days.
        if (_screen == Screen.Ready && RequestKey() != _requestKey)
        {
            _ = RefreshAsync(force: false);
        }
    }

    private void OnWarningTapped(object sender, TappedRoutedEventArgs args)
    {
        if (_warningOpensAccounts)
        {
            args.Handled = true;
            OfficeStatePanel.OpenAccounts();
        }
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

        var layout = Layout();
        var ready = _screen == Screen.Ready;
        var paged = ready && CalendarFit.Pages(layout);

        _back.Visibility = paged ? Visibility.Visible : Visibility.Collapsed;
        _forward.Visibility = paged ? Visibility.Visible : Visibility.Collapsed;
        _todayButton.Visibility = paged && !InView(Today) ? Visibility.Visible : Visibility.Collapsed;

        _warning.Visibility = ready && _warningText is not null ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(_warning, _warningText?.Invoke());

        // [S-568] The period is the header's title while paging; the agenda is just "Calendar".
        _surface?.SetHeaderTitle(paged ? CalendarFit.Title(layout, _config.EffectiveView, _anchor, _config.EffectiveWeekStart, StripDays(), Strings.Current) : null);

        switch (_screen)
        {
            case Screen.Loading:
                _state.Show(Str.Common_Loading.Text(), offerAccounts: false);
                return;

            case Screen.NoAccounts:
                // [S-1862] The card shows its Set up button over this.
                _state.Hide();
                return;

            case Screen.AccountsGone:
                _state.Show(SyncProblems.Message(SyncProblem.NoAccount, SyncSubject.Calendar, Strings.Current), offerAccounts: true);
                return;

            case Screen.Problem:
                _state.Show(_problemText(), _problemOffersAccounts);
                return;
        }

        _state.Hide();

        var width = _area.Width;
        var height = _area.Height;

        switch (layout)
        {
            case CalendarLayout.Next:
                DrawNext(width, height);
                break;

            case CalendarLayout.Strip:
                DrawStrip(width, height);
                break;

            case CalendarLayout.Month:
                DrawMonth(width, height);
                break;

            case CalendarLayout.MonthAgendaBelow:
                DrawMonthWithAgenda(width, height, beside: false);
                break;

            case CalendarLayout.MonthAgendaBeside:
                DrawMonthWithAgenda(width, height, beside: true);
                break;

            case CalendarLayout.Week:
                DrawWeek(width, height);
                break;

            case CalendarLayout.Day:
                DrawDay(width, height);
                break;

            default:
                DrawAgenda(width, height);
                break;
        }
    }

    /// <summary>Whether a day is in the period on screen, so the today button can hide when it would do nothing.</summary>
    private bool InView(DateOnly day)
        => CalendarFit.InView(Layout(), _config.EffectiveView, _anchor, Today, _config.EffectiveWeekStart, StripDays(), day);

    private void ClearDrawing()
    {
        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();
        _content.Children.Clear();
        _content.RowDefinitions.Clear();
        _content.ColumnDefinitions.Clear();
    }

    // ---- shared pieces ------------------------------------------------------------------------

    private bool ShowColors => CalendarRules.ShowColors(_targets, _config.CalendarColors);

    /// <summary>[S-566] An event's calendar colour, resolved at draw time so a settings change repaints at once.</summary>
    private Brush ColorOf(CalendarEvent item)
    {
        var hex = CalendarRules.ResolveColor(CalendarRules.ColorKey(item.AccountId, item.CalendarId), item.SourceIndex, _config.CalendarColors);

        if (!_colorBrushes.TryGetValue(hex, out var brush))
        {
            brush = HexColor.TryParse(hex, out var r, out var g, out var b)
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b))
                : FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
            _colorBrushes[hex] = brush;
        }

        return brush;
    }

    private static Brush Accent() => FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);

    private static Brush BirthdayBrush() => FluentLayout.Themed("SystemFillColorCautionBrush", Colors.Goldenrod);

    /// <summary>
    /// [S-1268] Open an event: a Microsoft one in Outlook for Windows when it is installed, else the
    /// provider's own page. CalDAV has none, so it only has its tooltip.
    /// </summary>
    private void Open(CalendarEvent item)
    {
        AccountProvider? provider = _providers.TryGetValue(item.AccountId, out var known) ? known : null;
        if (CalendarLinks.Open(item, provider, provider == AccountProvider.Microsoft && ShellLauncher.OutlookInstalled) is { } link)
        {
            ShellLauncher.OpenCalendarLink(link);
        }
    }

    private void MakeOpenable(FrameworkElement element, CalendarEvent item)
    {
        ToolTipService.SetToolTip(element, Describe(item));

        if (item.WebLink is not null || (_providers.TryGetValue(item.AccountId, out var provider) && provider == AccountProvider.Microsoft))
        {
            // Not handled: the card's double-click into edit mode still has to reach it.
            element.Tapped += (_, _) => Open(item);
        }
    }

    /// <summary>Everything a chip had to leave out, on hover.</summary>
    private static string Describe(CalendarEvent item)
    {
        var lines = new List<string> { Title(item) };

        if (item.AllDay)
        {
            var days = item.EndDay.DayNumber - item.FirstDay.DayNumber;
            lines.Add(Str.WidgetsA_Calendar_AllDayOn.Format(("date", days > 1
                ? CalendarRules.Range(item.FirstDay, item.EndDay.AddDays(-1), Strings.Current)
                : Formatting.ShortDate(item.FirstDay))));
        }
        else
        {
            var start = item.Start.ToLocalTime();
            var span = item.End > item.Start
                ? Str.WidgetsA_Calendar_SpanWithLength.Format(("from", Clock(item.Start)), ("to", Clock(item.End)), ("length", Formatting.Duration(item.End - item.Start)))
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

    private static string Title(CalendarEvent item) => string.IsNullOrWhiteSpace(item.Subject) ? Str.Calendar_NoTitle.Text() : item.Subject;

    private static string Clock(DateTimeOffset moment) => Formatting.Clock24(moment);

    private static Button NavButton(string glyph, string tooltip, Action click)
    {
        var button = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontSize = FluentType.Caption.FontSize },
            Padding = new Thickness(Fluent.Space1 + 2, Fluent.Space1, Fluent.Space1 + 2, Fluent.Space1),
            MinWidth = 0,
            MinHeight = 0,
            Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            VerticalAlignment = VerticalAlignment.Center,
        };

        ToolTipService.SetToolTip(button, tooltip);
        button.Click += (_, _) => click();
        return button;
    }
}
