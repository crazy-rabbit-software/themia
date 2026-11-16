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

        e.Handled = true;
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

    /// <summary>[S-5544] The left button came up, or the capture went: what the band selected stays.</summary>
    private bool EndDesktopBand(PointerRoutedEventArgs? e)
    {
        if (!_deskBandArmed)
        {
            return false;
        }

        _deskBandArmed = false;
        if (e is not null)
        {
            _root.ReleasePointerCapture(e.Pointer);
        }

        if (_deskBanding)
        {
            _deskBanding = false;
            _host.DesktopBandEnd();
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

        // [S-5545] Shift held: the shell's classic menu at once, as Explorer.
        _host.DesktopBackgroundMenu(this, at, classic: IsDown(Windows.System.VirtualKey.Shift));
        return true;
    }
}
