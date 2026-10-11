using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Habits;
using Themia.App.Shell;
using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1420]..[S-1429] A list of habits, a box to tick each period, how long the run is, and what
/// the last few periods looked like.
/// <para>
/// The habits are not in this widget's config. They live in the device-wide pool
/// (<see cref="HabitPool"/>), and the config names the list to show, so two widgets can track the
/// same habits and a tick in one is a tick in the other. Every change — a tick, an add, a rename, a
/// move, a delete — goes through the pool's one write path and comes back as its
/// <see cref="HabitPool.Changed"/>, so this view draws only what the pool holds.
/// </para>
/// <para>
/// It is a Windows 11 list with a size ladder (<see cref="HabitLayout"/>): a tile that grows shows
/// more — the run, the history dots, an add row, a rate and best run per habit, a summary, a chart —
/// rather than bigger rows.
/// </para>
/// </summary>
internal sealed partial class HabitTrackerView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetHeaderContent, IWidgetSizeAware, IWidgetKeys
{
    private const string Scope = "habits";

    private readonly TextBlock _done = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    private readonly StackPanel _list = new() { Spacing = 0 };
    private readonly ScrollViewer _scroll;
    private readonly Border _emptyHost;
    private readonly TextBlock _emptyText;

    // The summary strip: what is done this period, the recent rate, and a bar.
    private readonly Grid _summary;
    private readonly TextBlock _summaryDone = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly TextBlock _summaryRate = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    private readonly ColumnDefinition _barDone = new() { Width = new GridLength(0, GridUnitType.Star) };
    private readonly ColumnDefinition _barLeft = new() { Width = new GridLength(1, GridUnitType.Star) };
    private Grid _summaryBar = null!;

    private readonly Brush _accent = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
    private readonly Brush _track = FluentLayout.Themed("ControlAltFillColorSecondaryBrush", Colors.Gray);

    private IWidgetSurface? _surface;
    private Ticker? _ticker;
    private HabitPool? _pool;
    private HabitTrackerConfig _config = new();
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;

    private HabitList? _habitList;
    private WidgetArea _area;
    private HabitLayoutPlan? _plan;

    /// <summary>The period every row is drawn against, read from the clock once per render.</summary>
    private HabitBucket _current;

    // This period's ticks and the recent window's, counted once per render for the summary.
    private int _doneNow;
    private int _doneWindow;

    private bool _resolving;
    private bool _detached;

    public HabitTrackerView()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                            // summary
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                            // rows / empty hint
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                            // add row

        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_done, FontNumeralAlignment.Tabular);

        _scroll = new ScrollViewer
        {
            Content = _list,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };

        _emptyText = Fluent.Text(FluentType.Body);
        _emptyHost = new Border
        {
            Child = Fluent.EmptyState(Glyphs.Habit, _emptyText, out _),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        _summary = BuildSummary();
        _addRow = BuildAddRow();

        SetRow(_summary, 0);
        SetRow(_scroll, 1);
        SetRow(_emptyHost, 1);
        SetRow(_addRow, 2);

        Children.Add(_summary);
        Children.Add(_scroll);
        Children.Add(_emptyHost);
        Children.Add(_addRow);
    }

    /// <summary>The "3/5" count, in the card's header.</summary>
    public FrameworkElement? HeaderTrailing => _done;

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _pool = AppServices.Habits;

        if (_pool is not null)
        {
            _pool.Changed += OnPoolChanged;
        }

        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as HabitTrackerConfig ?? new HabitTrackerConfig();
        ResolveList();

        if (Resubscribe())
        {
            return;
        }

        Render();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>
    /// [S-1420] Find the list this widget shows. With none chosen it takes the device's first list,
    /// creating one only when there is none, and remembers it; a list that was deleted is not
    /// replaced — the widget says so instead of showing somebody else's habits.
    /// </summary>
    private void ResolveList()
    {
        if (_pool is null || _resolving)
        {
            _habitList = null;
            return;
        }

        if (string.IsNullOrEmpty(_config.ListId))
        {
            _resolving = true;

            try
            {
                if (_pool.FirstOrCreate() is { } id)
                {
                    _config.ListId = id;
                    _surface?.RequestSave();
                }
            }
            finally
            {
                _resolving = false;
            }
        }

        _habitList = _pool.Find(_config.ListId);
        _surface?.SetHeaderTitle(_habitList is { Name.Length: > 0 } list ? list.Name : null);
    }

    /// <summary>Only when the cadence changed; a new subscription ticks at once, so the caller is done.</summary>
    private bool Resubscribe()
    {
        var interval = _config.TickInterval;

        if (_subscription is not null && interval == _subscribedInterval)
        {
            return false;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, OnTick);
        return _subscription is not null;
    }

    /// <summary>
    /// [S-1422] The only reason this widget wakes: a box ticked in a past period clears when the
    /// period does. Nothing is redrawn unless the period actually changed.
    /// </summary>
    private void OnTick()
    {
        if (_detached)
        {
            return;
        }

        if (_habitList is not null && Bucket(DateTimeOffset.Now).Key == _current.Key)
        {
            return;
        }

        Render();
    }

    private void OnPoolChanged()
    {
        if (_detached || _resolving)
        {
            return;
        }

        _habitList = _pool?.Find(_config.ListId);
        _surface?.SetHeaderTitle(_habitList is { Name.Length: > 0 } list ? list.Name : null);
        Render();
    }

    private HabitBucket Bucket(DateTimeOffset moment) => HabitPeriods.At(moment, _config.Cadence, TimeZoneInfo.Local);

    private IReadOnlyList<Habit> Habits => _habitList?.Habits ?? [];

    /// <summary>
    /// Draw the habits. The clock is read <b>once</b> here and passed down: a list drawn across a
    /// period boundary that read it per row would tick one habit against today and the next against
    /// yesterday, in the same frame.
    /// </summary>
    private void Render()
    {
        if (_detached)
        {
            return;
        }

        _current = Bucket(DateTimeOffset.Now);

        var habits = Habits;
        SyncRows(habits);

        var cadence = _config.Cadence;
        var zone = TimeZoneInfo.Local;
        var doneNow = 0;
        var doneWindow = 0;

        for (var i = 0; i < _rows.Count; i++)
        {
            var log = HabitRules.Log(habits[i].Log);

            if (HabitRules.IsDone(log, _current))
            {
                doneNow++;
            }

            doneWindow += HabitRules.DoneIn(log, _current, cadence, zone, HabitRules.RateWindow);
            _rows[i].Update(habits[i], log, _current, cadence, _config.EffectiveHistoryDots, _config.ShowStreak);
        }

        _done.Text = habits.Count == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{doneNow}/{habits.Count}");
        AutomationProperties.SetName(_done, habits.Count == 0
            ? string.Empty
            : Str.WidgetsB_Habits_DoneOf.Format(("done", doneNow), ("total", habits.Count), ("when", HabitPeriods.CurrentNoun(cadence.Period))));

        _doneNow = doneNow;
        _doneWindow = doneWindow;
        _emptyText.Text = EmptyMessage();
        Lay();
    }

    private string EmptyMessage()
    {
        if (_pool is null)
        {
            return Str.WidgetsB_Habits_Unavailable.Text();
        }

        if (_habitList is null)
        {
            return string.IsNullOrEmpty(_config.ListId) || !_pool.Writable
                ? Str.WidgetsB_Habits_Unreadable.Text()
                : Str.WidgetsB_Habits_ListDeleted.Text();
        }

        return Writable ? Str.WidgetsB_Habits_EmptyAdd.Text() : Str.WidgetsB_Habits_Empty.Text();
    }

    /// <summary>[S-1427] Apply the ladder to what is drawn. Cheap: nothing is rebuilt here.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var count = Habits.Count;
        var plan = HabitLayout.Plan(
            _area,
            new HabitLayoutInput(count, Writable, _config.ShowStreak, _config.EffectiveHistoryDots),
            Fluent.RowHeight,
            Fluent.RowHeightCompact);

        _plan = plan;

        _emptyHost.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _scroll.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;

        // An empty list's hint takes the room the rows would have, so it can shed its glyph on a
        // small tile instead of being measured at its full height. Under a list that all fits the
        // rest of the tile stays empty: nothing but the habits fills it.
        RowDefinitions[1].Height = count == 0 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;

        // Whole rows in the viewport: the rest scroll, and the last visible row is never cut in half.
        _scroll.Height = plan.Rows < count ? plan.Rows * plan.RowHeight : double.NaN;
        _scroll.Padding = plan.AddRow ? new Thickness(0) : Fluent.ListPadding;

        foreach (var row in _rows)
        {
            row.SetLayout(plan);
        }

        FitAddRow(plan);

        _summary.Visibility = plan.Summary ? Visibility.Visible : Visibility.Collapsed;
        _summaryBar.Visibility = plan.SummaryBar ? Visibility.Visible : Visibility.Collapsed;
        if (plan.Summary)
        {
            DrawSummary();
        }
        _summary.Padding = new Thickness(plan.Inset, 0, plan.Inset, Fluent.Space3);
    }

    // ---- the summary ([S-1427]) ------------------------------------------------------------------------

    private Grid BuildSummary()
    {
        var line = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        _summaryDone.TextTrimming = TextTrimming.CharacterEllipsis;
        _summaryRate.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_summaryRate, FontNumeralAlignment.Tabular);
        SetColumn(_summaryRate, 1);
        line.Children.Add(_summaryDone);
        line.Children.Add(_summaryRate);

        var filled = new Border { Background = _accent, CornerRadius = new CornerRadius(2) };
        var bar = new Grid
        {
            Height = 4,
            Margin = new Thickness(0, Fluent.Space1, 0, 0),
            CornerRadius = new CornerRadius(2),
            Background = _track,
            ColumnDefinitions = { _barDone, _barLeft },
        };
        bar.Children.Add(filled);

        var summary = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
            },
            Visibility = Visibility.Collapsed,
        };

        SetRow(bar, 1);
        summary.Children.Add(line);
        summary.Children.Add(bar);
        _summaryBar = bar;
        return summary;
    }

    private void DrawSummary()
    {
        var doneNow = _doneNow;
        var doneWindow = _doneWindow;
        var count = Habits.Count;
        var period = _config.Cadence.Period;
        _summaryDone.Text = count == 0 ? string.Empty : Str.WidgetsB_Habits_DoneOf.Format(("done", doneNow), ("total", count), ("when", HabitPeriods.CurrentNoun(period)));

        var rate = count == 0 ? 0 : (int)Math.Round(100.0 * doneWindow / (count * HabitRules.RateWindow));
        var window = Str.WidgetsB_Habits_LastUnits.Format(("units", HabitPeriods.Units(period, HabitRules.RateWindow)));
        var percent = LocaleFormat.Percent(Strings.Current, rate);

        // On a narrow tile the rate stands alone, so the count beside it is not cut; it says the rest
        // on hover and to a screen reader.
        _summaryRate.Text = _area.WidthClass >= SizeClass.Expanded
            ? $"{percent} · {window}"
            : percent;
        var rateWords = Str.WidgetsB_Habits_RateOver.Format(("percent", percent), ("window", window));
        ToolTipService.SetToolTip(_summaryRate, rateWords);
        AutomationProperties.SetName(_summaryRate, rateWords);

        _barDone.Width = new GridLength(doneNow, GridUnitType.Star);
        _barLeft.Width = new GridLength(Math.Max(0, count - doneNow), GridUnitType.Star);
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        // What was being typed is kept: the pool outlives this view.
        CommitEdit(keepAdding: false);
        CancelDrag();
        ReleaseRowFocus();
        _detached = true;

        if (_pool is not null)
        {
            _pool.Changed -= OnPoolChanged;
        }

        _subscription?.Dispose();
        _subscription = null;
        _subscribedInterval = TimeSpan.Zero;

        foreach (var row in _rows)
        {
            row.Detach();
        }

        _rows.Clear();
        _list.Children.Clear();
        DetachAddRow();
        _ticker = null;
        _surface?.RequestTextInput(false);
    }
}
