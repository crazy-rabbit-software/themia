using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.Core.Widgets;
using Themia.App.Widgets;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>[S-5501] [S-5502] [S-5544] [S-5548] The surface as the desktop itself, while Themia's items show.</summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-5544] The band on empty desktop: where it started, whether it has left the dead zone, its modifiers.</summary>
    private Point _deskBandStart;
    private bool _deskBandArmed;
    private bool _deskBanding;
    private bool _deskBandCtrl;
    private bool _deskBandShift;
    private Border? _deskBand;

    /// <summary>
    /// [S-5502] [S-5544] [S-5550] A press on empty desktop outside edit mode. It reaches the surface only
    /// under Themia's items, whose region covers the work area ([S-5501]); a press on a card is the
    /// card's. The item selection goes (Ctrl or Shift keep it for the band), the desktop takes the keys,
    /// and a left press may become the band; the right button's menu opens on its release
    /// (<see cref="OnRootRightTapped"/>).
    /// </summary>
    private void OnDesktopPressed(PointerRoutedEventArgs e)
    {
        if (_closed || !_host.ShowsDesktopItems || !ReferenceEquals(e.OriginalSource, _root))
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

        var metrics = Metrics;
        var band = SelectionBand.Clamp(new BandRect(_deskBandStart.X, _deskBandStart.Y, now.X, now.Y), metrics.Width, metrics.Height);
        var shape = EnsureDesktopBand();
        Canvas.SetLeft(shape, band.Left);
        Canvas.SetTop(shape, band.Top);
        shape.Width = band.Width;
        shape.Height = band.Height;
        shape.Visibility = Visibility.Visible;
        _host.DesktopBand(this, band);
    }

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

        if (_deskBand is not null)
        {
            _deskBand.Visibility = Visibility.Collapsed;
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

    /// <summary>[S-5548] A right-click on empty desktop under Themia's items, outside edit mode: the desktop's own background menu.</summary>
    private bool OpenDesktopBackgroundMenu(Point at)
    {
        if (EditMode || !_host.ShowsDesktopItems)
        {
            return false;
        }

        _host.DesktopBackgroundMenu(this, at);
        return true;
    }
}
