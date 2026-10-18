using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// A focus and break timer.
/// <para>
/// The run state — phase, seconds left, whether it is going — lives in this
/// instance and nowhere else. It is deliberately not persisted and not shared ([S-150]): the same
/// widget shown on two monitors is two independent timers, which is also why only the instance that
/// is actually running can sound the cue, so a two-monitor desktop cannot echo it.
/// </para>
/// <para>
/// [S-1466] The parts are placed by hand on a canvas from one ladder pass (<see cref="Lay"/>), so
/// each size class gets its own arrangement — digits and a button at XS, a strip with the controls
/// beside, a stack, a ring on tall and large tiles — rather than one stack scaled up and down.
/// </para>
/// <para>
/// [S-6035] The style decides how the phase's progress is drawn on that ladder: Ring as above
/// (the shared <see cref="RingPart"/>), Bar the bar at every size (a large tile stacks the digits
/// over it), Digits neither, the digits taking the room.
/// </para>
/// </summary>
internal sealed partial class PomodoroView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private const string Scope = "pomodoro";

    /// <summary>The Fluent icon size, everywhere one appears in this widget.</summary>
    private const double IconSize = 16;

    /// <summary>The bar's thickness: the stock progress bar's own.</summary>
    private const double BarHeight = 4;

    /// <summary>A ring smaller than this is a donut, not a dial; the stack draws the bar instead.</summary>
    private const double RingMinimum = 120;

    /// <summary>The room a column beside the ring needs for "Next: Focus 25:00" and three buttons.</summary>
    private const double SideColumnMinimum = 180;

    private readonly Canvas _canvas = new();

    private readonly TextBlock _phase = Fluent.Text(FluentType.BodyStrong);
    private readonly FontIcon _phaseIcon = new();
    private readonly StackPanel _phaseRow;
    private readonly TextBlock _clock = Fluent.Text(FluentType.Display);
    private readonly ProgressBar _progress = new();
    private readonly RingPart _ring = new();
    private readonly TextBlock _next = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");

    private readonly Button _runButton = new();
    private readonly FontIcon _runIcon = new();
    private readonly Button _resetButton = new();
    private readonly Button _skipButton = new();
    private readonly StackPanel _controls;

    private Ticker? _ticker;
    private PomodoroConfig _config = new();
    private PomodoroState _state;
    private IDisposable? _subscription;
    private WidgetArea _area;
    private bool _preview;

    /// <summary>Whether the ring (rather than the bar) is drawn, from the last ladder pass.</summary>
    private bool _ringShown;

    /// <summary>
    /// Swallows the callback <see cref="Ticker.Subscribe"/> makes straight away ([S-155]): this
    /// subscriber does not render a tick, it <i>is</i> the tick, so the subscribe-time call would take
    /// a second off the clock the instant the user pressed start.
    /// </summary>
    private bool _skipFirstTick;

    private bool _running;

    public PomodoroView()
    {
        _state = PomodoroCycle.Fresh(_config);

        _phaseIcon.FontSize = IconSize;
        _phaseIcon.Margin = new Thickness(0, 0, Fluent.Space1, 0);
        _phaseIcon.VerticalAlignment = VerticalAlignment.Center;
        _phase.VerticalAlignment = VerticalAlignment.Center;
        _phaseRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { _phaseIcon, _phase } };

        // Tabular figures, so the seconds do not shuffle the whole readout sideways once a second.
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_clock, FontNumeralAlignment.Tabular);
        _clock.TextAlignment = TextAlignment.Center;
        _clock.TextTrimming = TextTrimming.None;

        _next.TextAlignment = TextAlignment.Center;

        _progress.Minimum = 0;
        _progress.Maximum = 100;


        Button(_runButton, _runIcon, Glyphs.Play, Str.WidgetsC_Pomodoro_Start.Text());
        Button(_resetButton, new FontIcon(), Glyphs.Reset, Str.Pomodoro_Reset.Text());
        Button(_skipButton, new FontIcon(), Glyphs.Next, Str.WidgetsC_Pomodoro_Skip.Text());

        _runButton.Click += (_, _) => SetRunning(!_running);
        _resetButton.Click += (_, _) => Reset();
        _skipButton.Click += (_, _) => Skip();

        // The primary action takes the accent style, the way the confirming button of a Windows dialog does.
        if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var accentStyle) && accentStyle is Style style)
        {
            _runButton.Style = style;
        }
        else
        {
            Log.Warn(Scope, "AccentButtonStyle is missing; the start button falls back to the standard button style");
        }

        _controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            Children = { _runButton, _resetButton, _skipButton },
        };

        _canvas.Children.Add(_ring.Root);
        _canvas.Children.Add(_phaseRow);
        _canvas.Children.Add(_clock);
        _canvas.Children.Add(_progress);
        _canvas.Children.Add(_next);
        _canvas.Children.Add(_controls);
        Children.Add(_canvas);

        Render();

        static void Button(Button button, FontIcon icon, string glyph, string tooltip)
        {
            icon.FontSize = IconSize;
            icon.Glyph = glyph;
            button.Content = icon;
            button.Padding = new Thickness(0);
            ToolTipService.SetToolTip(button, tooltip);
        }
    }

    public void Bind(IWidgetSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _preview = surface.IsPreview;

        Render();
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1110] The two fixed tooltips again; <see cref="Render"/> re-reads the rest.</summary>
    private void OnLanguage(Locale locale)
    {
        ToolTipService.SetToolTip(_resetButton, Str.Pomodoro_Reset.Text());
        ToolTipService.SetToolTip(_skipButton, Str.WidgetsC_Pomodoro_Skip.Text());
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as PomodoroConfig ?? new PomodoroConfig();

        // While the timer is stopped the clock follows the configured length, so changing it in
        // settings moves the digits and the bar together. While it is running the countdown owns
        // the remaining seconds and reloads them itself at the next flip.
        if (!_running)
        {
            _state = new PomodoroState(_state.Phase, PomodoroCycle.PhaseSeconds(_state.Phase, _config));
        }

        Render();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <summary>
    /// [S-155] Start and pause. The subscription exists only while the timer runs: a paused pomodoro
    /// has nothing to do once a second, and the shared ticker stops once nothing is subscribed.
    /// </summary>
    private void SetRunning(bool running)
    {
        if (_running == running)
        {
            return;
        }

        _running = running;

        if (running)
        {
            _skipFirstTick = true;
            _subscription = _ticker?.Subscribe(TimeSpan.FromSeconds(1), Tick);
        }
        else
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        Render();
    }

    /// <summary>[S-1987] Space or Enter start and pause, Right skips, Home resets (<see cref="ControlKeys.Pomodoro"/>).</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_ticker is null)
        {
            return false;
        }

        switch (ControlKeys.Pomodoro(key))
        {
            case ControlKey.Toggle:
                SetRunning(!_running);
                return true;

            case ControlKey.Next:
                Skip();
                return true;

            case ControlKey.Reset:
                Reset();
                return true;

            default:
                return false;
        }
    }

    /// <summary>[S-02] Reset stops and returns to a fresh work phase. Nothing sounds.</summary>
    private void Reset()
    {
        SetRunning(false);
        _state = PomodoroCycle.Fresh(_config);
        Render();
    }

    /// <summary>[S-1466] Skip to the next phase, running or not. A skip is the user's own flip: silent.</summary>
    private void Skip()
    {
        _state = PomodoroCycle.Skip(_state, _config);
        Render();
    }

    private void Tick()
    {
        if (_skipFirstTick)
        {
            _skipFirstTick = false;
            return;
        }

        if (!_running)
        {
            return;
        }

        var tick = PomodoroCycle.Advance(
            _state,
            PomodoroCycle.PhaseSeconds(PomodoroPhase.Work, _config),
            PomodoroCycle.PhaseSeconds(PomodoroPhase.Break, _config));

        _state = new PomodoroState(tick.Phase, tick.RemainingSeconds);

        // [S-152] The flip is the only thing that sounds, and it announces the phase being ENTERED.
        // Never from a preview card: it makes no sound ([S-1251]).
        if (tick.Flipped && !_preview)
        {
            SoundCue.Play(PomodoroSounds.Wave(_config.Sound, tick.Phase, _config.EffectiveVolume));
        }

        Render();
    }

    private void Render()
    {
        var work = _state.Phase == PomodoroPhase.Work;
        var total = PomodoroCycle.PhaseSeconds(_state.Phase, _config);
        var fraction = PomodoroCycle.Progress(_state, total);
        var characters = _clock.Text.Length;

        _clock.Text = PomodoroCycle.Clock(_state.RemainingSeconds);
        _phase.Text = PomodoroCycle.PhaseLabel(_state.Phase);
        _phaseIcon.Glyph = work ? Glyphs.Pomodoro : Glyphs.Timer;
        _progress.Value = fraction * 100;
        _next.Text = PomodoroCycle.NextLine(_state.Phase, _config);

        // Which phase you are in is the first thing to read off this widget, so it is carried by
        // colour as well as by the word: accent text for focus, the success colour for a break.
        var badge = work
            ? ThemeBrush.Of("AccentTextFillColorPrimaryBrush", Colors.DodgerBlue)
            : ThemeBrush.Of("SystemFillColorSuccessBrush", Colors.MediumSeaGreen);
        var fill = work
            ? ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue)
            : ThemeBrush.Of("SystemFillColorSuccessBrush", Colors.MediumSeaGreen);

        _phase.Foreground = badge;
        _phaseIcon.Foreground = badge;
        _progress.Foreground = fill;
        _ring.SetBrush(fill);

        _runIcon.Glyph = _running ? Glyphs.Pause : Glyphs.Play;
        ToolTipService.SetToolTip(_runButton, (_running ? Str.WidgetsC_Pomodoro_Pause : Str.WidgetsC_Pomodoro_Start).Text());

        if (_ringShown)
        {
            _ring.SetFraction(fraction);
        }

        // A longer phase is a longer readout ("120:00"), so the ladder re-runs when the digits
        // change width; otherwise a tick touches nothing but text.
        if (_clock.Text.Length != characters)
        {
            Lay();
        }
    }

    // ---- the ladder ------------------------------------------------------------------------------

    /// <summary>
    /// [S-1466] Decide what a tile this size shows and where. The countdown is the one thing that
    /// never drops; it takes whatever height the other rungs leave, as a hero step.
    /// <list type="bullet">
    /// <item>XS: the digits over a start/pause button.</item>
    /// <item>A short strip (S, Wide): phase and digits on the left, the controls on the right, the bar
    /// along the bottom; a wide strip adds skip and the next phase under the controls.</item>
    /// <item>M: phase, digits, bar, controls (with skip), the next phase.</item>
    /// <item>Tall, L, XL: a ring that fills with the phase, the digits inside it; beside it on a wide
    /// tile, the phase, the next phase, and the controls.</item>
    /// </list>
    /// </summary>
    private void Lay()
    {
        var area = _area.IsMeasured ? _area : new WidgetArea(ActualWidth, ActualHeight);

        if (!area.IsMeasured)
        {
            return;
        }

        var shortTile = area.HeightClass == SizeClass.Compact;

        // XS gives the digits every pixel it can: the header already leaves a gap above.
        var padY = !shortTile ? Fluent.Space4 : area.WidthClass == SizeClass.Compact ? Fluent.Space1 : Fluent.Space2;
        var box = area.Deflate(Fluent.Space4, padY);
        var side = area.Least >= SizeClass.Expanded ? Fluent.RowHeight : Fluent.RowHeightCompact;
        var characters = Math.Max(5, _clock.Text.Length);

        // Reset everything the arrangements below may not place.
        _ringShown = false;
        Hide(_ring.Root, _phaseRow, _progress, _next);

        var skip = area.WidthClass >= SizeClass.Expanded || area.AtLeast(SizeClass.Regular, SizeClass.Regular);
        var reset = area.WidthClass >= SizeClass.Regular || area.HeightClass >= SizeClass.Regular;
        var buttons = SetButtons(side, reset, skip);

        // [S-6035] Only the Ring style has a ring; the others stack or strip at every size.
        if (_config.DrawsRing && area.HeightClass >= SizeClass.Expanded && area.WidthClass >= SizeClass.Regular && LayRing(area, box, side, buttons, characters))
        {
            return;
        }

        if (shortTile && area.WidthClass >= SizeClass.Regular)
        {
            LayStrip(area, box, padY, side, buttons, characters);
        }
        else
        {
            LayStack(area, box, padY, side, buttons, characters);
        }
    }

    /// <summary>Phase, digits, bar, controls, next — stacked and centred. Also XS (digits over one button).</summary>
    private void LayStack(WidgetArea area, WidgetArea box, double padY, double side, double buttons, int characters)
    {
        var gap = Fluent.Space2;
        var room = new Disclosure(box, gap);

        room.Reserve(side);
        room.Reserve(FluentType.Body.LineHeight);
        var phase = area.WidthClass >= SizeClass.Regular && room.Take(FluentType.BodyStrong.LineHeight);
        var bar = _config.DrawsBar && area.WidthClass >= SizeClass.Regular && room.Take(BarHeight);
        var next = area.HeightClass >= SizeClass.Regular && room.Take(FluentType.Caption.LineHeight, FluentType.EstimateWidth(FluentType.Caption, 18));

        var digits = FluentType.Hero(box.Width, room.Remaining + FluentType.Body.LineHeight, characters);
        _clock.Apply(digits);
        _clock.TextAlignment = TextAlignment.Center;
        _phase.Apply(FluentType.BodyStrong);
        _next.Apply(FluentType.Caption);
        _next.TextAlignment = TextAlignment.Center;

        var height = digits.LineHeight + side + gap
            + (phase ? FluentType.BodyStrong.LineHeight + gap : 0)
            + (bar ? BarHeight + gap : 0)
            + (next ? FluentType.Caption.LineHeight + gap : 0);

        var x = Fluent.Space4;
        var y = padY + Math.Max(0, (box.Height - height) / 2);

        if (phase)
        {
            PlaceCentred(_phaseRow, x, y, box.Width, FluentType.BodyStrong.LineHeight);
            y += FluentType.BodyStrong.LineHeight + gap;
        }

        Place(_clock, x, y, box.Width, digits.LineHeight);
        y += digits.LineHeight + gap;

        if (bar)
        {
            Place(_progress, x, y, box.Width, BarHeight);
            y += BarHeight + gap;
        }

        Place(_controls, x + ((box.Width - buttons) / 2), y, buttons, side);
        y += side + gap;

        if (next)
        {
            Place(_next, x, y, box.Width, FluentType.Caption.LineHeight);
        }
    }

    /// <summary>A short, wider tile: phase and digits left, controls right, the bar along the bottom.</summary>
    private void LayStrip(WidgetArea area, WidgetArea box, double padY, double side, double buttons, int characters)
    {
        var gap = Fluent.Space2;
        var left = Math.Max(0, box.Width - buttons - Fluent.Space3);
        var room = new Disclosure(left, box.Height, gap);

        room.Reserve(FluentType.Body.LineHeight);
        var bar = _config.DrawsBar && room.Take(BarHeight);
        var phase = room.Take(FluentType.BodyStrong.LineHeight);

        var digits = FluentType.Hero(left, room.Remaining + FluentType.Body.LineHeight, characters);
        _clock.Apply(digits);
        _clock.TextAlignment = TextAlignment.Left;
        _phase.Apply(FluentType.BodyStrong);
        _next.Apply(FluentType.Caption);

        var column = digits.LineHeight + (phase ? FluentType.BodyStrong.LineHeight + Fluent.Space1 : 0);
        var top = padY + Math.Max(0, (box.Height - (bar ? BarHeight + gap : 0) - column) / 2);
        var x = Fluent.Space4;

        if (phase)
        {
            Place(_phaseRow, x, top, left, FluentType.BodyStrong.LineHeight);
            top += FluentType.BodyStrong.LineHeight + Fluent.Space1;
        }

        Place(_clock, x, top, left, digits.LineHeight);

        // The controls, and on a strip with the height for it the next phase under them.
        var rightX = x + box.Width - buttons;
        var next = area.WidthClass >= SizeClass.Expanded
            && box.Height - (bar ? BarHeight + gap : 0) >= side + Fluent.Space1 + FluentType.Caption.LineHeight;
        var block = side + (next ? Fluent.Space1 + FluentType.Caption.LineHeight : 0);
        var controlsTop = padY + Math.Max(0, (box.Height - (bar ? BarHeight + gap : 0) - block) / 2);

        Place(_controls, rightX, controlsTop, buttons, side);

        if (next)
        {
            var width = Math.Max(buttons, FluentType.EstimateWidth(FluentType.Caption, 18));
            _next.TextAlignment = TextAlignment.Right;
            Place(_next, x + box.Width - width, controlsTop + side + Fluent.Space1, width, FluentType.Caption.LineHeight);
        }

        if (bar)
        {
            Place(_progress, x, padY + box.Height - BarHeight, box.Width, BarHeight);
        }
    }

    /// <summary>
    /// A tall or large tile: the ring with the digits inside. Beside it on a tile wide enough for a
    /// second column, else stacked. False when the ring would be too small to read as one.
    /// </summary>
    private bool LayRing(WidgetArea area, WidgetArea box, double side, double buttons, int characters)
    {
        var gap = Fluent.Space3;
        var beside = box.Width - box.Height >= SideColumnMinimum;
        var label = area.Least >= SizeClass.Expanded ? FluentType.Strong(FluentType.Subtitle) : FluentType.BodyStrong;
        var nextStep = area.Least >= SizeClass.Expanded ? FluentType.Body : FluentType.Caption;
        double diameter;

        if (beside)
        {
            diameter = Math.Min(box.Height, box.Width - SideColumnMinimum - gap);
        }
        else
        {
            var room = new Disclosure(box, gap);
            room.Reserve(side);
            room.Reserve(RingMinimum);
            room.Take(label.LineHeight);
            room.Take(nextStep.LineHeight);
            diameter = Math.Min(box.Width, room.Remaining + RingMinimum);
        }

        if (diameter < RingMinimum)
        {
            return false;
        }

        _phase.Apply(label);
        _next.Apply(nextStep);
        _clock.Apply(FluentType.Hero(diameter * 0.66, diameter * 0.34, characters));

        _ringShown = true;
        _ring.Resize(diameter);

        var x0 = Fluent.Space4;
        var y0 = Fluent.Space4;

        if (beside)
        {
            var ringX = x0;
            var ringY = y0 + ((box.Height - diameter) / 2);
            PlaceRing(ringX, ringY, diameter);

            // The second column: everything but the digits, centred on the ring.
            var colX = ringX + diameter + gap + Fluent.Space4;
            var colW = Math.Max(0, (x0 + box.Width) - colX);
            var height = label.LineHeight + gap + nextStep.LineHeight + gap + side;
            var y = y0 + Math.Max(0, (box.Height - height) / 2);

            Place(_phaseRow, colX, y, colW, label.LineHeight);
            y += label.LineHeight + gap;
            _next.TextAlignment = TextAlignment.Left;
            Place(_next, colX, y, colW, nextStep.LineHeight);
            y += nextStep.LineHeight + gap;
            Place(_controls, colX, y, buttons, side);
        }
        else
        {
            var room = new Disclosure(box, gap);
            room.Reserve(side);
            room.Reserve(diameter);
            var phase = room.Take(label.LineHeight);
            var next = room.Take(nextStep.LineHeight);

            var height = diameter + gap + side
                + (phase ? label.LineHeight + gap : 0)
                + (next ? nextStep.LineHeight + gap : 0);
            var y = y0 + Math.Max(0, (box.Height - height) / 2);

            if (phase)
            {
                PlaceCentred(_phaseRow, x0, y, box.Width, label.LineHeight);
                y += label.LineHeight + gap;
            }

            PlaceRing(x0 + ((box.Width - diameter) / 2), y, diameter);
            y += diameter + gap;

            if (next)
            {
                _next.TextAlignment = TextAlignment.Center;
                Place(_next, x0, y, box.Width, nextStep.LineHeight);
                y += nextStep.LineHeight + gap;
            }

            Place(_controls, x0 + ((box.Width - buttons) / 2), y, buttons, side);
        }

        _ring.SetFraction(PomodoroCycle.Progress(_state, PomodoroCycle.PhaseSeconds(_state.Phase, _config)));
        return true;
    }

    private void PlaceRing(double x, double y, double diameter)
    {
        Place(_ring.Root, x, y, diameter, diameter);

        var digits = _clock.LineHeight;
        _clock.TextAlignment = TextAlignment.Center;
        Place(_clock, x, y + ((diameter - digits) / 2), diameter, digits);
    }

    /// <summary>Size the buttons and show the ones this rung has; returns the row's width.</summary>
    private double SetButtons(double side, bool reset, bool skip)
    {
        foreach (var button in new[] { _runButton, _resetButton, _skipButton })
        {
            button.Width = side;
            button.Height = side;
        }

        _resetButton.Visibility = reset ? Visibility.Visible : Visibility.Collapsed;
        _skipButton.Visibility = skip ? Visibility.Visible : Visibility.Collapsed;

        var count = 1 + (reset ? 1 : 0) + (skip ? 1 : 0);
        return (count * side) + ((count - 1) * Fluent.Space2);
    }


    private static void Place(FrameworkElement element, double x, double y, double width, double height)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        element.Width = Math.Max(0, width);
        element.Height = Math.Max(0, height);
        element.Visibility = Visibility.Visible;
    }

    /// <summary>Place a horizontal panel centred in a box, at its own measured width.</summary>
    private static void PlaceCentred(StackPanel panel, double x, double y, double width, double height)
    {
        // Visible before measuring: a collapsed panel measures to nothing.
        panel.Width = double.NaN;
        panel.Visibility = Visibility.Visible;
        panel.Measure(new Windows.Foundation.Size(width, height));
        var own = Math.Min(width, panel.DesiredSize.Width);
        Place(panel, x + ((width - own) / 2), y, own, height);
    }

    private static void Hide(params UIElement[] elements)
    {
        foreach (var element in elements)
        {
            element.Visibility = Visibility.Collapsed;
        }
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
        _running = false;

        // A key the lease delivers after the card left ([S-802]) must not start a detached timer.
        _ticker = null;
    }
}
