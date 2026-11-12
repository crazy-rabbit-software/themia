using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Office;
using Themia.App.Services.Tasks;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.WeekPlanner;

namespace Themia.App.Widgets;

/// <summary>
/// [S-5320] The week planner: today and the days after it, each with its events, and under today the
/// tasks due. One planner, not a calendar and a task list side by side: a day's tasks are rows of
/// that day, after its events ([S-5322]).
/// <para>
/// The events come through the calendar widgets' seam (<see cref="ICalendarData"/>, the shared sync
/// behind it, [S-2701]) for the planner's whole week whatever it shows, so a resize never asks again;
/// the tasks are the shared task service's Today view of one account ([S-5324]), so ticking one off
/// here ticks it in every Tasks widget. Neither is fetched by the widget: a 5-minute poll of what the
/// sync holds and the accounts' change events, as the calendars. How many days and rows a size shows
/// is <see cref="WeekPlannerRules"/>.
/// </para>
/// </summary>
internal sealed partial class WeekPlannerView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetSizeAware, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => WeekPlannerModule.Opens(_config.AccountIds, _widgetHost.AccountsWith(HostFeatures.Calendar));

    private const string Scope = "week-planner";

    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    /// <summary>The coloured mark before an event: thin, as a calendar chip's edge.</summary>

    /// <summary>The ring before a task, as the Tasks widget draws one ([S-597]), at a planner row's size.</summary>

    private readonly KitStatePanel _state;
    private readonly Grid _content = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly Dictionary<string, Brush> _colorBrushes = new(StringComparer.Ordinal);
    private readonly List<IDisposable> _hovers = [];

    private IWidgetSurface? _surface;
    private ICalendarData? _data;
    private ITaskSource? _service;
    private ITaskFeed? _feed;
    private IDisposable? _poll;
    private IDisposable? _tick;
    private WeekPlannerConfig _config = new();
    private WidgetArea _area;
    private Screen _screen = Screen.Loading;
    private Func<string> _problemText = () => string.Empty;
    private bool _problemOffersAccounts;
    private IReadOnlyList<CalendarEvent> _events = [];
    private IReadOnlyList<Birthday> _birthdays = [];
    private IReadOnlyList<CalendarTarget> _targets = [];
    private IReadOnlyDictionary<string, ProviderId> _providers = new Dictionary<string, ProviderId>();
    private bool _refreshing;
    private bool _again;
    private bool _detached;

    /// <summary>A debug render's tasks, in place of the service's ([S-1917]).</summary>
#if DEBUG
    private IReadOnlyList<TaskItem>? _sampleTasks;
#else
    // Release has no renders: the field's Debug-only writer (the .Preview.cs part) is not compiled.
    private IReadOnlyList<TaskItem>? _sampleTasks => null;
#endif

    /// <summary>The clock the days are drawn against: the machine's, which a preview pins.</summary>
    private Func<DateTimeOffset> _clock = static () => DateTimeOffset.Now;

    /// <summary>[S-6913] What the view asks of the host: the launcher, ⚙ › Accounts.</summary>
    private readonly IWidgetHost _widgetHost;

    public WeekPlannerView(IWidgetHost host)
    {
        _widgetHost = host;
        _state = new(Glyphs.WeekPlanner, host) { Visibility = Visibility.Collapsed };
        // [S-1113] Days run in reading order; the columns follow the language's direction like the week calendar's.
        Children.Add(_content);
        Children.Add(_state);
    }

    private enum Screen
    {
        Loading,
        NoAccounts,
        AccountsGone,
        Problem,
        Ready,
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _config = config as WeekPlannerConfig ?? new WeekPlannerConfig();
        UseData(_widgetHost.Office is { } source ? new OfficeCalendarData(source) : null);
        Strings.Changed += OnLanguage;

        _service = _widgetHost.Tasks;
        if (_service is not null)
        {
            _service.AccountsChanged += OnTaskAccountsChanged;
        }

        BindTasks();

        // [S-588] The poll fires once on subscribing: the first load.
        _poll = ticker.Subscribe(Poll, () => _ = RefreshAsync(force: false));
        _tick = ticker.Subscribe(Minute, Render);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as WeekPlannerConfig ?? new WeekPlannerConfig();
        _colorBrushes.Clear();
        BindTasks();
        _ = RefreshAsync(force: false);
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Render();
    }

    /// <summary>Refresh, as every calendar widget has: the events and the tasks both.</summary>
    public IReadOnlyList<WidgetAction> ContextActions => [new(Glyphs.Refresh, Str.Common_Refresh.Text(), () =>
    {
        if (_feed is { } feed)
        {
            _service?.Refresh(feed, UpdateCadence.Interval(HostCadences.Accounts), force: true);
        }

        _ = RefreshAsync(force: true);
    })];

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
        _tick?.Dispose();
        _tick = null;
        UseData(null);
        ReleaseTasks();

        if (_service is not null)
        {
            _service.AccountsChanged -= OnTaskAccountsChanged;
            _service = null;
        }

        ClearRows();
        _state.Detach();
    }

    private void OnLanguage(Locale locale) => Render();

    // -----------------------------------------------------------------------------------------
    // Data
    // -----------------------------------------------------------------------------------------

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

    private void OnAccountsChanged() => _ = RefreshAsync(force: false);

    /// <summary>One load at a time; one asked for meanwhile runs after it.</summary>
    private async Task RefreshAsync(bool force)
    {
        if (_detached)
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
            _screen = HasTasks ? Screen.Ready : Screen.NoAccounts;
            Render();
            return;
        }

        var (first, last) = WeekPlannerRules.Range(DateOnly.FromDateTime(_clock().LocalDateTime));
        _refreshing = true;

        try
        {
            var slice = await data.LoadAsync(_config, first, last, force, CancellationToken.None).ConfigureAwait(true);
            if (_detached)
            {
                return;
            }

            Take(slice);
        }
        catch (Exception ex)
        {
            // Reached from a ticker callback and a menu click: nothing above may see it.
            Log.Error(Scope, "could not read the calendar", ex);
            _screen = Screen.Problem;
            _problemText = () => Str.WidgetsA_Calendar_ReadFailed.Text();
            _problemOffersAccounts = false;
        }
        finally
        {
            _refreshing = false;
        }

        Render();

        if (_again && !_detached)
        {
            _again = false;
            await RefreshAsync(force: false).ConfigureAwait(true);
        }
    }

    private void Take(CalendarSlice slice)
    {
        switch (slice.State)
        {
            // [S-5325] Tasks alone still make a planner: no calendar account is an empty week then.
            case CalendarSliceState.NoAccounts:
                _events = [];
                _birthdays = [];
                _targets = [];
                _screen = HasTasks ? Screen.Ready : Screen.NoAccounts;
                return;

            case CalendarSliceState.AccountsGone:
                _screen = Screen.AccountsGone;
                return;
        }

        if (!slice.AnyData && slice.Problems.Count > 0)
        {
            var worst = slice.Problems.OrderByDescending(p => SyncProblems.NeedsUser(p.Problem)).First();
            _screen = Screen.Problem;
            _problemText = () => SyncProblems.Message(worst.Problem, SyncSubject.Calendar, Strings.Current);
            _problemOffersAccounts = SyncProblems.NeedsUser(worst.Problem);
            return;
        }

        _screen = Screen.Ready;
        _events = slice.Events;
        _birthdays = slice.Birthdays;
        _targets = slice.Targets;
        _providers = slice.Providers;
    }

    /// <summary>Whether the planner has a task source to show, calendar or not.</summary>
    private bool HasTasks => _sampleTasks is not null || _feed is not null;

    /// <summary>[S-5324] Hold the Today view of the chosen task account (the first with tasks when none is chosen).</summary>
    private void BindTasks()
    {
        ReleaseTasks();

        if (!_config.ShowTasks || _service is null || _sampleTasks is not null)
        {
            return;
        }

        var account = string.IsNullOrEmpty(_config.TaskAccountId)
            ? _service.Accounts.FirstOrDefault()?.Id
            : _service.Find(_config.TaskAccountId)?.Id;

        if (account is null)
        {
            return;
        }

        _feed = _service.Acquire(account, TaskRules.TodayListId, showCompleted: false);
        _feed.Changed += OnTasksChanged;

        // [S-2609] Only a list never synced asks at once; the service's timer does the rest.
        if (!_feed.Loaded)
        {
            _service.Refresh(_feed, UpdateCadence.Interval(HostCadences.Accounts), force: true);
        }
    }

    private void ReleaseTasks()
    {
        if (_feed is null)
        {
            return;
        }

        _feed.Changed -= OnTasksChanged;
        _service?.Release(_feed);
        _feed = null;
    }

    private void OnTasksChanged() => Render();

    private void OnTaskAccountsChanged()
    {
        BindTasks();
        _ = RefreshAsync(force: false);
    }

    private IReadOnlyList<TaskItem> Tasks => !_config.ShowTasks ? [] : _sampleTasks ?? _feed?.Tasks ?? [];

    // -----------------------------------------------------------------------------------------
    // Drawing
    // -----------------------------------------------------------------------------------------

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        ClearRows();
        _content.Visibility = Visibility.Collapsed;

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

        var now = _clock();
        var zone = TimeZoneInfo.Local;
        var today = DateOnly.FromDateTime(now.LocalDateTime);
        var days = WeekPlannerRules.Build(_events, _birthdays, Tasks, today, zone, now);

        // [S-5321] Rows carry the card's side inset, so the columns meet; the card's bottom inset stays free.
        var plan = WeekPlannerRules.Plan(_area);
        var room = Math.Max(0, _area.Height - FamilySpace.Inset);
        var heading = FamilyShape.Row1;
        var row = FamilyShape.Row1;

        // An empty today still takes a row, to say nothing is planned ([S-5323]).
        var counts = days.Select((d, i) => i == 0 ? Math.Max(1, d.Count) : d.Count).ToList();
        var slots = plan.Stacked
            ? WeekPlannerRules.Stack(counts, room, heading, row)
            : WeekPlannerRules.Columns(counts, plan.Columns, room, heading, row, plan.Headings);
        var showColors = CalendarRules.ShowColors(_targets, _config.CalendarColors);
        var columns = plan.Stacked ? 1 : slots.Count;

        for (var c = 0; c < columns; c++)
        {
            _content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var stack = new StackPanel();
        for (var s = 0; s < slots.Count; s++)
        {
            var slot = slots[s];
            if (!plan.Stacked)
            {
                stack = new StackPanel();
                SetColumn(stack, s);
                _content.Children.Add(stack);
            }
            else if (s == 0)
            {
                _content.Children.Add(stack);
            }

            DrawDay(stack, days[slot.Day], slot, today, zone, showColors, plan.Times);
        }

        _content.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// [S-5323] A day: its heading (one row of 28, BodyStrong; primary for today, secondary for the rest,
    /// never the accent), its events, then its tasks, then "+3" when the day has more than fit.
    /// </summary>
    private void DrawDay(StackPanel stack, PlannerDay day, PlannerSlot slot, DateOnly today, TimeZoneInfo zone, bool showColors, bool times)
    {
        if (slot.Heading)
        {
            var title = Family.Text(FamilyType.BodyStrong, day.Day == today ? ColourRole.TextPrimary : ColourRole.TextSecondary, Formatting.DayHeading(day.Day, today));
            title.VerticalAlignment = VerticalAlignment.Center;
            stack.Children.Add(new Grid { Children = { title }, Height = FamilyShape.Row1, Padding = Family.SideInset });
        }

        if (day.Count == 0)
        {
            if (day.Day == today && slot.Shown > 0)
            {
                stack.Children.Add(Note(WeekPlannerStr.Planner_Free.Text()));
            }

            return;
        }

        var shown = 0;
        foreach (var item in day.Events)
        {
            if (shown++ >= slot.Shown)
            {
                break;
            }

            stack.Children.Add(EventRow(item, day.Day, zone, showColors, times));
        }

        foreach (var task in day.Tasks)
        {
            if (shown++ >= slot.Shown)
            {
                break;
            }

            stack.Children.Add(TaskRow(task, today, zone));
        }

        if (slot.Hidden > 0)
        {
            stack.Children.Add(Note($"+{LocaleFormat.Integer(Strings.Current, slot.Hidden)}"));
        }
    }

    /// <summary>
    /// [S-5322] An event, a Row block of 32: its calendar's colour as a 2 px bar in the lead (a data
    /// colour, [S-6705]; the secondary role when the planner shows one calendar), the title as the name,
    /// the start as the value where the column has room. A tap opens it where it lives.
    /// </summary>
    private Border EventRow(DayItem item, DateOnly day, TimeZoneInfo zone, bool showColors, bool times)
    {
        var block = new RowBlock(RowKind.List);
        block.Lead(new Border
        {
            Width = FamilyShape.Stroke,
            Height = FamilyShape.Icon,
            CornerRadius = Family.Round(FamilyShape.Stroke, FamilyShape.Icon),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = showColors && item.Event is { } colored ? ColorOf(colored) : Family.Brush(ColourRole.TextSecondary),
        });

        string title;
        string? value = null;
        string tip;
        if (item.Birthday is { } birthday)
        {
            title = BirthdayRules.Label(birthday, day);
            tip = Str.WidgetsA_Calendar_BirthdayOf.Format(("name", title));
        }
        else
        {
            var e = item.Event!;
            title = string.IsNullOrWhiteSpace(e.Subject) ? Str.Calendar_NoTitle.Text() : e.Subject;
            var part = CalendarRules.AgendaSpan(e, day, zone);
            if (part.WholeDay)
            {
                tip = $"{Str.Calendar_AllDay.Text()}  {title}";
            }
            else
            {
                value = times ? Time(part.Start) : null;
                tip = part.End > part.Start ? $"{Time(part.Start)}–{Time(part.End)}  {title}" : $"{Time(part.Start)}  {title}";
            }

            if (!string.IsNullOrWhiteSpace(e.Location))
            {
                tip += Environment.NewLine + e.Location;
            }
        }

        block.Show(title, value);
        var row = Item(block);
        ToolTipService.SetToolTip(row, tip);
        if (item.Event is { } openable)
        {
            // [S-2004] The click is the widget's own: it never reaches the card.
            row.Tapped += (_, args) =>
            {
                args.Handled = true;
                Open(openable);
            };
        }

        return row;
    }

    /// <summary>[S-5322] A task due today or overdue: a Row block led by its check (a 16 ring, critical when overdue); a tap on the ring completes it.</summary>
    private Border TaskRow(TaskItem task, DateOnly today, TimeZoneInfo zone)
    {
        var overdue = TaskDue.IsPast(task.DueRaw, today, zone);
        var ring = new Border
        {
            Width = FamilyShape.Icon,
            Height = FamilyShape.Icon,
            CornerRadius = Family.Round(FamilyShape.Icon, FamilyShape.Icon),
            BorderThickness = new Thickness(FamilyShape.Stroke),
            BorderBrush = Family.Brush(overdue ? ColourRole.Critical : ColourRole.TextSecondary),
            Background = Family.Brush(ColourRole.Clear),
            VerticalAlignment = VerticalAlignment.Center,
        };

        // [S-621] A read-only account's task (CalDAV) has no tick: the ring says so and does nothing.
        var readOnly = _feed?.ReadOnly == true;
        ToolTipService.SetToolTip(ring, readOnly ? Str.Shell_Tasks_ReadOnly.Text() : Str.WidgetsC_Tasks_MarkDone.Text());
        ring.Tapped += (_, args) =>
        {
            args.Handled = true;
            if (!readOnly)
            {
                _ = CompleteAsync(task);
            }
        };

        var block = new RowBlock(RowKind.List);
        block.Lead(ring);
        block.Show(task.DisplayTitle);
        var row = Item(block);
        ToolTipService.SetToolTip(row, task.DisplayTitle);
        return row;
    }

    /// <summary>[S-597] Tick a task off through the shared service, optimistically; inert in a preview and in edit mode.</summary>
    private async Task CompleteAsync(TaskItem task)
    {
        if (_surface?.IsPreview == true || _surface?.EditMode == true || _service is null || _feed is null)
        {
            return;
        }

        try
        {
            await _service.SetCompletedAsync(_feed, task, completed: true).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // From a tap: nothing above may see it. The service takes the tick back and says why.
            Log.Error(Scope, "could not complete a task", ex);
        }
    }

    /// <summary>A line that is not an item ("Nothing planned", "+3"): a Row block's name in the secondary role, at the rows' text edge.</summary>
    private static RowBlock Note(string text)
    {
        var block = new RowBlock(RowKind.List);
        block.Show(text);
        block.NameText.Foreground = Family.Brush(ColourRole.TextSecondary);
        return block;
    }

    /// <summary>A full-width item around a row: the item layer's fill and hover ([S-6513]).</summary>
    private Border Item(RowBlock block)
    {
        var row = new Border { Child = block };
        ItemFill.Paint(row, ItemRole.Row);
        _hovers.Add(Fluent.Hover(row));
        return row;
    }

    private void ClearRows()
    {
        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();
        _content.Children.Clear();
        _content.ColumnDefinitions.Clear();
    }

    /// <summary>[S-3773] The region's clock, as every time the widgets write.</summary>
    private static string Time(DateTimeOffset moment)
        => LocaleFormat.Time(Strings.Current, TimeOnly.FromDateTime(moment.ToLocalTime().DateTime));

    /// <summary>[S-566] The event's calendar colour, the user's or the palette's, as a data colour.</summary>
    private Brush ColorOf(CalendarEvent item)
    {
        var hex = CalendarRules.ResolveColor(CalendarRules.ColorKey(item.AccountId, item.CalendarId), item.SourceIndex, _config.CalendarColors);
        if (!_colorBrushes.TryGetValue(hex, out var brush))
        {
            brush = Family.Data(hex);
            _colorBrushes[hex] = brush;
        }

        return brush;
    }

    /// <summary>[S-1268] A click on an event opens it in its calendar, as a calendar widget's does; never in a preview or edit mode.</summary>
    private void Open(CalendarEvent item)
    {
        if (_surface?.IsPreview == true || _surface?.EditMode == true)
        {
            return;
        }

        ProviderId? provider = _providers.TryGetValue(item.AccountId, out var known) ? known : null;
        if (CalendarLinks.Open(item, provider, provider == HostProviders.Microsoft && _widgetHost.Launcher.OutlookInstalled) is { } link)
        {
            _widgetHost.Launcher.OpenCalendarLink(link);
        }
    }
}
