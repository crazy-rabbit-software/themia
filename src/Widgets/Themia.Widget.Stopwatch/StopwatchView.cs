using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Stopwatch;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4600]..[S-4606] A stopwatch (Karpo §84) on the timers' face ([S-6739]): the time as the Readout,
/// centred, the lap under way its line under, the Controls row under it (Start, Pause or Resume as the
/// one main action, Lap and Reset greyed, never gone, where they do nothing) and, from an expanded
/// width, the laps beside it, newest first, as many whole rows as the tile holds.
/// <para>
/// The stopwatch is the widget's config ([S-4602]): written on every press, read back on attach, so
/// it keeps running across a restart. In memory it counts on the monotonic clock; the shared ticker
/// wakes it once a second only while it runs ([S-155]), so a running time is whole seconds and a
/// paused one shows its tenth ([S-4603]).
/// </para>
/// </summary>
internal sealed partial class StopwatchView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    /// <summary>Every view of one stopwatch (the same view on two displays): one press reaches all of them.</summary>
    private static event Action<StopwatchConfig>? Acted;

    private readonly Grid _face = new();
    private readonly ReadoutBlock _clock = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly ControlsBlock _controls = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _startButton;
    private readonly Button _lapButton;
    private readonly Button _resetButton;
    private readonly StackPanel _laps = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly StatusBlock _noLaps = new();
    private readonly ColumnDefinition _lapColumn = new() { Width = new GridLength(0) };

    private Ticker? _ticker;
    private StopwatchConfig _config = new();
    private StopwatchState _state = StopwatchState.None;
    private IDisposable? _subscription;
    private IWidgetSurface? _surface;
    private WidgetArea _area;
    private StopwatchPlan _plan;
    private bool _hours;

    public StopwatchView()
    {
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(_lapColumn);

        _face.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _face.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _face.Children.Add(_clock);
        SetRow(_controls, 1);
        _face.Children.Add(_controls);

        // [S-4601] Start first, so the icons give way before it: Lap, then Reset, from the last.
        _startButton = _controls.Main(StopwatchStr.Stopwatch_Start.Text(), () => Act(state => state.Act(Now())));
        _lapButton = _controls.Icon(StopwatchModule.LapGlyph, StopwatchStr.Stopwatch_Lap.Text(), () => Act(state => state.Lap(Now())));
        _resetButton = _controls.Icon(Glyphs.Reset, StopwatchStr.Stopwatch_Reset.Text(), () => Act(_ => StopwatchState.None));

        SetColumn(_laps, 1);
        SetColumn(_noLaps, 1);
        _noLaps.Hide();
        Children.Add(_face);
        Children.Add(_laps);
        Children.Add(_noLaps);
    }

    /// <summary>The monotonic clock the stopwatch counts on: setting Windows' clock never moves it.</summary>
    private static TimeSpan Now() => System.Diagnostics.Stopwatch.GetElapsedTime(0);

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        Acted -= OnActed;
        Acted += OnActed;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var next = config as StopwatchConfig ?? new StopwatchConfig();

        // Only another config is read again: the one held is this view's own writing.
        if (!ReferenceEquals(next, _config))
        {
            _config = next;
            _state = _config.Record.ToState(DateTimeOffset.UtcNow, Now());
        }

        Run();
        Render();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    private void OnLanguage(Locale locale)
    {
        Tooltips();
        Render();
    }

    /// <summary>Another view of the same stopwatch pressed a button: take what it wrote.</summary>
    private void OnActed(StopwatchConfig config)
    {
        if (!ReferenceEquals(config, _config) || _ticker is null)
        {
            return;
        }

        _state = _config.Record.ToState(DateTimeOffset.UtcNow, Now());
        Run();
        Render();
    }

    /// <summary>A press: the new state, written into the config ([S-4602]) and saved, every view of it told.</summary>
    private void Act(Func<StopwatchState, StopwatchState> change)
    {
        if (_ticker is null)
        {
            return;
        }

        var next = change(_state);
        if (next == _state)
        {
            return;
        }

        _state = next;
        _config.Store(StopwatchRecord.Of(_state, DateTimeOffset.UtcNow, Now()));
        _surface?.RequestSave();
        Run();
        Render();
        Acted?.Invoke(_config);
    }

    /// <summary>[S-155] Subscribed to the second only while it runs.</summary>
    private void Run()
    {
        var wanted = _state.Running && _ticker is not null;

        if (wanted == (_subscription is not null))
        {
            return;
        }

        if (wanted)
        {
            _subscription = _ticker!.Subscribe(TimeSpan.FromSeconds(1), Render);
        }
        else
        {
            _subscription?.Dispose();
            _subscription = null;
        }
    }

    /// <summary>[S-4604] Space or Enter start and pause, Right a lap, Home reset.</summary>
    public bool HandleKey(WidgetKey key) => Do(StopwatchKeys.Of(key));

    /// <summary>[S-4604] L a lap, R reset.</summary>
    public bool HandleText(char character) => Do(StopwatchKeys.Letter(character));

    private bool Do(ControlKey command)
    {
        if (_ticker is null)
        {
            return false;
        }

        switch (command)
        {
            case ControlKey.Toggle:
                Act(state => state.Act(Now()));
                return true;
            case ControlKey.Lap:
                Act(state => state.Lap(Now()));
                return true;
            case ControlKey.Reset:
                Act(_ => StopwatchState.None);
                return true;
            default:
                return false;
        }
    }

    private void Tooltips()
    {
        ControlsBlock.Reglyph(_lapButton, StopwatchModule.LapGlyph, StopwatchStr.Stopwatch_Lap.Text());
        ControlsBlock.Reglyph(_resetButton, Glyphs.Reset, StopwatchStr.Stopwatch_Reset.Text());
        ToolTipService.SetToolTip(_lapButton, StopwatchStr.Stopwatch_Lap.Text());
        ToolTipService.SetToolTip(_resetButton, StopwatchStr.Stopwatch_Reset.Text());
    }

    // ---- drawing ----------------------------------------------------------------------------

    private string Time(TimeSpan elapsed, bool tenths)
    {
        var locale = Strings.Current;
        return LocaleFormat.Digits(locale, StopwatchText.Time(elapsed, tenths, LocaleFormat.DecimalSeparator(locale)));
    }

    private void Render()
    {
        var now = Now();
        var elapsed = _state.Elapsed(now);

        // From an hour up the time is three figures wider: the face is planned again once.
        var hours = elapsed >= TimeSpan.FromHours(1);
        if (hours != _hours)
        {
            _hours = hours;
            Lay();
            return;
        }

        // [S-4603] Running: whole seconds, the one the ticker can keep. Stopped: the tenth too.
        var laps = _state.Splits;
        var lapLine = _state.Phase == StopwatchPhase.Paused && laps.Length == 0
            ? StopwatchStr.Stopwatch_Paused.Text()
            : laps.Length == 0 ? null
            : $"{StopwatchStr.Stopwatch_LapNumber.Format(("n", LocaleFormat.Integer(laps.Length + 1)))} · {Time(_state.CurrentLap(now), tenths: !_state.Running)}";
        _clock.Show(
            Time(elapsed, tenths: !_state.Running && !_state.Idle),
            under: _plan.Face.Under ? lapLine : null,
            valueRole: _state.Phase == StopwatchPhase.Paused ? ColourRole.TextSecondary : ColourRole.TextPrimary);

        ControlsBlock.Relabel(_startButton, (_state.Running ? StopwatchStr.Stopwatch_Pause : _state.Idle ? StopwatchStr.Stopwatch_Start : StopwatchStr.Stopwatch_Resume).Text());

        // [S-4601] Greyed where they do nothing, never taken away: the row never moves.
        _lapButton.IsEnabled = _state.CanLap;
        _resetButton.IsEnabled = _state.CanReset;

        FillLaps();
    }

    /// <summary>[S-4605] The newest laps that fit, each a fact row: "Lap N", its length, its total on the second line.</summary>
    private void FillLaps()
    {
        var laps = _state.Splits;
        var shown = Math.Min(_plan.LapRows, laps.Length);

        while (_laps.Children.Count < shown)
        {
            _laps.Children.Add(new RowBlock(RowKind.Fact, lead: 0));
        }

        for (var i = 0; i < _laps.Children.Count; i++)
        {
            var row = (RowBlock)_laps.Children[i];
            row.Visibility = i < shown ? Visibility.Visible : Visibility.Collapsed;

            if (i < shown && _state.LapAt(laps.Length - 1 - i) is { } lap)
            {
                row.Lines = _plan.LapLines;
                row.Show(StopwatchStr.Stopwatch_LapNumber.Format(("n", LocaleFormat.Integer(laps.Length - i))), Time(lap.Length, true), Time(lap.Total, true));
            }
        }

        // "No laps yet" stands where the laps will, the Status row of a list with nothing in it.
        if (_plan.LapColumn > 0 && laps.Length == 0)
        {
            _noLaps.Show(StatusKind.Empty, StopwatchModule.LapGlyph, StopwatchStr.Stopwatch_NoLaps.Text());
        }
        else
        {
            _noLaps.Hide();
        }
    }

    // ---- the face ---------------------------------------------------------------------------

    /// <summary>[S-4605] What a tile this size shows (<see cref="StopwatchFace"/>): the face, and the laps' column from an expanded width.</summary>
    private void Lay()
    {
        var area = _area.IsMeasured ? _area : new WidgetArea(ActualWidth, ActualHeight);
        if (!area.IsMeasured)
        {
            return;
        }

        _plan = StopwatchFace.Plan(area, _hours);
        var side = _plan.LapColumn > 0;
        _lapColumn.Width = new GridLength(_plan.LapColumn);
        _face.Margin = new Thickness(FamilySpace.Inset, 0, side ? 0 : FamilySpace.Inset, FamilySpace.Inset);
        _face.RowSpacing = _plan.Face.Controls ? FamilySpace.S2 : 0;
        _controls.Visibility = _plan.Face.Controls ? Visibility.Visible : Visibility.Collapsed;
        _laps.Visibility = side ? Visibility.Visible : Visibility.Collapsed;
        _clock.Size(area, _plan.FaceWidth, _plan.Face.Box, new ReadoutText(StopwatchFace.Widest(_hours), LineUnder: true, OneReadout: true));
        Render();
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        Acted -= OnActed;
        _subscription?.Dispose();
        _subscription = null;

        // A key the lease delivers after the card left ([S-802]) must not press a detached stopwatch.
        _ticker = null;
    }
}
