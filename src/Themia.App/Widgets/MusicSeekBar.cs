using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1465] The now-playing progress bar, which is also the seek bar: click to jump, drag to scrub,
/// let go to commit — the original's [S-15]/[S-16].
/// <para>
/// A thin track and fill rather than a stock <c>Slider</c>: the slider's 32px template and its value
/// tooltip are a settings control, and this has to sit as a 4px line under a title at every size.
/// The fill and the thumb move by render transforms, so the once-a-second advance never costs a
/// layout pass. The pointer is captured by the bar itself while scrubbing, which keeps the drag
/// inside the widget (outside edit mode the card does not drag).
/// </para>
/// </summary>
internal sealed partial class MusicSeekBar : Grid
{
    /// <summary>The visible line.</summary>
    private const double TrackHeight = 4;

    /// <summary>The thumb shown while hovering or scrubbing a seekable track.</summary>
    private const double ThumbSize = 12;

    /// <summary>The height the bar takes: the thumb's, so the thumb is never clipped and is easy to hit.</summary>
    public const double BarHeight = ThumbSize;

    private readonly Border _track = new();
    private readonly Border _fill = new();
    private readonly Ellipse _thumb = new();
    private readonly ScaleTransform _fillScale = new() { ScaleX = 0 };
    private readonly TranslateTransform _thumbShift = new();

    private double _value;
    private bool _scrubbing;
    private bool _hovering;
    private bool _canSeek;

    public MusicSeekBar()
    {
        Height = BarHeight;
        Background = new SolidColorBrush(Colors.Transparent); // hit-testable across its whole height

        _track.Height = TrackHeight;
        _track.CornerRadius = new CornerRadius(TrackHeight / 2);
        _track.VerticalAlignment = VerticalAlignment.Center;
        _track.Background = FluentLayout.Themed("ControlStrongFillColorDisabledBrush", Colors.Gray);

        _fill.Height = TrackHeight;
        _fill.CornerRadius = new CornerRadius(TrackHeight / 2);
        _fill.VerticalAlignment = VerticalAlignment.Center;
        _fill.Background = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        _fill.RenderTransform = _fillScale;
        _fill.RenderTransformOrigin = new Windows.Foundation.Point(0, 0.5);

        _thumb.Width = ThumbSize;
        _thumb.Height = ThumbSize;
        _thumb.HorizontalAlignment = HorizontalAlignment.Left;
        _thumb.VerticalAlignment = VerticalAlignment.Center;
        _thumb.Fill = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        _thumb.Stroke = FluentLayout.Themed("ControlSolidFillColorDefaultBrush", Colors.White);
        _thumb.StrokeThickness = 2;
        _thumb.RenderTransform = _thumbShift;
        _thumb.Visibility = Visibility.Collapsed;

        Children.Add(_track);
        Children.Add(_fill);
        Children.Add(_thumb);

        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        PointerCaptureLost += OnCaptureLost;
        PointerEntered += (_, _) => { _hovering = true; ShowThumb(); };
        PointerExited += (_, _) => { _hovering = false; ShowThumb(); };
        SizeChanged += (_, _) => Draw(_value);
    }

    /// <summary>A fraction (0..1) while scrubbing: the elapsed label follows the pointer.</summary>
    public event Action<double>? Scrubbing;

    /// <summary>The fraction let go at: seek there.</summary>
    public event Action<double>? Committed;

    /// <summary>Whether the session can seek. A bar that cannot is a plain progress line: no thumb, no input.</summary>
    public bool CanSeek
    {
        get => _canSeek;
        set
        {
            _canSeek = value;
            IsHitTestVisible = value;

            if (!value)
            {
                _scrubbing = false;
            }

            ShowThumb();
        }
    }

    /// <summary>How far along (0..1). Ignored while the user is scrubbing: the pointer is the truth then.</summary>
    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(double.IsFinite(value) ? value : 0, 0, 1);

            if (!_scrubbing)
            {
                Draw(_value);
            }
        }
    }

    public bool IsScrubbing => _scrubbing;

    private void Draw(double fraction)
    {
        var width = ActualWidth;
        _fillScale.ScaleX = fraction;
        _thumbShift.X = Math.Clamp((fraction * width) - (ThumbSize / 2), 0, Math.Max(0, width - ThumbSize));
    }

    private void ShowThumb()
        => _thumb.Visibility = _canSeek && (_hovering || _scrubbing) ? Visibility.Visible : Visibility.Collapsed;

    private double FractionAt(PointerRoutedEventArgs e) => MusicRules.ScrubFraction(e.GetCurrentPoint(this).Position.X, ActualWidth);

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_canSeek || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _scrubbing = CapturePointer(e.Pointer);
        var fraction = FractionAt(e);
        Draw(fraction);
        ShowThumb();
        Scrubbing?.Invoke(fraction);
        e.Handled = true;

        // A click that could not capture is still a click: seek straight away.
        if (!_scrubbing)
        {
            Committed?.Invoke(fraction);
        }
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_scrubbing)
        {
            return;
        }

        var fraction = FractionAt(e);
        Draw(fraction);
        Scrubbing?.Invoke(fraction);
        e.Handled = true;
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_scrubbing)
        {
            return;
        }

        var fraction = FractionAt(e);
        _scrubbing = false;
        ReleasePointerCapture(e.Pointer);
        Draw(fraction);
        ShowThumb();
        Committed?.Invoke(fraction);
        e.Handled = true;
    }

    /// <summary>Capture taken away (the surface deactivated mid-drag): no seek; the bar goes back to the track.</summary>
    private void OnCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (!_scrubbing)
        {
            return;
        }

        _scrubbing = false;
        Draw(_value);
        ShowThumb();
        Scrubbing?.Invoke(-1);
    }
}
