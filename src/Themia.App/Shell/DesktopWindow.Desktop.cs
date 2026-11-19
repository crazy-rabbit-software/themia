using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.Core.Widgets;
using Themia.App.Widgets;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>[S-5501] [S-5502] [S-5544] [S-5548] The surface as the desktop itself, outside edit mode, whether its icons show or not.</summary>
internal sealed partial class DesktopWindow
{
    /// <summary>
    /// [S-5544] The band on empty desktop, on the surface the press landed on: where it started, whether
    /// it has left the dead zone, its modifiers. The band itself is the host's, one rectangle in screen
    /// space; <see cref="_deskBand"/> is the shape every surface it reaches draws its part with.
    /// </summary>
    private Point _deskBandStart;
    private bool _deskBandArmed;
    private bool _deskBanding;
    private bool _deskBandCtrl;
    private bool _deskBandShift;
    private Border? _deskBand;

    /// <summary>
    /// [S-13007] A rectangle drawn with the right button on empty desktop, as Fences makes a fence: where it started,
    /// whether it is armed (the press captured) and drawing (past the dead zone), and its shape on the ghost layer. Its
    /// release opens "Create fence here" instead of the background menu.
    /// </summary>
    private Point _fenceStart;
    private bool _fenceArmed;
    private bool _fenceDrawing;
    private bool _fenceMenuShown;
    private Border? _fenceBand;

    /// <summary>
    /// [S-5502] [S-5544] [S-5550] A press on empty desktop outside edit mode: the surface covers the
    /// work area ([S-5501]), so it is always Themia's; a press on a card is the card's. The item selection goes (Ctrl or Shift keep it for the band), the desktop takes the keys,
    /// and a left press may become the band; the right button's menu opens on its release
    /// (<see cref="OnRootRightTapped"/>).
    /// </summary>
    private void OnDesktopPressed(PointerRoutedEventArgs e)
    {
        if (_closed || !ReferenceEquals(e.OriginalSource, _root))
        {
            return;
        }

        var point = e.GetCurrentPoint(_root);
        var left = point.Properties.IsLeftButtonPressed;
        var ctrl = left && IsDown(Windows.System.VirtualKey.Control);
        var shift = left && IsDown(Windows.System.VirtualKey.Shift);
        _host.DesktopEmptyPressed(this, point.Position, ctrl, shift);

        if (left)
        {
            _deskBandStart = point.Position;
            _deskBandCtrl = ctrl;
            _deskBandShift = shift;
            _deskBanding = false;
            _deskBandArmed = _root.CapturePointer(e.Pointer);
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            // [S-13007] A right press may become the rectangle a fence is made on.
            _fenceStart = point.Position;
            _fenceDrawing = false;
            _fenceMenuShown = false;
            _fenceArmed = _root.CapturePointer(e.Pointer);
        }

        e.Handled = true;
    }

    /// <summary>[S-13020] The running band began on empty body of a fence: it offers no area at its release.</summary>
    private bool _deskBandInFence;

    /// <summary>
    /// [S-13020] A left press on empty body of a fence (handed over by its host): the desktop's band from that point, as a
    /// press on empty desktop starts it (Ctrl or Shift keep the selection), the pointer captured here so the band follows it
    /// anywhere. Returns whether it started.
    /// </summary>
    internal bool BeginDesktopBandFrom(PointerRoutedEventArgs press)
    {
        if (_closed || EditMode)
        {
            return false;
        }

        var point = press.GetCurrentPoint(_root);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return false;
        }

        var ctrl = press.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control);
        var shift = press.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift);
        _host.DesktopEmptyPressed(this, point.Position, ctrl, shift, inFence: true);
        _deskBandStart = point.Position;
        _deskBandCtrl = ctrl;
        _deskBandShift = shift;
        _deskBanding = false;
        _deskBandInFence = true;
        _deskBandArmed = _root.CapturePointer(press.Pointer);
        _deskBandInFence = _deskBandArmed;
        return _deskBandArmed;
    }

    /// <summary>[S-13007] The pointer moved with the right button down on empty desktop: past the dead zone it draws the fence's rectangle.</summary>
    private void UpdateFenceBand(Point now)
    {
        if (!_fenceArmed)
        {
            return;
        }

        if (!_fenceDrawing && !SelectionBand.PastThreshold(now.X - _fenceStart.X, now.Y - _fenceStart.Y))
        {
            return;
        }

        _fenceDrawing = true;
        var shape = EnsureFenceBand();
        Canvas.SetLeft(shape, Math.Min(_fenceStart.X, now.X));
        Canvas.SetTop(shape, Math.Min(_fenceStart.Y, now.Y));
        shape.Width = Math.Abs(now.X - _fenceStart.X);
        shape.Height = Math.Abs(now.Y - _fenceStart.Y);
        shape.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// [S-13007] The right button came up (or the capture went): a rectangle drawn opens Fences' small menu at the release
    /// ("Create fence here"); a plain right click goes on to the background menu.
    /// </summary>
    private bool EndFenceBand(PointerRoutedEventArgs? e)
    {
        if (!_fenceArmed)
        {
            return false;
        }

        _fenceArmed = false;
        if (e is not null)
        {
            _root.ReleasePointerCapture(e.Pointer);
        }

        if (_fenceBand is not null)
        {
            _fenceBand.Visibility = Visibility.Collapsed;
        }

        if (!_fenceDrawing)
        {
            return false;
        }

        _fenceDrawing = false;
        if (e is not null)
        {
            _fenceMenuShown = true;
            _host.FenceRectDrawn(this, _fenceStart, e.GetCurrentPoint(_root).Position);
        }

        return true;
    }

    /// <summary>[S-13007] The rectangle in the band's one look, on the ghost layer (never hit-testable).</summary>
    private Border EnsureFenceBand()
    {
        if (_fenceBand is not null)
        {
            return _fenceBand;
        }

        _fenceBand = new Border { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        SelectionChrome.PaintBand(_fenceBand);
        _ghostLayer.Children.Add(_fenceBand);
        return _fenceBand;
    }

    /// <summary>
    /// [S-5593] Ctrl + mouse wheel over empty desktop or a desktop icon, outside edit mode: the desktop's
    /// icon size one step up or down, as Explorer's. Over any other widget the wheel is that widget's
    /// (a list scrolls). The surface hears the wheel wherever it hears a press: Windows sends it to the
    /// window under the pointer, active or not.
    /// </summary>
    private void OnDesktopWheel(object sender, PointerRoutedEventArgs e)
    {
        if (_closed || EditMode || !e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control))
        {
            return;
        }

        var onDesktop = ReferenceEquals(e.OriginalSource, _root);
        for (var at = e.OriginalSource as DependencyObject; !onDesktop && at is not null; at = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(at))
        {
            if (at is WidgetCard card)
            {
                onDesktop = card.Model.Config is Themia.Core.Model.IDesktopItemConfig;
                break;
            }
        }

        var delta = e.GetCurrentPoint(_root).Properties.MouseWheelDelta;
        if (!onDesktop || delta == 0)
        {
            return;
        }

        _host.StepDesktopIconSize(delta);
        e.Handled = true;
    }

    /// <summary>[S-5544] The pointer moved with the left button down on empty desktop: past the dead zone it is a band, live.</summary>
    private void UpdateDesktopBand(Point now)
    {
        if (!_deskBandArmed)
        {
            return;
        }

        if (!_deskBanding)
        {
            if (!SelectionBand.PastThreshold(now.X - _deskBandStart.X, now.Y - _deskBandStart.Y))
            {
                return;
            }

            _deskBanding = true;
            _host.DesktopBandBegin(_deskBandCtrl, _deskBandShift);
        }

        // The pointer is captured here, so it is reported past this surface's edges too: the host
        // makes the one band of it and hands every display its part.
        _host.DesktopBand(this, _deskBandStart, now);
    }

    /// <summary>
    /// [S-5544] This display's part of the desktop's band, in its own DIPs (the window cuts it to the
    /// work area, [S-21]); null when the band does not reach this display, or is over.
    /// </summary>
    public void ShowDesktopBand(BandRect? part)
    {
        if (part is not { } band || _closed)
        {
            if (_deskBand is not null)
            {
                _deskBand.Visibility = Visibility.Collapsed;
            }

            return;
        }

        var shape = EnsureDesktopBand();
        Canvas.SetLeft(shape, band.Left);
        Canvas.SetTop(shape, band.Top);
        shape.Width = band.Width;
        shape.Height = band.Height;
        shape.Visibility = Visibility.Visible;
    }

    /// <summary>The DIP-to-pixel factor this surface draws at, for the host's screen-space band.</summary>
    public double BandScale => RasterScale;

    /// <summary>
    /// [S-5544] The left button came up, or the capture went: what the band selected stays. [S-13018] A band let go
    /// over empty desktop (it touched nothing) offers its area's menu at the release.
    /// </summary>
    private bool EndDesktopBand(PointerRoutedEventArgs? e)
    {
        if (!_deskBandArmed)
        {
            return false;
        }

        _deskBandArmed = false;
        var inFence = _deskBandInFence;
        _deskBandInFence = false;
        if (e is not null)
        {
            _root.ReleasePointerCapture(e.Pointer);
        }

        if (_deskBanding)
        {
            _deskBanding = false;
            var caught = _host.BandCaught;
            _host.DesktopBandEnd();
            if (e is not null && !inFence)
            {
                _host.LassoReleased(this, _deskBandStart, e.GetCurrentPoint(_root).Position, caught);
            }
        }

        return true;
    }

    /// <summary>[S-6627] The band in the SDK's one look, on the ghost layer (never hit-testable).</summary>
    private Border EnsureDesktopBand()
    {
        if (_deskBand is not null)
        {
            return _deskBand;
        }

        _deskBand = new Border { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        SelectionChrome.PaintBand(_deskBand);
        _ghostLayer.Children.Add(_deskBand);
        return _deskBand;
    }

    /// <summary>[S-5548] A right-click on empty desktop outside edit mode: the desktop's own background menu.</summary>
    private bool OpenDesktopBackgroundMenu(Point at)
    {
        if (EditMode)
        {
            return false;
        }

        // [S-13007] A rectangle was drawn: its release opened "Create fence here" instead.
        if (_fenceMenuShown)
        {
            _fenceMenuShown = false;
            return true;
        }

        // [S-5545] Shift held: the shell's classic menu at once, as Explorer.
        _host.DesktopBackgroundMenu(this, at, classic: IsDown(Windows.System.VirtualKey.Shift));
        return true;
    }
}
