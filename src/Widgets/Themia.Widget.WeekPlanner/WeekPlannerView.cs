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
internal sealed partial class WeekPlannerView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetSizeAware, IWidgetContextActions
{
    private const string Scope = "week-planner";

    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    /// <summary>The coloured mark before an event: thin, as a calendar chip's edge.</summary>
    private const double StripeWidth = 3;

    /// <summary>The ring before a task, as the Tasks widget draws one ([S-597]), at a planner row's size.</summary>
    private const double RingSize = 14;

    private readonly KitStatePanel _state;
    private readonly Grid _content = new() { ColumnSpacing = WeekPlannerRules.ColumnGap };
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
        _state = new(Glyphs.WeekPlanner, host);
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

        var vertical = _area.HeightClass == SizeClass.Compact ? Fluent.Space2 : Fluent.Space3;
        _content.Margin = new Thickness(Fluent.Space3, vertical, Fluent.Space3, vertical);
        var box = _area.Deflate(Fluent.Space3, vertical);
        var plan = WeekPlannerRules.Plan(box);
        var heading = HeadingHeight;
        var row = RowHeight;

        // An empty today still takes a row, to say nothing is planned ([S-5323]).
        var counts = days.Select((d, i) => i == 0 ? Math.Max(1, d.Count) : d.Count).ToList();
        var slots = plan.Stacked
            ? WeekPlannerRules.Stack(counts, box.Height, heading, row)
            : WeekPlannerRules.Columns(counts, plan.Columns, box.Height, heading, row, plan.Headings);

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

            DrawDay(stack, days[slot.Day], slot, today, now, zone, showColors, plan.Times, heading, row);
        }

        _content.Visibility = Visibility.Visible;
    }

    private static double HeadingHeight => FluentType.BodyStrong.LineHeight + Fluent.Space1;

    private static double RowHeight => FluentType.Body.LineHeight + Fluent.Space1;

    /// <summary>A day: its heading, then its events, then (today) its tasks, as many as the slot shows, and "+N" for the rest.</summary>
    private void DrawDay(StackPanel stack, PlannerDay day, PlannerSlot slot, DateOnly today, DateTimeOffset now, TimeZoneInfo zone, bool showColors, bool times, double heading, double row)
    {
        if (slot.Heading)
        {
            var title = Fluent.Text(FluentType.BodyStrong, day.Day == today ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
            title.Text = Formatting.DayHeading(day.Day, today);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.Height = heading;
            stack.Children.Add(title);
        }

        if (day.Count == 0)
        {
            if (day.Day == today && slot.Shown > 0)
            {
                stack.Children.Add(Note(WeekPlannerStr.Planner_Free.Text(), row));
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

            stack.Children.Add(EventRow(item, day.Day, now, zone, showColors, times, row));
        }

        foreach (var task in day.Tasks)
        {
            if (shown++ >= slot.Shown)
            {
                break;
            }

            stack.Children.Add(TaskRow(task, today, zone, row));
        }

        if (slot.Hidden > 0)
        {
            stack.Children.Add(Note($"+{LocaleFormat.Integer(Strings.Current, slot.Hidden)}", row));
        }
    }

    /// <summary>[S-5322] An event: its calendar's mark, its start (none for a whole day; in the accent while it runs) and its title.</summary>
    private Border EventRow(DayItem item, DateOnly day, DateTimeOffset now, TimeZoneInfo zone, bool showColors, bool times, double height)
    {
        var line = new Grid { ColumnSpacing = Fluent.Space2 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var stripe = new Border
        {
            Width = StripeWidth,
            Height = FluentType.Body.FontSize,
            CornerRadius = new CornerRadius(StripeWidth / 2),
            VerticalAlignment = VerticalAlignment.Center,
            Background = item.Birthday is not null ? Caution() : showColors && item.Event is { } colored ? ColorOf(colored) : Accent(),
        };
        line.Children.Add(stripe);

        var title = Fluent.Text(FluentType.Body);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(title, 1);
        SetColumnSpan(title, 2);
        line.Children.Add(title);

        string tip;
        if (item.Birthday is { } birthday)
        {
            title.Text = BirthdayRules.Label(birthday, day);
            tip = Str.WidgetsA_Calendar_BirthdayOf.Format(("name", title.Text));
        }
        else
        {
            var e = item.Event!;
            title.Text = string.IsNullOrWhiteSpace(e.Subject) ? Str.Calendar_NoTitle.Text() : e.Subject;
            var part = CalendarRules.AgendaSpan(e, day, zone);
            tip = title.Text;

            if (!part.WholeDay && times)
            {
                SetColumn(title, 2);
                SetColumnSpan(title, 1);
                var running = AgendaRules.IsInProgress(false, part.Start, part.End, now);
                var time = Fluent.Text(FluentType.Caption, running ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
                time.Text = Time(part.Start);
                time.VerticalAlignment = VerticalAlignment.Center;
                Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(time, FontNumeralAlignment.Tabular);
                SetColumn(time, 1);
                line.Children.Add(time);
                tip = part.End > part.Start ? $"{Time(part.Start)}–{Time(part.End)}  {title.Text}" : $"{time.Text}  {title.Text}";
            }
            else if (part.WholeDay)
            {
                tip = $"{Str.Calendar_AllDay.Text()}  {title.Text}";
            }
            else
            {
                tip = part.End > part.Start ? $"{Time(part.Start)}–{Time(part.End)}  {title.Text}" : $"{Time(part.Start)}  {title.Text}";
            }

            if (!string.IsNullOrWhiteSpace(e.Location))
            {
                tip += Environment.NewLine + e.Location;
            }
        }

        var row = Row(line, height);
        ToolTipService.SetToolTip(row, tip);

        if (item.Event is { } openable)
        {
            row.Tapped += (_, _) => Open(openable);
        }

        return row;
    }

    /// <summary>[S-5322] A task due today (or overdue: its ring in the critical colour); the ring ticks it off ([S-597]).</summary>
    private Border TaskRow(TaskItem task, DateOnly today, TimeZoneInfo zone, double height)
    {
        var overdue = TaskDue.IsPast(task.DueRaw, today, zone);
        var line = new Grid { ColumnSpacing = Fluent.Space2 };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var ring = new Border
        {
            Width = RingSize,
            Height = RingSize,
            CornerRadius = new CornerRadius(RingSize / 2),
            VerticalAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(Fluent.Hairline),
            BorderBrush = overdue
                ? ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed)
                : ThemeBrush.Of("ControlStrongStrokeColorDefaultBrush", Colors.Gray),
            Background = Fluent.Clear(),
        };
        ToolTipService.SetToolTip(ring, Str.WidgetsC_Tasks_MarkDone.Text());
        ring.Tapped += (_, args) =>
        {
            args.Handled = true;
            _ = CompleteAsync(task);
        };
        line.Children.Add(ring);

        var title = Fluent.Text(FluentType.Body);
        title.Text = task.DisplayTitle;
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(title, 1);
        line.Children.Add(title);

        var row = Row(line, height);
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

    private static TextBlock Note(string text, double height)
    {
        var note = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        note.Text = text;
        note.Height = height;
        note.Padding = new Thickness(0, Fluent.Space1, 0, 0);
        note.TextTrimming = TextTrimming.CharacterEllipsis;
        return note;
    }

    private Border Row(Grid line, double height)
    {
        var row = new Border
        {
            Child = line,
            Height = height,
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
        };
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

    /// <summary>[S-566] An event's calendar colour, resolved at draw time so a settings change repaints at once.</summary>
    private Brush ColorOf(CalendarEvent item)
    {
        var hex = CalendarRules.ResolveColor(CalendarRules.ColorKey(item.AccountId, item.CalendarId), item.SourceIndex, _config.CalendarColors);

        if (!_colorBrushes.TryGetValue(hex, out var brush))
        {
            brush = HexColor.TryParse(hex, out var r, out var g, out var b)
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b))
                : Accent();
            _colorBrushes[hex] = brush;
        }

        return brush;
    }

    private static Brush Accent() => ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue);

    private static Brush Caution() => ThemeBrush.Of("SystemFillColorCautionBrush", Colors.Goldenrod);

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
