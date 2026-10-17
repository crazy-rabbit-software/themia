using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Services.Focus;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4420] A focus session, as Windows' Focus: a length, Start, the time left counting down, End. While
/// it runs it holds Windows' do not disturb ([S-4423]); a Windows focus session started elsewhere shows
/// as such ([S-4424]). The session is the app's one (<see cref="FocusSessionService"/>): every focus
/// widget shows it.
/// <para>
/// [S-4426] The ladder: the time alone on the smallest tile (a click on it starts or ends); the button
/// under it from a regular size, with its word where it is wide; the −/+ beside it while idle; the line
/// that says what do not disturb is doing; a ring of the time gone from an expanded tile. The second tick
/// runs only while a session does ([S-4422]).
/// </para>
/// </summary>
internal sealed partial class FocusSessionView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private const double ButtonHeight = 32;

    private readonly StackPanel _stack = new() { Spacing = Fluent.Space2, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Grid _dial = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Ellipse _track = new();
    private readonly Microsoft.UI.Xaml.Shapes.Path _arc = new();
    private readonly TextBlock _clock = Fluent.Text(FluentType.Display);
    private readonly TextBlock _status = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly StackPanel _controls = new() { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _shorter = new();
    private readonly Button _longer = new();
    private readonly Button _run = new();
    private readonly FontIcon _runIcon = Fluent.Icon(Glyphs.Play, "TextOnAccentFillColorPrimaryBrush");
    private readonly TextBlock _runText = Fluent.Text(FluentType.Body, "TextOnAccentFillColorPrimaryBrush");

    private FocusSessionConfig _config = new();
    private IWidgetSurface? _surface;
    private FocusLease? _lease;
    private Ticker? _ticker;
    private IDisposable? _seconds;
    private WidgetArea _area;
    private FocusPlan _plan;
    private double _ringDiameter;
    private FocusSnapshot _preview = new(FocusSessionState.Idle, ToggleReading.Pending, false, false);
    private bool _ticking;
    private bool _detached = true;

    public FocusSessionView()
    {
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_clock, FontNumeralAlignment.Tabular);
        _clock.TextAlignment = TextAlignment.Center;
        _clock.HorizontalAlignment = HorizontalAlignment.Center;
        _clock.VerticalAlignment = VerticalAlignment.Center;
        _status.TextAlignment = TextAlignment.Center;
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.TextTrimming = TextTrimming.CharacterEllipsis;
        _status.MaxLines = 1;

        // [S-837] The ring stands for a progress ring: the strong rest fill, the accent for what has gone.
        ItemFill.PaintStroke(_track, ItemRole.Track);
        _arc.Stroke = ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        _arc.StrokeStartLineCap = PenLineCap.Round;
        _arc.StrokeEndLineCap = PenLineCap.Round;
        _dial.Children.Add(_track);
        _dial.Children.Add(_arc);
        _dial.Children.Add(_clock);

        Stepper(_shorter, Glyphs.Minus);
        Stepper(_longer, Glyphs.Add);

        var face = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2 };
        face.Children.Add(_runIcon);
        face.Children.Add(_runText);
        _run.Content = face;
        _run.Height = ButtonHeight;
        _run.MinWidth = ButtonHeight;
        _run.Padding = new Thickness(Fluent.Space3, 0, Fluent.Space3, 0);
        _run.CornerRadius = new CornerRadius(Fluent.ControlRadius);
        _run.IsTabStop = false;
        if (Application.Current?.Resources.TryGetValue("AccentButtonStyle", out var accent) == true && accent is Style style)
        {
            _run.Style = style;
        }

        _controls.Children.Add(_shorter);
        _controls.Children.Add(_run);
        _controls.Children.Add(_longer);

        _stack.Children.Add(_dial);
        _stack.Children.Add(_status);
        _stack.Children.Add(_controls);
        Children.Add(_stack);

        // The whole tile is hit-testable: on the smallest tile a click on it starts or ends.
        Background = ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);
    }

    private static void Stepper(Button button, string glyph)
    {
        button.Content = Fluent.Icon(glyph, "TextFillColorPrimaryBrush");
        button.Width = ButtonHeight;
        button.Height = ButtonHeight;
        button.Padding = new Thickness(0);
        button.CornerRadius = new CornerRadius(Fluent.ControlRadius);
        button.IsTabStop = false;
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _ticker = ticker;
        if (_surface is not { IsPreview: true })
        {
            _lease = FocusSessionService.Acquire(OnChanged);
        }

        _run.Click += OnRun;
        _shorter.Click += OnShorter;
        _longer.Click += OnLonger;
        Tapped += OnTapped;
        PointerEntered += OnPointerEntered;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as FocusSessionConfig ?? new FocusSessionConfig();
        Lay();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _run.Click -= OnRun;
        _shorter.Click -= OnShorter;
        _longer.Click -= OnLonger;
        Tapped -= OnTapped;
        PointerEntered -= OnPointerEntered;
        Strings.Changed -= OnLanguage;
        _seconds?.Dispose();
        _seconds = null;
        _ticking = false;
        _lease?.Dispose();
        _lease = null;
        _ticker = null;
    }

    private FocusSnapshot Snapshot => _lease is null ? _preview : FocusSessionService.Snapshot;

    // ---- layout ------------------------------------------------------------------------------

    /// <summary>[S-4426] The ladder, spent from the deflated box through one <see cref="Disclosure"/>.</summary>
    private void Lay()
    {
        if (_detached || !_area.IsMeasured)
        {
            return;
        }

        _plan = FocusSessionRules.Plan(_area);
        var compact = _area.Least == SizeClass.Compact;
        var inset = compact ? Fluent.Space2 : Fluent.Space4;
        var box = _area.Deflate(inset * 2, inset * 2);
        var room = new Disclosure(box, Fluent.Space2);

        var snapshot = Snapshot;
        var running = snapshot.Session.Running;
        var characters = FocusSessionRules.WidestClock(running ? (int)Math.Ceiling(snapshot.Session.Remaining(DateTimeOffset.Now).TotalMinutes) : _config.Minutes).Length;

        // The button is the must-show part where the plan has it; the status line is the one optional rung.
        var showButton = _plan.Button && room.Reserve(ButtonHeight);
        var showStatus = _plan.Status && room.Take(FluentType.Caption.LineHeight);
        var clockHeight = Math.Max(FluentType.Caption.LineHeight, room.Remaining);

        if (_plan.Ring && Math.Min(box.Width, clockHeight) >= 120)
        {
            _ringDiameter = Math.Min(box.Width, clockHeight);
            var stroke = Math.Clamp(_ringDiameter * 0.05, 4, 12);
            _track.Width = _track.Height = _ringDiameter;
            _track.StrokeThickness = stroke;
            _arc.StrokeThickness = stroke;
            _arc.Width = _arc.Height = _ringDiameter;
            _dial.Width = _dial.Height = _ringDiameter;
            _track.Visibility = Visibility.Visible;
            _arc.Visibility = Visibility.Visible;
            _clock.Apply(FluentType.Hero(_ringDiameter * 0.66, _ringDiameter * 0.34, characters));
        }
        else
        {
            _ringDiameter = 0;
            _dial.Width = double.NaN;
            _dial.Height = double.NaN;
            _track.Visibility = Visibility.Collapsed;
            _arc.Visibility = Visibility.Collapsed;
            _clock.Apply(FluentType.Hero(box.Width, clockHeight, characters));
        }

        _status.Visibility = showStatus ? Visibility.Visible : Visibility.Collapsed;
        _status.MaxWidth = Math.Max(0, box.Width);
        _controls.Visibility = showButton ? Visibility.Visible : Visibility.Collapsed;
        _runText.Visibility = _plan.Words ? Visibility.Visible : Visibility.Collapsed;
        var steppers = showButton && _plan.Steppers && !running;
        _shorter.Visibility = steppers ? Visibility.Visible : Visibility.Collapsed;
        _longer.Visibility = steppers ? Visibility.Visible : Visibility.Collapsed;

        Paint();
    }

    /// <summary>The time, the ring, the words and the button, from the snapshot.</summary>
    private void Paint()
    {
        if (_detached)
        {
            return;
        }

        var snapshot = Snapshot;
        var now = DateTimeOffset.Now;
        var running = snapshot.Session.Running;

        _clock.Text = running
            ? FocusSessionRules.Clock(snapshot.Session.Remaining(now))
            : FocusSessionRules.Clock(TimeSpan.FromMinutes(FocusSessionRules.ClampMinutes(_config.Minutes)));

        if (_ringDiameter > 0)
        {
            _arc.Data = Arc(_ringDiameter, _arc.StrokeThickness, running ? snapshot.Session.Progress(now) : 0);
        }

        _status.Text = Status(snapshot);
        _runIcon.Glyph = running ? Glyphs.Stop : Glyphs.Play;
        _runText.Text = (running ? Str.Panels_Focus_End : Str.Panels_Focus_Start).Text();
        ToolTipService.SetToolTip(_run, _runText.Text);
        ToolTipService.SetToolTip(_shorter, Str.Panels_Focus_Shorter.Text());
        ToolTipService.SetToolTip(_longer, Str.Panels_Focus_Longer.Text());
        _shorter.IsEnabled = _config.Minutes > FocusSessionRules.MinMinutes;
        _longer.IsEnabled = _config.Minutes < FocusSessionRules.MaxMinutes;

        var left = Str.Panels_Focus_Left.Format(("time", _clock.Text));
        AutomationProperties.SetName(this, running ? left : FocusSessionRules.Length(_config.Minutes));
        AutomationProperties.SetHelpText(this, _status.Text);

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
                { On: true } => Str.Panels_Focus_QuietOn.Text(),
                { Block: not ToggleBlock.None and not ToggleBlock.Reading } => Str.Panels_Focus_QuietUnavailable.Text(),
                _ => FocusSessionRules.Length(_config.Minutes),
            };
        }

        if (snapshot.WindowsFocus == true)
        {
            return Str.Panels_Focus_Windows.Text();
        }

        return _config.DoNotDisturb ? Str.Panels_Focus_WillQuiet.Text() : FocusSessionRules.Length(_config.Minutes);
    }

    /// <summary>The filled part of the ring, clockwise from twelve o'clock (Pomodoro's arc).</summary>
    private static Geometry Arc(double diameter, double stroke, double fraction)
    {
        var r = Math.Max(1, (diameter - stroke) / 2);
        var c = diameter / 2;
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction <= 0.0005)
        {
            return new PathGeometry();
        }

        var figure = new PathFigure { StartPoint = new Windows.Foundation.Point(c, c - r), IsClosed = false };
        if (fraction >= 0.9995)
        {
            figure.Segments.Add(new ArcSegment { Point = new Windows.Foundation.Point(c, c + r), Size = new Windows.Foundation.Size(r, r), SweepDirection = SweepDirection.Clockwise });
            figure.Segments.Add(new ArcSegment { Point = new Windows.Foundation.Point(c, c - r), Size = new Windows.Foundation.Size(r, r), SweepDirection = SweepDirection.Clockwise });
        }
        else
        {
            var angle = fraction * 2 * Math.PI;
            figure.Segments.Add(new ArcSegment
            {
                Point = new Windows.Foundation.Point(c + (r * Math.Sin(angle)), c - (r * Math.Cos(angle))),
                Size = new Windows.Foundation.Size(r, r),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = fraction > 0.5,
            });
        }

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
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

        if (FocusSessionService.Snapshot.Session.Running)
        {
            FocusSessionService.End();
        }
        else
        {
            FocusSessionService.Start(_config.Minutes, _config.DoNotDisturb);
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

    private void OnRun(object sender, RoutedEventArgs args) => Toggle();

    private void OnShorter(object sender, RoutedEventArgs args) => StepLength(-1);

    private void OnLonger(object sender, RoutedEventArgs args) => StepLength(+1);

    /// <summary>On the smallest tile the time is the button.</summary>
    private void OnTapped(object sender, TappedRoutedEventArgs args)
    {
        if (!_plan.Button && !Inert)
        {
            args.Handled = true;
            Toggle();
        }
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        if (_lease is not null)
        {
            FocusSessionService.Refresh();
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
