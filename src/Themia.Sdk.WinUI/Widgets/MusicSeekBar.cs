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
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1465] [S-15104] The now-playing progress bar, which is also the seek bar: click to jump, drag to scrub, let go to
/// commit. It is a bar atom (owner, 2026-11-21: "a scaling progress bar that also fills up to the universal spacing
/// given"): its rail fills its card's box less the universal spacing (<see cref="AtomSpace.Pad"/>) on every side, as
/// long as that room and as thick, its ends round; its value fills the rail from the start. The whole card is its
/// press area, edge to edge.
/// <para>
/// The fill moves by its clip and the thumb by a render transform, so the once-a-second advance never costs a layout
/// pass. The pointer is captured by the bar itself while scrubbing, which keeps the drag inside the widget.
/// </para>
/// <para>
/// [S-13060] A drag from the thumb scrubs; a press on the track elsewhere waits (<see cref="TrackPress"/>), as a slider's
/// track does: let go without moving, it seeks to where it was pressed; moved past the threshold, the card takes it for its
/// move (and nothing was sought); still held after that (nothing took it), the bar scrubs from there.
/// </para>
/// <para>
/// [S-12816] The pointer never moves the layout: the thumb (a thin bar's alone, <see cref="SeekBarFace.ThumbShown"/>) is
/// faded in and out (opacity, never visibility), drawn over the spacing, and nothing else changes under the pointer.
/// </para>
/// </summary>
public sealed partial class MusicSeekBar : Grid
{
    /// <summary>The thumb shown while hovering or scrubbing a thin seekable track.</summary>
    private const double ThumbSize = SeekBarFace.Thumb;

    private readonly Grid _rail = new() { Margin = new Thickness(AtomSpace.Pad) };
    private readonly Border _track = new();
    private readonly Grid _filled = new();
    private readonly Border _fill = new();
    private readonly RectangleGeometry _clip = new();
    private readonly Ellipse _thumb = new();
    private readonly TranslateTransform _thumbShift = new();

    private double _value;
    private bool _scrubbing;
    private bool _hovering;
    private bool _canSeek;

    /// <summary>[S-13060] A press on the track that waits for its release or its drag.</summary>
    private readonly TrackPress _press = new();

    public MusicSeekBar()
    {
        // [S-1113] A media timeline: left to right in every language, as Windows' own players keep it.
        FlowDirection = Microsoft.UI.Xaml.FlowDirection.LeftToRight;
        Background = Family.Brush(ColourRole.Clear); // hit-testable across its whole card

        // [S-6716] A seek bar is a slider: its rail the Track role, its value the accent, as the Controls block's.
        _track.Background = Family.Brush(ColourRole.Track);
        _fill.Background = Family.Brush(ColourRole.Accent);
        _filled.Clip = _clip;
        _filled.Children.Add(_fill);
        _rail.Children.Add(_track);
        _rail.Children.Add(_filled);

        _thumb.Width = ThumbSize;
        _thumb.Height = ThumbSize;
        _thumb.HorizontalAlignment = HorizontalAlignment.Left;
        _thumb.VerticalAlignment = VerticalAlignment.Center;
        _thumb.Fill = Family.Brush(ColourRole.Accent);
        _thumb.Stroke = Family.Brush(ColourRole.Solid);
        _thumb.StrokeThickness = FamilyShape.Stroke;
        _thumb.RenderTransform = _thumbShift;
        _thumb.Opacity = 0;

        Children.Add(_rail);
        Children.Add(_thumb);

        // [S-13060] The card's move arms on a press on the track; a drag from the thumb is the bar's.
        PressParts.Mark(this, PressPart.Track);
        PressParts.Mark(_thumb, PressPart.Thumb);

        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        PointerCaptureLost += OnCaptureLost;
        PointerEntered += (_, _) => Point(true);
        PointerExited += (_, _) => Point(false);
        SizeChanged += (_, _) => Shape();
    }

    /// <summary>A fraction (0..1) while scrubbing: an elapsed label may follow the pointer.</summary>
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

    /// <summary>The pointer over the bar, or gone from it (a render's hovered state stands in for it): the thumb shows over a thin seekable track.</summary>
    public void Point(bool over)
    {
        _hovering = over;
        ShowThumb();
    }

    /// <summary>[S-15104] The bar's room in the box it has now.</summary>
    private AtomRoom Room => AtomRules.Room(ActualWidth, ActualHeight);

    /// <summary>[S-15104] The rail's round ends for the room it has now, then the value in it.</summary>
    private void Shape()
    {
        var radius = new CornerRadius(AtomRules.BarRadius(Room));
        _track.CornerRadius = radius;
        _fill.CornerRadius = radius;
        Draw(_scrubbing ? double.NaN : _value);
        ShowThumb();
    }

    private void Draw(double fraction)
    {
        if (!double.IsFinite(fraction))
        {
            return;
        }

        var room = Room;
        var filled = AtomRules.BarFill(fraction, room.Width);
        _clip.Rect = new Rect(0, 0, filled, room.Height);
        _thumbShift.X = Math.Clamp(AtomSpace.Pad + filled - (ThumbSize / 2), 0, Math.Max(0, ActualWidth - ThumbSize));
    }

    private void ShowThumb()
        => _thumb.Opacity = SeekBarFace.ThumbShown(_canSeek, _hovering, _scrubbing, Room.Height) ? 1 : 0;

    /// <summary>The fraction of the rail at <paramref name="x"/> of the bar's box.</summary>
    private double FractionAt(double x) => MusicRules.ScrubFraction(x - AtomSpace.Pad, Room.Width);

    private double FractionAt(PointerRoutedEventArgs e) => FractionAt(e.GetCurrentPoint(this).Position.X);

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!_canSeek || !point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Handled = true;

        // [S-13060] Off the thumb the press waits: a click seeks on the release, a drag is the card's move.
        if (!ReferenceEquals(e.OriginalSource, _thumb))
        {
            _press.Press(point.Position.X, point.Position.Y);
            if (!CapturePointer(e.Pointer))
            {
                // A press that could not be held hears no release: it is a click now.
                _press.Lost();
                Committed?.Invoke(FractionAt(e));
            }

            return;
        }

        _scrubbing = CapturePointer(e.Pointer);
        var fraction = FractionAt(e);
        Draw(fraction);
        ShowThumb();
        Scrubbing?.Invoke(fraction);

        // A click that could not capture is still a click: seek straight away.
        if (!_scrubbing)
        {
            Committed?.Invoke(fraction);
        }
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        // [S-13060] A track press still held past the card's move: nothing took the drag, the bar scrubs from here.
        if (!_scrubbing && _press.Pending)
        {
            var at = e.GetCurrentPoint(this).Position;
            if (_press.Move(at.X, at.Y) != TrackStep.Follow)
            {
                return;
            }

            _scrubbing = true;
            ShowThumb();
        }

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
        // [S-13060] A click on the track: seek where it was pressed.
        if (!_scrubbing && _press.Pending)
        {
            var (x, _) = _press.At;
            _ = _press.Release();
            ReleasePointerCapture(e.Pointer);
            var at = FractionAt(x);
            Draw(at);
            Committed?.Invoke(at);
            e.Handled = true;
            return;
        }

        if (!_scrubbing)
        {
            return;
        }

        var fraction = FractionAt(e);
        _scrubbing = false;
        _press.Lost();
        ReleasePointerCapture(e.Pointer);
        Draw(fraction);
        ShowThumb();
        Committed?.Invoke(fraction);
        e.Handled = true;
    }

    /// <summary>
    /// Capture taken away (the card took the pointer for its move, the surface deactivated mid-drag): no seek; the bar
    /// goes back to the track.
    /// </summary>
    private void OnCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _press.Lost();
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
