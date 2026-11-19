using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-13060] The family's slider (<see cref="ControlsBlock.RailSlider"/>): a press on its track, outside the thumb, does
/// not set the value on the press, as WinUI's slider does (its <c>SliderContainer</c> handler jumps the thumb there and
/// captures the pointer), so a press that becomes the card's move never also set the volume on its way. The press waits
/// (<see cref="TrackPress"/>): let go without moving, the value jumps to where it was pressed, on the release, where
/// WinUI would have put it (<see cref="SliderTrack.ValueAt"/>); moved past the threshold, the card takes it; still held
/// after that (Lock widgets, a preview: nothing took it), the track follows the pointer as ever. The thumb is the slider's
/// own drag. Our handlers stand on the template's container before the slider's own (they are added first), and mark the
/// track's press handled so the slider's own never runs for it; the container holds the pointer for the release, and
/// gives it up when the card takes it for the move.
/// </summary>
public sealed partial class TrackSlider : Slider
{
    private readonly TrackPress _press = new();
    private FrameworkElement? _container;

    protected override void OnApplyTemplate()
    {
        if (_container is not null)
        {
            _container.PointerPressed -= OnTrackPressed;
            _container.PointerMoved -= OnTrackMoved;
            _container.PointerReleased -= OnTrackReleased;
            _container.PointerCaptureLost -= OnTrackLost;
            _container.PointerCanceled -= OnTrackLost;
        }

        _press.Lost();
        _container = GetTemplateChild("SliderContainer") as FrameworkElement;
        if (_container is not null)
        {
            _container.PointerPressed += OnTrackPressed;
            _container.PointerMoved += OnTrackMoved;
            _container.PointerReleased += OnTrackReleased;
            _container.PointerCaptureLost += OnTrackLost;
            _container.PointerCanceled += OnTrackLost;
        }

        // The slider's own handlers go on the same container now, after ours.
        base.OnApplyTemplate();
    }

    private bool Horizontal => Orientation == Orientation.Horizontal;

    /// <summary>The orientation's track grid, the frame WinUI measures a click in.</summary>
    private FrameworkElement? Track => GetTemplateChild(Horizontal ? "HorizontalTemplate" : "VerticalTemplate") as FrameworkElement;

    private void OnTrackPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_container is null || Track is not { } track || !IsEnabled || OnThumb(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var point = e.GetCurrentPoint(track);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _press.Press(point.Position.X, point.Position.Y);
        e.Handled = true;

        // ignore: without the capture the release still comes here when nothing moved; a move is the card's either way
        _ = _container.CapturePointer(e.Pointer);
    }

    private void OnTrackMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_press.Pending || Track is not { } track)
        {
            return;
        }

        var at = e.GetCurrentPoint(track).Position;
        if (_press.Move(at.X, at.Y) == TrackStep.Follow)
        {
            Value = ValueAt(track, at.X, at.Y);
            e.Handled = true;
        }
    }

    private void OnTrackReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_press.Pending || Track is not { } track)
        {
            return;
        }

        var at = e.GetCurrentPoint(track).Position;
        var (x, y) = _press.At;
        switch (_press.Release())
        {
            case TrackStep.Click:
                Value = ValueAt(track, x, y);
                break;
            case TrackStep.Follow:
                Value = ValueAt(track, at.X, at.Y);
                break;
        }

        _container?.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    /// <summary>The card took the pointer for its move (or the window went): the press does nothing.</summary>
    private void OnTrackLost(object sender, PointerRoutedEventArgs e) => _press.Lost();

    private double ValueAt(FrameworkElement track, double x, double y)
    {
        var thumb = GetTemplateChild(Horizontal ? "HorizontalThumb" : "VerticalThumb") as FrameworkElement;
        var length = Horizontal ? track.ActualWidth : track.ActualHeight;
        var offset = Horizontal ? x : length - y;
        var step = SnapsTo == SliderSnapsTo.Ticks ? TickFrequency : StepFrequency;
        return SliderTrack.ValueAt(offset, length, Horizontal ? thumb?.ActualWidth ?? 0 : thumb?.ActualHeight ?? 0, Minimum, Maximum, step, IsDirectionReversed);
    }

    private bool OnThumb(DependencyObject? source)
    {
        for (var at = source; at is not null && !ReferenceEquals(at, this); at = VisualTreeHelper.GetParent(at))
        {
            if (at is Thumb)
            {
                return true;
            }
        }

        return false;
    }
}
