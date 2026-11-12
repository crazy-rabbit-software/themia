using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.FocusSession;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4420] A focus session, as Windows' Focus: a length, Start, the time left counting down, End. While
/// it runs it holds Windows' do not disturb ([S-4423]); a Windows focus session started elsewhere shows
/// as such ([S-4424]). The session is the app's one (<see cref="IWidgetHost.Focus"/>): every focus
/// widget shows it.
/// <para>
/// [S-4426] On the timers' face ([S-6739]): the time as the Readout, centred, the line that says what do
/// not disturb is doing under it; the Controls row (Start with −/+ while idle, End session while it
/// runs) where it fits, the time alone otherwise (a click on it starts or ends); the Chart ring of the
/// time gone around the time from expanded both ways. The second tick runs only while a session does
/// ([S-4422]).
/// </para>
/// </summary>
internal sealed partial class FocusSessionView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private readonly Grid _dial = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly ChartRing _ring = new();
    private readonly ReadoutBlock _clock = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    // Idle and running have rows of their own: a row folds its own parts, so a part is never hidden by hand.
    private readonly ControlsBlock _idleRow = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly ControlsBlock _runningRow = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _start;
    private readonly Button _shorter;
    private readonly Button _longer;
    private readonly Button _end;

    // [S-11305] At Small the rows are icon buttons: Start the accent's Play, End session Stop.
    private readonly ControlsBlock _idleIcons = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly ControlsBlock _runningIcons = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _shorterIcon;
    private readonly Button _longerIcon;

    private FocusSessionConfig _config = new();
    private readonly IWidgetHost _host;
    private IWidgetSurface? _surface;
    private IDisposable? _lease;
    private Ticker? _ticker;
    private IDisposable? _seconds;
    private TimerSized _face;
    private FocusSnapshot _preview = new(FocusSessionState.Idle, ToggleReading.Pending, false, false);
    private bool _ticking;
    private bool _detached = true;

    public FocusSessionView(IWidgetHost host)
    {
        _host = host;
        Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        RowSpacing = FamilySpace.S2;
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _dial.Children.Add(_ring);
        _dial.Children.Add(_clock);

        // Start first, so the steppers give way before it (+, then -).
        _start = _idleRow.Main(FocusSessionStr.Panels_Focus_Start.Text(), Toggle);
        _shorter = _idleRow.Icon(Glyphs.Minus, FocusSessionStr.Panels_Focus_Shorter.Text(), () => StepLength(-1));
        _longer = _idleRow.Icon(Glyphs.Add, FocusSessionStr.Panels_Focus_Longer.Text(), () => StepLength(+1));
        _end = _runningRow.Button(FocusSessionStr.Panels_Focus_End.Text(), Toggle);
        _idleIcons.MainIcon(Glyphs.Play, FocusSessionStr.Panels_Focus_Start.Text(), Toggle);
        _shorterIcon = _idleIcons.Icon(Glyphs.Minus, FocusSessionStr.Panels_Focus_Shorter.Text(), () => StepLength(-1));
        _longerIcon = _idleIcons.Icon(Glyphs.Add, FocusSessionStr.Panels_Focus_Longer.Text(), () => StepLength(+1));
        _runningIcons.Icon(Glyphs.Stop, FocusSessionStr.Panels_Focus_End.Text(), Toggle);

        Children.Add(_dial);
        foreach (var row in new[] { _idleRow, _runningRow, _idleIcons, _runningIcons })
        {
            SetRow(row, 1);
            Children.Add(row);
        }

    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _ticker = ticker;
        if (_surface is not { IsPreview: true })
        {
            _lease = _host.Focus.Acquire(OnChanged);
        }

        PointerEntered += OnPointerEntered;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as FocusSessionConfig ?? new FocusSessionConfig();
        Lay();
    }

    /// <summary>[S-11305] The face is the config's, not the box's: a wider card centres the same face.</summary>
    public void ApplySize(WidgetArea area)
    {
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        PointerEntered -= OnPointerEntered;
        Strings.Changed -= OnLanguage;
        _seconds?.Dispose();
        _seconds = null;
        _ticking = false;
        _lease?.Dispose();
        _lease = null;
        _ticker = null;
    }

    private FocusSnapshot Snapshot => _lease is null ? _preview : _host.Focus.Snapshot;

    // ---- layout ------------------------------------------------------------------------------

    /// <summary>[S-11305] The face at the widget's Size: the time's step, in its ring where Ring is on.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        _face = FocusSessionLayout.Sized(_config);
        _dial.Width = _face.Ring > 0 ? _face.Ring : double.NaN;
        _dial.Height = _face.Ring > 0 ? _face.Ring : double.NaN;
        _ring.Visibility = _face.Ring > 0 ? Visibility.Visible : Visibility.Collapsed;
        _clock.Use(_face.Step, centred: true);

        // In the ring the status line is cut at the hole's edge, never drawn over the arc.
        _clock.MaxWidth = _face.Ring > 0 ? ChartRules.Hole(_face.Ring).Width : double.PositiveInfinity;
        Paint();
    }

    /// <summary>The time, the ring, the line and the controls, from the snapshot.</summary>
    private void Paint()
    {
        if (_detached)
        {
            return;
        }

        var snapshot = Snapshot;
        var now = DateTimeOffset.Now;
        var running = snapshot.Session.Running;

        var clock = running
            ? FocusSessionRules.Clock(snapshot.Session.Remaining(now))
            : FocusSessionRules.Clock(TimeSpan.FromMinutes(FocusSessionRules.ClampMinutes(_config.Minutes)));
        var status = Status(snapshot);
        _clock.Show(clock, under: status);

        if (_face.Ring > 0)
        {
            _ring.Show(running ? snapshot.Session.Progress(now) : 0);
        }

        var icons = TimerFace.IconControls(_config.Size);
        _idleRow.Visibility = !icons && !running ? Visibility.Visible : Visibility.Collapsed;
        _runningRow.Visibility = !icons && running ? Visibility.Visible : Visibility.Collapsed;
        _idleIcons.Visibility = icons && !running ? Visibility.Visible : Visibility.Collapsed;
        _runningIcons.Visibility = icons && running ? Visibility.Visible : Visibility.Collapsed;
        ControlsBlock.Relabel(_start, FocusSessionStr.Panels_Focus_Start.Text());
        ControlsBlock.Relabel(_end, FocusSessionStr.Panels_Focus_End.Text());
        ControlsBlock.Reglyph(_shorter, Glyphs.Minus, FocusSessionStr.Panels_Focus_Shorter.Text());
        ControlsBlock.Reglyph(_longer, Glyphs.Add, FocusSessionStr.Panels_Focus_Longer.Text());
        ToolTipService.SetToolTip(_shorter, FocusSessionStr.Panels_Focus_Shorter.Text());
        ToolTipService.SetToolTip(_longer, FocusSessionStr.Panels_Focus_Longer.Text());
        _shorter.IsEnabled = _config.Minutes > FocusSessionRules.MinMinutes;
        _shorterIcon.IsEnabled = _shorter.IsEnabled;
        _longer.IsEnabled = _config.Minutes < FocusSessionRules.MaxMinutes;
        _longerIcon.IsEnabled = _longer.IsEnabled;

        var left = FocusSessionStr.Panels_Focus_Left.Format(("time", clock));
        AutomationProperties.SetName(this, running ? left : FocusSessionRules.Length(_config.Minutes));
        AutomationProperties.SetHelpText(this, status);

        // [S-4422] The second tick only while it runs.
        // The flag first: Subscribe calls back at once, into this method.
        if (running && !_ticking && _ticker is not null)
        {
            _ticking = true;
            _seconds = _ticker.Subscribe(TimeSpan.FromSeconds(1), Paint);
        }
        else if (!running && _ticking)
        {
            _ticking = false;
            _seconds?.Dispose();
            _seconds = null;
        }
    }

    /// <summary>[S-4423] [S-4424] What do not disturb is doing, or Windows' own session, or what Start will do.</summary>
    private string Status(FocusSnapshot snapshot)
    {
        if (snapshot.Session.Running)
        {
            if (!snapshot.WantsQuiet)
            {
                return FocusSessionRules.Length(_config.Minutes);
            }

            return snapshot.Quiet switch
            {
                { On: true } => FocusSessionStr.Panels_Focus_QuietOn.Text(),
                { Block: not ToggleBlock.None and not ToggleBlock.Reading } => FocusSessionStr.Panels_Focus_QuietUnavailable.Text(),
                _ => FocusSessionRules.Length(_config.Minutes),
            };
        }

        if (snapshot.WindowsFocus == true)
        {
            return FocusSessionStr.Panels_Focus_Windows.Text();
        }

        return _config.DoNotDisturb ? FocusSessionStr.Panels_Focus_WillQuiet.Text() : FocusSessionRules.Length(_config.Minutes);
    }

    // ---- input -------------------------------------------------------------------------------

    private bool Inert => _detached || _surface?.EditMode == true;

    private void Toggle()
    {
        if (Inert)
        {
            return;
        }

        if (_lease is null)
        {
            // A debug render: the made-up session flips, nothing else does.
            _preview = _preview with { Session = _preview.Session.Running ? FocusSessionState.Idle : FocusSessionRules.Start(DateTimeOffset.Now, _config.Minutes) };
            Lay();
            return;
        }

        if (_host.Focus.Snapshot.Session.Running)
        {
            _host.Focus.End();
        }
        else
        {
            _host.Focus.Start(_config.Minutes, _config.DoNotDisturb);
        }
    }

    /// <summary>[S-4421] Five minutes longer or shorter, saved: the widget's own length.</summary>
    private void StepLength(int direction)
    {
        if (Inert || Snapshot.Session.Running)
        {
            return;
        }

        var next = FocusSessionRules.Step(_config.Minutes, direction);
        if (next == _config.Minutes)
        {
            return;
        }

        _config.Minutes = next;
        _surface?.RequestSave();
        Lay();
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        if (_lease is not null)
        {
            _host.Focus.Refresh();
        }
    }

    /// <summary>[S-4427] Space or Enter starts or ends; Up/+ and Down/− change the length while idle.</summary>
    public bool HandleKey(WidgetKey key)
    {
        switch (key)
        {
            case WidgetKey.Space or WidgetKey.Enter:
                Toggle();
                return !Inert;

            case WidgetKey.Up or WidgetKey.Plus or WidgetKey.Right:
                StepLength(+1);
                return !Inert && !Snapshot.Session.Running;

            case WidgetKey.Down or WidgetKey.Minus or WidgetKey.Left:
                StepLength(-1);
                return !Inert && !Snapshot.Session.Running;

            default:
                return false;
        }
    }

    public bool WantsKeys => !Inert;

    private void OnChanged() => Lay();

    private void OnLanguage(Locale locale) => Lay();
}
