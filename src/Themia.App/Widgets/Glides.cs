using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Themia.App.Shell.Motion;
using Themia.Core.Diagnostics;
using Themia.Core.Motion;
using Windows.UI;

namespace Themia.App.Widgets;

// [S-6323]..[S-6328] The one way a live readout moves from one reading to the next (rules in Core
// Glide and OkLab). Three kinds of glide, one rule:
//   NumberGlide  a value that can only be redrawn on the UI thread (a number's text, a gauge's or a
//                ring's arc, a battery's fill): stepped by the shared GlideClock, touched only when
//                the next step is due and never once it has arrived.
//   DoubleGlide  a property the compositor animates (an opacity, a scale transform's factor): an
//                independent Storyboard animation, nothing per frame on the UI thread.
//   ColorGlide   a brush of its own whose colour the compositor animates along the OKLab path.
// Each takes the new value with the period it should glide over (the series' period, [S-1826]);
// with Windows' animation effects off, or from or to "no value", it jumps as before.

/// <summary>[S-6326] Whether readouts glide now: Windows' animation effects, read at most once a second.</summary>
internal static class GlideMotion
{
    private static long _readAt = long.MinValue;
    private static bool _enabled = true;

    /// <summary>The period every glide on the hub's cadence takes ([S-2503]).</summary>
    public static TimeSpan Tick { get; } = TimeSpan.FromSeconds(Core.SystemData.SystemCadence.TickSeconds);

    /// <summary>
    /// [S-6326] Windows' Animation effects ([S-1010]). Read per glide but asked of Windows at most once
    /// a second: a core grid starts dozens of glides a second, and the switch is a person's.
    /// </summary>
    public static bool Animations
    {
        get
        {
            var now = Stopwatch.GetTimestamp();

            if (now - _readAt >= Stopwatch.Frequency)
            {
                _readAt = now;
                _enabled = SystemMotion.AnimationsEnabled;
            }

            return _enabled;
        }
    }

#if DEBUG
    /// <summary>
    /// Debug renders only (<c>--tween 0..1</c>): every glide drawn that far from the value it stood at
    /// to the new one, still, so an image shows a readout between two readings.
    /// </summary>
    public static double? PreviewProgress { get; set; }
#endif

    /// <summary>What a readout does with a new value now: the rule, the switch, and a debug still.</summary>
    public static GlideMove Plan(double? from, double? to, bool settle)
    {
        var move = Glide.Plan(!settle && Animations, from, to);
#if DEBUG
        if (PreviewProgress is not null && move == GlideMove.Jump && !settle && from is not null && to is not null)
        {
            // A still between two readings needs the glide, even with animation effects off.
            move = Glide.Plan(true, from, to);
        }
#endif
        return move;
    }

    /// <summary>The progress a debug still stands at, or null to animate.</summary>
    public static double? Still
    {
        get
        {
#if DEBUG
            return PreviewProgress;
#else
            return null;
#endif
        }
    }
}

/// <summary>A glide the <see cref="GlideClock"/> steps.</summary>
internal interface IGlideStep
{
    /// <summary>Show the step due at <paramref name="now"/> (a <see cref="Stopwatch"/> timestamp); the next due timestamp, or null when arrived.</summary>
    long? Advance(long now);
}

/// <summary>
/// [S-6325] The one scheduler of the UI-thread glides: a one-shot timer armed for the earliest step
/// any glide has due, only while some glide is moving. No frame callback and no repeating timer:
/// a number gliding 42 % to 45 % wakes it three times.
/// </summary>
internal static class GlideClock
{
    private const string Scope = "glide";

    /// <summary>Two wakes are never closer than this: a step that is late by a few milliseconds is not seen.</summary>
    private static readonly long MinGap = Stopwatch.Frequency / 60;

    [ThreadStatic]
    private static Dictionary<IGlideStep, long>? _due;

    [ThreadStatic]
    private static DispatcherQueueTimer? _timer;

    [ThreadStatic]
    private static long _armedFor;

    /// <summary>Step <paramref name="glide"/> at <paramref name="due"/> (a <see cref="Stopwatch"/> timestamp).</summary>
    public static void Schedule(IGlideStep glide, long due)
    {
        _due ??= [];
        _due[glide] = due;
        Arm();
    }

    /// <summary>Stop stepping <paramref name="glide"/> (it jumped, or its view is gone).</summary>
    public static void Cancel(IGlideStep glide)
    {
        _due?.Remove(glide);
    }

    private static void Arm()
    {
        if (_due is not { Count: > 0 } due)
        {
            _timer?.Stop();
            return;
        }

        var next = long.MaxValue;
        foreach (var at in due.Values)
        {
            next = Math.Min(next, at);
        }

        var now = Stopwatch.GetTimestamp();
        next = Math.Max(next, now + MinGap);

        if (_timer is { IsRunning: true } && _armedFor <= next)
        {
            return;
        }

        if (_timer is null)
        {
            if (DispatcherQueue.GetForCurrentThread() is not { } queue)
            {
                Log.Warn(Scope, "no dispatcher on this thread: readouts jump");
                due.Clear();
                return;
            }

            _timer = queue.CreateTimer();
            _timer.IsRepeating = false;
            _timer.Tick += OnTick;
        }

        _timer.Stop();
        _armedFor = next;
        _timer.Interval = TimeSpan.FromSeconds((double)(next - now) / Stopwatch.Frequency);
        _timer.Start();
    }

    private static void OnTick(DispatcherQueueTimer sender, object args)
    {
        try
        {
            if (_due is not { } due)
            {
                return;
            }

            var now = Stopwatch.GetTimestamp();

            foreach (var (glide, at) in due.ToArray())
            {
                if (at > now + MinGap)
                {
                    continue;
                }

                if (glide.Advance(now) is { } next)
                {
                    due[glide] = next;
                }
                else
                {
                    due.Remove(glide);
                }
            }
        }
        catch (Exception ex)
        {
            // A readout that failed to step is cosmetic; the next reading draws it again.
            Log.Error(Scope, "a readout's glide failed", ex);
        }
        finally
        {
            _armedFor = long.MaxValue;
            Arm();
        }
    }
}

/// <summary>
/// [S-6325] A value redrawn on the UI thread as it glides (a number's text, an arc, a fill's width):
/// <see cref="Set"/> takes each reading, and the value shown goes to <c>apply</c> at the start, at
/// every step and on arrival, in the value's own resolution.
/// </summary>
internal sealed class NumberGlide : IGlideStep
{
    private readonly Action<double> _apply;
    private readonly double _resolution;
    private double _from;
    private double _to;
    private double _quantum;
    private long _start;
    private TimeSpan _duration;
    private bool _has;
    private bool _moving;

    /// <param name="apply">Show a value. Called with the value to show only; dedupe text in it.</param>
    /// <param name="resolution">The value's own step (0.01 for a whole percent of a fraction; 0 for none).</param>
    public NumberGlide(Action<double> apply, double resolution)
    {
        _apply = apply;
        _resolution = resolution;
    }

    /// <summary>The value it is gliding to (or stands at); null when it has none.</summary>
    public double? Target => _has ? _to : null;

    /// <summary>
    /// A new value: glide to it over <paramref name="period"/>, or show it at once when
    /// <paramref name="settle"/> or the rule says jump. Null forgets the value (the caller shows "--").
    /// </summary>
    public void Set(double? value, TimeSpan period, bool settle = false)
    {
        var now = Stopwatch.GetTimestamp();

        if (value is not { } to || !double.IsFinite(to))
        {
            Stop();
            _has = false;
            return;
        }

        // A reading the glide is already heading for keeps the glide it has.
        if (_has && _moving && to == _to && !settle)
        {
            return;
        }

        var from = _has ? Current(now) : (double?)null;

        // Still: it stands at the value already (perhaps mid-way to another); standing there is a jump nobody sees.
        switch (GlideMotion.Plan(from, to, settle))
        {
            case GlideMove.Still:
            case GlideMove.Jump:
                Stop();
                _has = true;
                _from = to;
                _to = to;
                _apply(to);
                return;
        }

        _from = from!.Value;
        _to = to;
        _has = true;
        _start = now;
        _duration = period;
        _quantum = Glide.Quantum(_from, _to, _resolution, period);

        if (GlideMotion.Still is { } still)
        {
            Stop();
            _apply(Glide.Shown(_from, _to, still, _quantum));
            return;
        }

        _apply(Glide.Shown(_from, _to, 0, _quantum));

        if (Glide.NextChange(_from, _to, 0, _quantum) is var next && double.IsFinite(next))
        {
            _moving = true;
            GlideClock.Schedule(this, Due(next));
        }
        else
        {
            Stop();
        }
    }

    /// <summary>Stand where it is: no more steps (a view detached, a look changed).</summary>
    public void Stop()
    {
        if (_moving)
        {
            _moving = false;
            GlideClock.Cancel(this);
        }
    }

    public long? Advance(long now)
    {
        if (!_moving)
        {
            return null;
        }

        var progress = Progress(now);
        _apply(Glide.Shown(_from, _to, progress, _quantum));
        var next = Glide.NextChange(_from, _to, progress, _quantum);

        // Arrived, or showing the target already: the readout is settled and nothing wakes for it.
        if (progress >= 1 || !double.IsFinite(next))
        {
            _moving = false;
            return null;
        }

        return Due(next);
    }

    private double Current(long now) => _moving ? Glide.At(_from, _to, Progress(now)) : _to;

    private double Progress(long now)
        => Glide.Progress(TimeSpan.FromSeconds((double)(now - _start) / Stopwatch.Frequency), _duration);

    private long Due(double progress)
        => _start + (long)(Math.Clamp(double.IsFinite(progress) ? progress : 1, 0, 1) * _duration.TotalSeconds * Stopwatch.Frequency);
}

/// <summary>
/// [S-6324] A double property the compositor animates (an element's opacity, a scale transform's
/// factor): the property is set to the new value and an independent animation runs from where it
/// stood, linearly over the period, so nothing runs per frame on the UI thread.
/// </summary>
internal sealed class DoubleGlide
{
    private readonly DependencyObject _target;
    private readonly DependencyProperty _property;
    private readonly Storyboard _board = new();
    private readonly DoubleAnimation _animation = new() { EnableDependentAnimation = false, FillBehavior = FillBehavior.Stop };
    private double _from;
    private double _to;
    private long _start;
    private TimeSpan _duration;
    private bool _has;

    /// <param name="target">What is animated.</param>
    /// <param name="property">Its property (Opacity, ScaleX, ScaleY).</param>
    /// <param name="path">The property's name, as a Storyboard path.</param>
    public DoubleGlide(DependencyObject target, DependencyProperty property, string path)
    {
        _target = target;
        _property = property;
        Storyboard.SetTarget(_animation, target);
        Storyboard.SetTargetProperty(_animation, path);
        _board.Children.Add(_animation);
    }

    /// <summary>The value set last.</summary>
    public double Value => _has ? _to : (double)_target.GetValue(_property);

    public void Set(double value, TimeSpan period, bool settle = false)
    {
        var now = Stopwatch.GetTimestamp();
        var from = _has ? Current(now) : (double?)null;

        if (_has && value == _to && !settle && Glide.Progress(Elapsed(now), _duration) < 1)
        {
            return;
        }

        // Still: it stands at the value already (perhaps mid-way to another); standing there is a jump nobody sees.
        var move = GlideMotion.Plan(from, value, settle);

        _board.Stop();
        _has = true;
        _to = value;

        if (move != GlideMove.Glide)
        {
            _from = value;
            _duration = TimeSpan.Zero;
            _target.SetValue(_property, value);
            return;
        }

        _from = from!.Value;
        _start = now;
        _duration = period;

        if (GlideMotion.Still is { } still)
        {
            _duration = TimeSpan.Zero;
            _target.SetValue(_property, Glide.At(_from, _to, still));
            return;
        }

        _target.SetValue(_property, value);
        _animation.From = _from;
        _animation.To = value;
        _animation.Duration = new Duration(period);
        _board.Begin();
    }

    private TimeSpan Elapsed(long now) => TimeSpan.FromSeconds((double)(now - _start) / Stopwatch.Frequency);

    private double Current(long now) => Glide.At(_from, _to, Glide.Progress(Elapsed(now), _duration));
}

/// <summary>
/// [S-6327] A brush of its own whose colour glides: the colour set at once, and an independent
/// key-framed animation through the OKLab mix of where it stood and the new colour (four straight
/// segments, <see cref="OkLab.Ramp"/>), over the period.
/// </summary>
internal sealed class ColorGlide
{
    private const int Segments = 4;

    private readonly Storyboard _board = new();
    private readonly ColorAnimationUsingKeyFrames _animation = new() { EnableDependentAnimation = false, FillBehavior = FillBehavior.Stop };
    private readonly DiscreteColorKeyFrame _first = new() { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero) };
    private readonly LinearColorKeyFrame[] _frames = new LinearColorKeyFrame[Segments];
    private uint _from;
    private uint _to;
    private long _start;
    private TimeSpan _duration;
    private bool _has;

    public ColorGlide()
    {
        _animation.KeyFrames.Add(_first);

        for (var i = 0; i < Segments; i++)
        {
            _frames[i] = new LinearColorKeyFrame();
            _animation.KeyFrames.Add(_frames[i]);
        }

        Storyboard.SetTarget(_animation, Brush);
        Storyboard.SetTargetProperty(_animation, nameof(SolidColorBrush.Color));
        _board.Children.Add(_animation);
    }

    /// <summary>The brush to paint with.</summary>
    public SolidColorBrush Brush { get; } = new();

    /// <summary>Glide to <paramref name="brush"/>'s colour (a theme brush, read now).</summary>
    public void Set(Brush brush, TimeSpan period, bool settle = false)
    {
        if (brush is SolidColorBrush solid)
        {
            Set(solid.Color, period, settle);
        }
    }

    public void Set(Color color, TimeSpan period, bool settle = false)
    {
        var now = Stopwatch.GetTimestamp();
        var to = Pack(color);
        var progress = Glide.Progress(TimeSpan.FromSeconds((double)(now - _start) / Stopwatch.Frequency), _duration);

        // Heading for it already, or there.
        if (_has && to == _to && !settle)
        {
            return;
        }

        var from = _has ? OkLab.Mix(_from, _to, progress) : (uint?)null;
        // Still: it stands at the colour already (perhaps mid-way to another); standing there is a jump nobody sees.
        var move = GlideMotion.Plan(from, to, settle);

        _board.Stop();
        _has = true;
        _to = to;

        if (move != GlideMove.Glide)
        {
            _from = to;
            _duration = TimeSpan.Zero;
            Brush.Color = color;
            return;
        }

        _from = from!.Value;
        _start = now;
        _duration = period;

        if (GlideMotion.Still is { } still)
        {
            _duration = TimeSpan.Zero;
            Brush.Color = Unpack(OkLab.Mix(_from, _to, still));
            return;
        }

        Brush.Color = color;
        _first.Value = Unpack(_from);
        var ramp = OkLab.Ramp(_from, _to, Segments);

        for (var i = 0; i < Segments; i++)
        {
            _frames[i].Value = Unpack(ramp[i]);
            _frames[i].KeyTime = KeyTime.FromTimeSpan(period * ((i + 1) / (double)Segments));
        }

        _animation.Duration = new Duration(period);
        _board.Begin();
    }

    private static uint Pack(Color c) => ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;

    private static Color Unpack(uint argb) => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
}
