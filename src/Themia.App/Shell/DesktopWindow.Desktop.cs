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
    /// <summary>Which band a press on empty surface draws.</summary>
    private enum BandKind
    {
        /// <summary>[S-5544] The desktop's band (a left press on empty desktop or a fence's empty body): the host's, in screen space.</summary>
        Desktop,

        /// <summary>[S-13007] The right button's rectangle a fence is made on.</summary>
        Fence,

        /// <summary>[S-1581] The edit-mode lasso over this display's widgets.</summary>
        Lasso,
    }

    /// <summary>
    /// [S-5544] [S-13007] [S-1581] The one rubber band of this surface: its kind, its gesture (<see cref="BandGesture"/>: where
    /// it was pressed, whether it left the 5-DIP dead zone, [S-6624]), the modifiers at its press, and for the lasso the
    /// selection it adds to and how many widgets it touches. Null while no band is armed (the press captured).
    /// </summary>
    private sealed class RubberBand(BandKind kind, BandGesture gesture)
    {
        public BandKind Kind { get; } = kind;

        public BandGesture Gesture { get; set; } = gesture;

        public bool Ctrl { get; init; }

        public bool Shift { get; init; }

        /// <summary>[S-13020] The desktop's band began on empty body of a fence: it offers no area at its release.</summary>
        public bool InFence { get; init; }

        /// <summary>[S-1581] What the lasso adds to (Ctrl or Shift at its press).</summary>
        public IReadOnlyList<string> Base { get; init; } = [];

        /// <summary>[S-13018] The widgets the lasso touches now.</summary>
        public int Caught { get; set; }

        public Point Start => new(Gesture.StartX, Gesture.StartY);
    }

    private RubberBand? _band;

    /// <summary>[S-6627] Every band's shape on this surface, in the SDK's one look (<see cref="SelectionChrome.PaintBand"/>).</summary>
    private Border? _bandShape;

    /// <summary>[S-13007] A fence's rectangle was let go: its menu opened instead of the background menu.</summary>
    private bool _fenceMenuShown;

    /// <summary>The band, armed when the press is captured (then the pointer is reported past this surface's edges too).</summary>
    private RubberBand? Arm(PointerRoutedEventArgs e, RubberBand band) => _root.CapturePointer(e.Pointer) ? band : null;

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

        var at = new BandGesture(point.Position.X, point.Position.Y);
        if (left)
        {
            _band = Arm(e, new RubberBand(BandKind.Desktop, at) { Ctrl = ctrl, Shift = shift });
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            // [S-13007] A right press may become the rectangle a fence is made on.
            _fenceMenuShown = false;
            _band = Arm(e, new RubberBand(BandKind.Fence, at));
        }

        e.Handled = true;
    }

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
        _band = Arm(press, new RubberBand(BandKind.Desktop, new BandGesture(point.Position.X, point.Position.Y)) { Ctrl = ctrl, Shift = shift, InFence = true });
        return _band is not null;
    }

    /// <summary>
    /// [S-5593] Ctrl + mouse wheel over empty desktop or a desktop icon, outside edit mode: the desktop's
    /// icon size one step up or down, as Explorer's. Over any other widget the wheel is that widget's
    /// (a list scrolls). The surface hears the wheel wherever it hears a press: Windows sends it to the
    /// window under the pointer, active or not.
    /// </summary>
    private void OnDesktopWheel(object sender, PointerRoutedEventArgs e)
    {
        if (_closed || EditMode)
        {
            return;
        }

        // [S-13043] The plain wheel over a fence (its body or an icon in it) scrolls it a row a notch, on the grid.
        if (!e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control))
        {
            var wheel = e.GetCurrentPoint(_root);
            if (!e.Handled && _host.FenceWheel(this, wheel.Position, wheel.Properties.MouseWheelDelta))
            {
                e.Handled = true;
            }

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

    /// <summary>
    /// [S-5544] [S-13007] [S-1581] The pointer moved while a band is armed: past the dead zone it is drawn, and from then on
    /// it follows the pointer until it is let go, however small (<see cref="BandGesture.Moved"/>).
    /// </summary>
    private void UpdateBand(Point now)
    {
        if (_band is not { } band)
        {
            return;
        }

        var wasDrawn = band.Gesture.Drawn;
        band.Gesture = band.Gesture.Moved(now.X, now.Y);
        if (!band.Gesture.Drawn)
        {
            return;
        }

        switch (band.Kind)
        {
            case BandKind.Desktop:
                if (!wasDrawn)
                {
                    _host.DesktopBandBegin(band.Ctrl, band.Shift);
                }

                // The pointer is captured here, so it is reported past this surface's edges too: the host
                // makes the one band of it and hands every display its part.
                _host.DesktopBand(this, band.Start, now);
                break;

            case BandKind.Fence:
                ShowBand(band.Gesture.To(now.X, now.Y));
                break;

            case BandKind.Lasso:
                UpdateLasso(band, now);
                break;
        }
    }

    /// <summary>
    /// [S-5544] This display's part of the desktop's band, in its own DIPs (the window cuts it to the
    /// work area, [S-21]); null when the band does not reach this display, or is over.
    /// </summary>
    public void ShowDesktopBand(BandRect? part)
    {
        if (part is not { } band || _closed)
        {
            HideBand(_bandShape);

            return;
        }

        ShowBand(band);
    }

    /// <summary>The DIP-to-pixel factor this surface draws at, for the host's screen-space band.</summary>
    public double BandScale => RasterScale;

    /// <summary>
    /// [S-5544] [S-13007] [S-1581] The button came up, or the capture went (<paramref name="e"/> null): what a band selected
    /// stays. [S-13018] A desktop band or lasso let go over empty desktop offers its area's menu at the release; a fence's
    /// rectangle opens "Create fence here" ([S-13007]). False for a right press that drew nothing: the background menu is
    /// its release's.
    /// </summary>
    private bool EndBand(PointerRoutedEventArgs? e)
    {
        if (_band is not { } band)
        {
            return false;
        }

        _band = null;
        if (e is not null)
        {
            _root.ReleasePointerCapture(e.Pointer);
        }

        HideBand(_bandShape);
        if (!band.Gesture.Drawn)
        {
            return band.Kind != BandKind.Fence;
        }

        var end = e?.GetCurrentPoint(_root).Position;
        switch (band.Kind)
        {
            case BandKind.Desktop:
                var caught = _host.BandCaught;
                _host.DesktopBandEnd();
                if (end is { } at && !band.InFence && _host.LassoReleased(this, band.Start, at, caught, ReleaseHeldBand))
                {
                    HoldBand(_bandShape);
                }

                break;

            case BandKind.Fence:
                if (end is { } corner)
                {
                    _fenceMenuShown = true;
                    if (_host.FenceRectDrawn(this, band.Start, corner, ReleaseHeldBand))
                    {
                        HoldBand(_bandShape);
                    }
                }

                break;

            case BandKind.Lasso:
                if (end is { } released && _host.LassoReleased(this, band.Start, released, band.Caught, ReleaseHeldBand))
                {
                    HoldBand(_bandShape);
                }

                break;
        }

        return true;
    }

    /// <summary>[S-13024] The band whose area menu is open: it stays drawn until the menu closes.</summary>
    private FrameworkElement? _heldBand;

    /// <summary>
    /// [S-13024] A band's area menu opened (owner, 2026-11-19: "when the add widget here context menu comes up the lasso
    /// select should stay visible"): <paramref name="band"/> stays drawn where it was let go while the menu is open.
    /// </summary>
    private void HoldBand(FrameworkElement? band)
    {
        if (band is null || _closed)
        {
            return;
        }

        _heldBand = band;
        band.Visibility = Visibility.Visible;
    }

    /// <summary>[S-13024] The area menu closed: its band goes, unless a new band already took the shape over.</summary>
    private void ReleaseHeldBand()
    {
        if (_heldBand is not { } band)
        {
            return;
        }

        _heldBand = null;
        if (_band is null)
        {
            band.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>A band is taken down at its end, unless its area menu holds it ([S-13024]).</summary>
    private void HideBand(FrameworkElement? band)
    {
        if (band is not null && !ReferenceEquals(band, _heldBand))
        {
            band.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>[S-6627] <paramref name="rect"/> (this surface's DIPs) drawn as the band.</summary>
    private void ShowBand(BandRect rect)
    {
        var shape = EnsureBandShape();
        Canvas.SetLeft(shape, rect.Left);
        Canvas.SetTop(shape, rect.Top);
        shape.Width = rect.Width;
        shape.Height = rect.Height;
        shape.Visibility = Visibility.Visible;
    }

    /// <summary>[S-6627] The band in the SDK's one look, on the ghost layer (never hit-testable).</summary>
    private Border EnsureBandShape()
    {
        if (_bandShape is not null)
        {
            return _bandShape;
        }

        _bandShape = new Border { IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        SelectionChrome.PaintBand(_bandShape);
        _ghostLayer.Children.Add(_bandShape);
        return _bandShape;
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
