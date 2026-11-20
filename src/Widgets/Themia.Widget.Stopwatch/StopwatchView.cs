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
using Themia.Sdk;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4600]..[S-4606] A stopwatch (Karpo §84) on the timers' sized face ([S-11302]): the time as the
/// Readout on the family's one-readout step, centred, the lap under way its line under, the Controls row under
/// it (Start, Pause or Resume as the one main action, Lap and Reset greyed, never gone, where they do
/// nothing) and under that the newest laps, as many rows as it lists.
/// <para>
/// The stopwatch is the widget's config ([S-4602]): written on every press, read back on attach, so
/// it keeps running across a restart. In memory it counts on the monotonic clock; the shared ticker
/// wakes it once a second only while it runs ([S-155]), so a running time is whole seconds and a
/// paused one shows its tenth ([S-4603]).
/// </para>
/// </summary>
internal sealed partial class StopwatchView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetKeys
{
    /// <summary>Every view of one stopwatch (the same view on two displays): one press reaches all of them.</summary>
    private static event Action<StopwatchConfig>? Acted;

    /// <summary>[S-12808] The block, planned at the natural box and scaled up as one (in the view's padding).</summary>
    private readonly ReadingFit _fit = new() { Margin = default };

    private readonly ReadoutBlock _clock = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly ControlsBlock _controls = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _startButton;
    private readonly Button _lapButton;
    private readonly Button _resetButton;
    /// <summary>[S-12811] The laps as the rows every facts widget draws.</summary>
    private readonly FactList _laps = new();

    private Ticker? _ticker;
    private StopwatchConfig _config = new();
    private StopwatchState _state = StopwatchState.None;
    private IDisposable? _subscription;
    private IWidgetSurface? _surface;
    private bool _hours;

    public StopwatchView()
    {
        // [S-11302] The face, its controls 8 under it, the laps under them; the inset at the sides and the foot.
        Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        // [S-12808] The face, its controls and the laps: one block, planned at the natural box and scaled up as one.
        _controls.Margin = new Thickness(0, FamilySpace.S2, 0, 0);

        // A Row carries the side inset itself, so the laps reach the card's edges with their hover. [S-12834] The laps are
        // rows (WR-6: whole grid cells, never scaled with the face): under the block, to the card's foot edge.
        _laps.Margin = new Thickness(-FamilySpace.Inset, 0, -FamilySpace.Inset, -FamilySpace.Inset);
        _fit.Parts.Add(_clock);
        _fit.Parts.Add(_controls);
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        SetRow(_laps, 1);
        Children.Add(_fit);
        Children.Add(_laps);

        // [S-4601] Start first, then Lap and Reset.
        _startButton = _controls.Main(StopwatchStr.Stopwatch_Start.Text(), () => Act(state => state.Act(Now())));
        _lapButton = _controls.Icon(StopwatchModule.LapGlyph, StopwatchStr.Stopwatch_Lap.Text(), () => Act(state => state.Lap(Now())));
        _resetButton = _controls.Icon(Glyphs.Reset, StopwatchStr.Stopwatch_Reset.Text(), () => Act(_ => StopwatchState.None));
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

        // [S-11302] From an hour up the time shows its hours and no tenth: the face is sized for both.
        _hours = elapsed >= TimeSpan.FromHours(1);
        var tenths = StopwatchFace.Tenths(_hours);

        // [S-4603] Running: whole seconds, the one the ticker can keep. Stopped: the tenth too.
        var laps = _state.Splits;
        var lapLine = _state.Phase == StopwatchPhase.Paused && laps.Length == 0
            ? StopwatchStr.Stopwatch_Paused.Text()
            : laps.Length == 0 ? null
            : $"{StopwatchStr.Stopwatch_LapNumber.Format(("n", LocaleFormat.Integer(laps.Length + 1)))} · {Time(_state.CurrentLap(now), tenths: tenths && !_state.Running)}";
        _clock.Show(
            Time(elapsed, tenths: tenths && !_state.Running && !_state.Idle),
            under: lapLine,
            valueRole: _state.Phase == StopwatchPhase.Paused ? ColourRole.TextSecondary : ColourRole.TextPrimary);

        var main = (_state.Running ? StopwatchStr.Stopwatch_Pause : _state.Idle ? StopwatchStr.Stopwatch_Start : StopwatchStr.Stopwatch_Resume).Text();
        ControlsBlock.Relabel(_startButton, main);

        // [S-4601] Greyed where they do nothing, never taken away: the row never moves.
        _lapButton.IsEnabled = _state.CanLap;
        _resetButton.IsEnabled = _state.CanReset;

        FillLaps();
    }

    /// <summary>[S-11302] The newest laps, one fact row each ("Lap N" and its length), as many as the config lists; "No laps yet" in the first while there is none.</summary>
    private void FillLaps()
    {
        var laps = _state.Splits;
        var rows = StopwatchFace.LapRows(_config);

        var lines = new List<FactLine>();
        if (rows > 0 && laps.Length == 0)
        {
            lines.Add(new FactLine(string.Empty, StopwatchStr.Stopwatch_NoLaps.Text(), string.Empty));
        }

        for (var i = 0; i < rows && i < laps.Length; i++)
        {
            if (_state.LapAt(laps.Length - 1 - i) is { } lap)
            {
                lines.Add(new FactLine(string.Empty, StopwatchStr.Stopwatch_LapNumber.Format(("n", LocaleFormat.Integer(laps.Length - i))), Time(lap.Length, true)));
            }
        }

        _laps.Rows = rows;
        _laps.Show(lines);
    }

    // ---- the face ---------------------------------------------------------------------------

    /// <summary>[S-11302] The face, its controls and its lap rows.</summary>
    private void Lay()
    {
        var laps = StopwatchFace.LapRows(_config);
        _laps.Visibility = laps > 0 ? Visibility.Visible : Visibility.Collapsed;
        _clock.Use(StopwatchFace.Sized(_config).Step, centred: true);

        // [S-12834] With laps the block takes its natural lines grown to a grid line (its last ink on the laps' top, WR-1)
        // and the laps grow from there to the card's foot (WR-4, WR-6); without, the block fills the card.
        RowDefinitions[0].Height = laps > 0 ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        RowDefinitions[1].Height = laps > 0 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        Render();
        _fit.Height = laps > 0 ? LineRules.Under(ItemGrids.Of(this), FamilySpace.Inset, _fit.Natural()) : double.NaN;
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
