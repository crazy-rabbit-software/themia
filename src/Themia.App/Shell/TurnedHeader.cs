using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-405] Holds the card's header and, for a strip down a side edge, stands it on end.
/// <para>
/// WinUI has no layout transform, and a render transform alone leaves the layout believing the
/// header is still lying flat: the strip would be as wide as the card and one line high. So this
/// panel does the swap itself: it measures the header against the strip's length, reports the
/// header's height as its width, lays it out flat at length × thickness and turns it into place
/// with a render transform (<see cref="HeaderStrip.Rotation"/>), which also turns its hit-testing.
/// Unturned it passes the header straight through. Turned, the card lays it out left to right
/// ([S-406]): the turn is physical.
/// </para>
/// </summary>
internal sealed partial class TurnedHeader : Panel
{
    private readonly FrameworkElement _header;
    private readonly CompositeTransform _transform = new();
    private HeaderTurn _turn;

    /// <summary>The length the turned header was last measured against.</summary>
    private double _measuredLength = double.NaN;

    public TurnedHeader(FrameworkElement header)
    {
        _header = header;
        Children.Add(header);
    }

    public HeaderTurn Turn
    {
        get => _turn;
        set
        {
            if (_turn == value)
            {
                return;
            }

            _turn = value;
            _header.RenderTransform = value == HeaderTurn.None ? null : _transform;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_turn == HeaderTurn.None)
        {
            _header.Measure(availableSize);
            return _header.DesiredSize;
        }

        _measuredLength = availableSize.Height;
        _header.Measure(new Size(availableSize.Height, availableSize.Width));
        return new Size(_header.DesiredSize.Height, _header.DesiredSize.Width);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_turn == HeaderTurn.None)
        {
            _header.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }

        var length = finalSize.Height;
        var thickness = finalSize.Width;

        // An auto column may have measured the strip against an unbounded height; the title only
        // ellipsises in measure, so it is measured again against the length it really has.
        if (!(Math.Abs(_measuredLength - length) < 0.5))
        {
            _measuredLength = length;
            _header.Measure(new Size(length, thickness));
        }

        _header.Arrange(new Rect(0, 0, length, thickness));

        var rotation = HeaderStrip.Rotation(_turn, length, thickness);
        _transform.Rotation = rotation.Degrees;
        _transform.TranslateX = rotation.X;
        _transform.TranslateY = rotation.Y;
        return finalSize;
    }
}
