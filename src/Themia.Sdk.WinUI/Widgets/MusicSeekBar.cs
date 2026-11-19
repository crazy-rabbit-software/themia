using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

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
/// <para>
/// [S-12816] The pointer never moves the layout: the thumb is faded in and out (opacity, never visibility, so a hover
/// costs no layout pass), and the bar states its ink (<see cref="IInked"/>), its track's whether or not the thumb
/// shows. A block that scales by its parts' ink (<see cref="ReadingFit"/>) read the shown thumb as ink reaching the
/// bar's edges and drew the times smaller while the pointer stayed on the bar.
/// </para>
/// </summary>
public sealed partial class MusicSeekBar : Grid, IInked
{
    /// <summary>The visible line.</summary>
    private const double TrackHeight = SeekBarFace.Track;

    /// <summary>The thumb shown while hovering or scrubbing a seekable track.</summary>
    private const double ThumbSize = SeekBarFace.Thumb;

    /// <summary>The height the bar takes: the thumb's, so the thumb is never clipped and is easy to hit.</summary>
    public const double BarHeight = SeekBarFace.Height;

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

        // [S-1113] A media timeline: left to right in every language, as Windows' own players keep it.
        FlowDirection = Microsoft.UI.Xaml.FlowDirection.LeftToRight;
        Background = Family.Brush(ColourRole.Clear); // hit-testable across its whole height

        _track.Height = TrackHeight;
        _track.CornerRadius = Family.Round(TrackHeight, TrackHeight);
        _track.VerticalAlignment = VerticalAlignment.Center;

        // [S-6716] A seek bar is a slider: its rail the Track role, its value the accent, as the Controls block's.
        _track.Background = Family.Brush(ColourRole.Track);

        _fill.Height = TrackHeight;
        _fill.CornerRadius = Family.Round(TrackHeight, TrackHeight);
        _fill.VerticalAlignment = VerticalAlignment.Center;
        _fill.Background = Family.Brush(ColourRole.Accent);
        _fill.RenderTransform = _fillScale;
        _fill.RenderTransformOrigin = new Windows.Foundation.Point(0, 0.5);

        _thumb.Width = ThumbSize;
        _thumb.Height = ThumbSize;
        _thumb.HorizontalAlignment = HorizontalAlignment.Left;
        _thumb.VerticalAlignment = VerticalAlignment.Center;
        _thumb.Fill = Family.Brush(ColourRole.Accent);
        _thumb.Stroke = Family.Brush(ColourRole.Solid);
        _thumb.StrokeThickness = FamilyShape.Stroke;
        _thumb.RenderTransform = _thumbShift;
        _thumb.Opacity = 0;

        Children.Add(_track);
        Children.Add(_fill);
        Children.Add(_thumb);

        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        PointerCaptureLost += OnCaptureLost;
        PointerEntered += (_, _) => Point(true);
        PointerExited += (_, _) => Point(false);
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

    /// <summary>[S-12816] Its track's ink, the thumb shown or not: what a block it stands in is scaled and placed by.</summary>
    public LineInk Ink => SeekBarFace.Ink;

    /// <summary>A bar keeps its height in a stack.</summary>
    public bool Grows => false;

    /// <summary>The pointer over the bar, or gone from it (a render's hovered state stands in for it): the thumb shows over a seekable track.</summary>
    public void Point(bool over)
    {
        _hovering = over;
        ShowThumb();
    }

    private void Draw(double fraction)
    {
        var width = ActualWidth;
        _fillScale.ScaleX = fraction;
        _thumbShift.X = Math.Clamp((fraction * width) - (ThumbSize / 2), 0, Math.Max(0, width - ThumbSize));
    }

    private void ShowThumb()
        => _thumb.Opacity = SeekBarFace.ThumbShown(_canSeek, _hovering, _scrubbing) ? 1 : 0;

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
