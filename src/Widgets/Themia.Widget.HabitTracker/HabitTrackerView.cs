using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.HabitTracker;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1420]..[S-1429] A list of habits, a box to tick each period, how long the run is, and what
/// the last few periods looked like.
/// <para>
/// The habits are not in this widget's config. They live in the device-wide pool
/// (<see cref="IWidgetHost.Habits"/>), and the config names the list to show, so two widgets can track the
/// same habits and a tick in one is a tick in the other. Every change — a tick, an add, a rename, a
/// move, a delete — goes through the pool's one write path and comes back as its
/// <see cref="IHabitLists.Changed"/>, so this view draws only what the pool holds.
/// </para>
/// <para>
/// It is a Windows 11 list with a size ladder (<see cref="HabitLayout"/>): a tile that grows shows
/// more — the run, the history dots, an add row, a rate and best run per habit — rather than
/// bigger rows; never a summary or a chart (concise widgets, [S-666]).
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

    private IWidgetSurface? _surface;
    private Ticker? _ticker;
    private readonly IWidgetHost _host;
    private IHabitLists? _pool;
    private HabitTrackerConfig _config = new();
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval = TimeSpan.Zero;

    private HabitList? _habitList;
    private WidgetArea _area;
    private HabitLayoutPlan? _plan;

    /// <summary>The period every row is drawn against, read from the clock once per render.</summary>
    private HabitBucket _current;

    private bool _resolving;
    private bool _detached;

    public HabitTrackerView(IWidgetHost host)
    {
        _host = host;
        // [S-3850] The rows take the tile and the add footer is pinned under them, at the bottom.
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });       // rows / empty hint
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                            // add footer

        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_done, FontNumeralAlignment.Tabular);

        _scroll = new ScrollViewer
        {
            Content = _list,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalAlignment = VerticalAlignment.Top,
        };

        _emptyText = Fluent.Text(FluentType.Body);
        _emptyHost = new Border
        {
            Child = Fluent.EmptyState(HabitTrackerModule.Glyph, _emptyText, out _),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        _addRow = BuildAddRow();

        SetRow(_scroll, 0);
        SetRow(_emptyHost, 0);
        SetRow(_addRow.Element, 1);

        Children.Add(_scroll);
        Children.Add(_emptyHost);
        Children.Add(_addRow.Element);
        BuildHeatmap();
    }

    /// <summary>The "3/5" count, in the card's header.</summary>
    public FrameworkElement? HeaderTrailing => _done;

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _pool = _host.Habits;

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
        var doneNow = 0;
        var checklist = _config.Style == HabitTrackerStyle.Checklist;

        for (var i = 0; i < _rows.Count; i++)
        {
            var log = HabitRules.Log(habits[i].Log);

            if (HabitRules.IsDone(log, _current))
            {
                doneNow++;
            }

            // [S-6410] A checklist is the current period alone: no run and no history to work out.
            _rows[i].Update(habits[i], log, _current, cadence, checklist ? 0 : _config.EffectiveHistoryDots, !checklist && _config.ShowStreak);
        }

        _done.Text = habits.Count == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{doneNow}/{habits.Count}");
        AutomationProperties.SetName(_done, habits.Count == 0
            ? string.Empty
            : HabitTrackerStr.WidgetsB_Habits_DoneOf.Format(("done", doneNow), ("total", habits.Count), ("when", HabitPeriods.CurrentNoun(cadence.Period))));

        _emptyText.Text = EmptyMessage();
        Lay();
    }

    private string EmptyMessage()
    {
        if (_pool is null)
        {
            return HabitTrackerStr.WidgetsB_Habits_Unavailable.Text();
        }

        if (_habitList is null)
        {
            return string.IsNullOrEmpty(_config.ListId) || !_pool.Writable
                ? HabitTrackerStr.WidgetsB_Habits_Unreadable.Text()
                : HabitTrackerStr.WidgetsB_Habits_ListDeleted.Text();
        }

        return Writable ? HabitTrackerStr.WidgetsB_Habits_EmptyAdd.Text() : HabitTrackerStr.WidgetsB_Habits_Empty.Text();
    }

    /// <summary>[S-1427] Apply the ladder to what is drawn. Cheap: nothing is rebuilt here.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var count = Habits.Count;

        // [S-6409] The heatmap draws the history, not rows: no add footer beside it, except under
        // an empty list, which has nothing to draw and still needs a way to add its first habit.
        var heat = IsHeatmap;
        var plan = HabitLayout.Plan(
            _area,
            new HabitLayoutInput(count, Writable && (!heat || count == 0), _config.ShowStreak, _config.EffectiveHistoryDots,
                Checklist: _config.Style == HabitTrackerStyle.Checklist),
            Fluent.RowHeight,
            Fluent.RowHeightCompact);

        _plan = plan;

        _emptyHost.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _scroll.Visibility = count == 0 || heat ? Visibility.Collapsed : Visibility.Visible;
        LayHeatmap(count);

        // The rows sit at the top of the tile and the add footer at its bottom ([S-3850]); under a
        // list that all fits, the room between stays empty: nothing but the habits fills it. An
        // empty list's hint takes that room, so it can shed its glyph on a small tile.
        // Whole rows in the viewport: the rest scroll, and the last visible row is never cut in half.
        _scroll.Height = plan.Rows < count ? plan.Rows * plan.RowHeight : double.NaN;
        _scroll.Padding = plan.AddRow ? new Thickness(0) : Fluent.ListPadding;

        foreach (var row in _rows)
        {
            row.SetLayout(plan);
        }

        FitAddRow(plan);
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
        ClearHeatmap();
        DetachAddRow();
        _ticker = null;
        _surface?.RequestTextInput(false);
    }
}
