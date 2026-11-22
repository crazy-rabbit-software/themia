using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.HabitTracker;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1420]..[S-1429] A list of habits, a box to tick each period, how long the run is, and what the last few periods
/// looked like: the kit's check look ([S-7074], [S-7037], <see cref="HabitSpec"/>, as Tasks), the history dots at each
/// row's end (<see cref="KitCheckLook.Trail"/>); the heatmap style draws its own grids ([S-6409]).
/// <para>
/// The habits are not in this widget's config. They live in the device-wide pool (<see cref="IWidgetHost.Habits"/>), and
/// the config names the list to show, so two widgets can track the same habits and a tick in one is a tick in the
/// other. Every change — a tick, an add, a rename, a move, a delete — goes through the pool's one write path and comes
/// back as its <see cref="IHabitLists.Changed"/>, so this view draws only what the pool holds.
/// </para>
/// </summary>
internal sealed partial class HabitTrackerView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetSizeAware, IWidgetKeys, IWidgetOwnsDrags
{
    private const string Scope = "habits";

    private readonly KitList _list;
    private readonly KitCheckLook _look;
    private readonly KitStatePanel _state;

    private IWidgetSurface? _surface;
    private Ticker? _ticker;
    private readonly IWidgetHost _host;
    private IHabitLists? _pool;
    private HabitTrackerConfig _config = new();

    /// <summary>The ticker subscription at the cadence the view needs now (<see cref="Ticker.Slot"/>).</summary>
    private readonly Ticker.Slot _tick = new();

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
        _look = new KitCheckLook { Trail = Dots };
        _list = new KitList(host, HabitSpec.Spec, item => HabitSpec.Actions(item, _config.Cadence.Period, Writable), Run, _look)
        {
            Renamed = (item, name) => Rename(item.Id, name),
            Added = Add,
            Reordered = Reorder,
            TextInput = _ => UpdateKeyboard(),
        };

        // [S-6714] No list, no pool or no habits: the kit's state row where the first habit would be, over the list's slot.
        _state = new KitStatePanel(HabitTrackerModule.Glyph, host) { Visibility = Visibility.Collapsed };
        Children.Add(_list);
        _list.Overlay(_state);
        BuildHeatmap();
    }

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _pool = _host.Habits;
        _list.Attach();
        _links.Join(_surface?.Links);

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
        Render();
    }

    /// <summary>[S-1426] [S-14410] The dots a row shows: what the card's width holds (its natural width's before it is measured).</summary>
    private int HistoryDots => HabitSizing.HistoryFor(_config, _area.IsMeasured ? _area.Width : HabitSizing.Sizing(_config).Natural.Width);

    /// <summary>Whether a habit can be added or changed here: a list is chosen and the pool can be written.</summary>
    private bool Writable => _pool is { Writable: true } && _habitList is not null;

    /// <summary>
    /// [S-1420] Find the list this widget shows. With none chosen it takes the device's first list, creating one only
    /// when there is none, and remembers it; a list that was deleted is not replaced — the widget says so instead of
    /// showing somebody else's habits.
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
    }

    /// <summary>Only when the cadence changed; a new subscription ticks at once, so the caller is done.</summary>
    private bool Resubscribe() => _tick.Set(_ticker, _config.TickInterval, OnTick);

    /// <summary>
    /// [S-1422] The only reason this widget wakes: a box ticked in a past period clears when the period does. Nothing is
    /// redrawn unless the period actually changed.
    /// </summary>
    private void OnTick()
    {
        if (_detached || (_habitList is not null && Bucket(DateTimeOffset.Now).Key == _current.Key))
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
        Render();
    }

    private HabitBucket Bucket(DateTimeOffset moment) => HabitPeriods.At(moment, _config.Cadence, TimeZoneInfo.Local);

    private IReadOnlyList<Habit> Habits => _habitList?.Habits ?? [];

    /// <summary>
    /// Draw the habits. The clock is read <b>once</b> here and passed down: a list drawn across a period boundary that
    /// read it per row would tick one habit against today and the next against yesterday, in the same frame.
    /// </summary>
    private void Render()
    {
        if (_detached)
        {
            return;
        }

        _current = Bucket(DateTimeOffset.Now);

        var habits = Habits;
        var heat = IsHeatmap;
        var checklist = _config.Style == HabitTrackerStyle.Checklist;

        // [S-6409] The heatmap draws the history, not rows: no add row beside it, except under an empty list, which has
        // nothing to draw and still needs a way to add its first habit.
        var plan = HabitLayout.Plan(new HabitLayoutInput(habits.Count, Writable, _config.ShowStreak, HistoryDots,
            Checklist: checklist, Rows: _config.Rows, Lines: _config.Lines, RunWords: _config.RunWords, Heatmap: heat));
        _plan = plan;

        // [S-15771] The run's column kept in every row, so the dots of a row without a run stand where the others' do.
        _look.ValueWidth = plan.ValueWidth;

        var kitPlan = KitPlanner.Plan(_area, HabitSpec.Spec, new KitChoices(KitShow.Subtitle | (plan.Streak ? KitShow.When : KitShow.None))
        {
            Count = habits.Count,
            CanAdd = plan.AddRow,
            OneLine = checklist,
            Rows = new KitRowsChoice(plan.Rows, plan.Lines),
        });

        LayHeatmap(habits.Count);
        _list.Visibility = heat && habits.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

        if (_habitList is null)
        {
            _list.Clear(kitPlan);
        }
        else
        {
            var locale = Strings.Current;
            var cadence = _config.Cadence;
            _list.Show([.. habits.Select(h => HabitSpec.Item(h, HabitRules.Log(h.Log), _current, cadence, plan, locale))], kitPlan, _area, DateTimeOffset.Now);
        }

        if (habits.Count == 0)
        {
            var (problem, sentence) = EmptyState();
            _state.RowPitch = kitPlan.RowHeight;
            _state.Show(sentence, offerAccounts: false, problem: problem);
        }
        else
        {
            _state.Hide();
        }
    }

    /// <summary>
    /// [S-6714] What the state row says with no habit to draw: no pool and an empty list are plain states; a list that was
    /// deleted or lists that could not be read are a problem.
    /// </summary>
    private (bool Problem, string Sentence) EmptyState()
    {
        if (_pool is null)
        {
            return (false, HabitTrackerStr.WidgetsB_Habits_Unavailable.Text());
        }

        if (_habitList is null)
        {
            return (true, string.IsNullOrEmpty(_config.ListId) || !_pool.Writable
                ? HabitTrackerStr.WidgetsB_Habits_Unreadable.Text()
                : HabitTrackerStr.WidgetsB_Habits_ListDeleted.Text().Replace('\n', ' '));
        }

        return (false, HabitTrackerStr.WidgetsB_Habits_Empty.Text());
    }

    /// <summary>
    /// [S-1426] A habit's history at its row's end: the newest periods the card's width holds as dots, done the accent and
    /// an open period the track ([S-6717]), each its period in the cadence's words and its state.
    /// </summary>
    private KitTrail? Dots(KitItem item)
    {
        var count = _plan?.Dots ?? 0;
        if (count <= 0 || Habits.FirstOrDefault(h => h.Id == item.Id) is not { } habit)
        {
            return null;
        }

        var cadence = _config.Cadence;
        var recent = HabitRules.Recent(HabitRules.Log(habit.Log), _current, cadence, TimeZoneInfo.Local, count);
        var history = new StackPanel { Orientation = Orientation.Horizontal, Spacing = HabitLayout.DotGap, VerticalAlignment = VerticalAlignment.Center };
        foreach (var dot in recent)
        {
            var mark = new Microsoft.UI.Xaml.Controls.Border
            {
                Width = HabitLayout.DotSize,
                Height = HabitLayout.DotSize,
                CornerRadius = Family.Round(HabitLayout.DotSize, HabitLayout.DotSize),
                VerticalAlignment = VerticalAlignment.Center,
                Background = Family.Brush(dot.Done ? ColourRole.Accent : ColourRole.Track),
            };

            var label = (dot.Done ? HabitTrackerStr.WidgetsB_Habits_DotDone : HabitTrackerStr.WidgetsB_Habits_DotNotDone).Format(("period", HabitPeriods.Describe(dot.Bucket, cadence)));
            ToolTipService.SetToolTip(mark, label);
            AutomationProperties.SetName(mark, label);
            history.Children.Add(mark);
        }

        return recent.Count == 0 ? null : new KitTrail(history, (recent.Count * HabitLayout.DotPitch) - HabitLayout.DotGap);
    }

    // ---- changes: every one goes through the pool ---------------------------------------------------

    /// <summary>[S-1425] A habit's action: tick it, rename it in place, delete it.</summary>
    private void Run(KitItem item, KitAction action)
    {
        switch (action.Id)
        {
            case HabitSpec.ToggleId:
                Toggle(item.Id);
                break;

            case HabitSpec.RenameId when Writable:
                _list.BeginRename(item);
                break;

            case HabitSpec.DeleteId:
                Delete(item.Id);
                break;
        }
    }

    /// <summary>
    /// [S-1421] Tick or un-tick the period the box shows. The clock is read once, here, and the period it gives is both
    /// written and drawn — writing one period while drawing another left the original's box dead for a tick after every
    /// rollover.
    /// </summary>
    private void Toggle(string habitId)
    {
        if (Writable && _habitList is { } list)
        {
            var key = Bucket(DateTimeOffset.Now).Key;
            _pool!.Update(list.Id, habits => HabitLists.ToggleHabit(habits, habitId, key));
        }
    }

    /// <summary>[S-1425] [S-786] The order a drag (or Alt+Shift+Up / Down, [S-3802]) left the habits in.</summary>
    private void Reorder(IReadOnlyList<string> ids)
    {
        if (Writable && _habitList is { } list)
        {
            _pool!.Update(list.Id, habits => HabitLists.ReorderHabits(habits, ids));
        }
    }

    /// <summary>
    /// [S-1425] Delete a habit and its log, without a dialog: it sits behind a right-click or a focused row's Delete key,
    /// never a stray click.
    /// </summary>
    private void Delete(string habitId)
    {
        if (Writable && _habitList is { } list)
        {
            _pool!.Update(list.Id, habits => HabitLists.RemoveHabit(habits, habitId));
        }
    }

    /// <summary>[S-1425] A blank or unchanged name commits nothing.</summary>
    private void Rename(string habitId, string name)
    {
        if (Writable && _habitList is { } list && name.Trim() is { Length: > 0 } trimmed && Habits.FirstOrDefault(h => h.Id == habitId)?.Name != trimmed)
        {
            _pool!.Update(list.Id, habits => HabitLists.RenameHabit(habits, habitId, trimmed));
        }
    }

    private void Add(string name)
    {
        if (Writable && _habitList is { } list && name.Trim() is { Length: > 0 } trimmed)
        {
            _pool!.Update(list.Id, habits => HabitLists.AddHabit(habits, trimmed, HabitLists.NewId("habit")));
        }
    }

    // ---- the keyboard ([S-1428]) --------------------------------------------------------------------

    /// <summary>[S-809] Whether an editor is being typed into: the text box's share of the shell's keyboard lease.</summary>
    private void UpdateKeyboard() => _surface?.RequestTextInput(!_detached && _list.Editing);

    /// <summary>[S-1428] The kit's one key map over the rows; the heatmap has no rows to move between, its keys stay the shell's.</summary>
    public bool HandleKey(WidgetKey key) => !_detached && !IsHeatmap && _list.HandleKey(key);

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _links.Leave();

        if (_pool is not null)
        {
            _pool.Changed -= OnPoolChanged;
        }

        _tick.Dispose();
        _list.Detach();
        _state.Detach();
        ClearHeatmap();
        _ticker = null;
        _surface?.RequestTextInput(false);
    }
}
