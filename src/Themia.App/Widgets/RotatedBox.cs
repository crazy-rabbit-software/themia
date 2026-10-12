using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-468] [S-774] A panel that turns its children by any angle and still fits them.
/// <para>
/// A render transform alone rotates the child <i>after</i> layout, so a picture laid out for a wide
/// frame and then turned sideways overflows top and bottom and is cut off. Here the child is laid
/// out at the largest box that, once turned, still fits the frame (<see cref="StickerRules.FitRotated"/>)
/// — a quarter turn lays a 320x240 frame's child out at 240x320 — and only then turned, so a rotated
/// camera or a tilted sticker fits the frame exactly as an upright one does.
/// </para>
/// <para>
/// The box has the child's own shape when the caller knows it (<see cref="ChildAspect"/>, a sticker's
/// picture), else the frame's shape turned with it (the video stream, whose picture fits itself).
/// It also clips to its own bounds: the "cover" fit deliberately overflows, and the overflow must
/// stop at the card rather than paint over its neighbours.
/// </para>
/// </summary>
internal sealed partial class RotatedBox : Panel
{
    private readonly RectangleGeometry _clip = new();
    private double _angle;
    private double? _aspect;

    public RotatedBox()
    {
        Clip = _clip;
    }

    /// <summary>[S-468] A quarter turn, for the video stream.</summary>
    public StreamRotation Turn
    {
        get => (StreamRotation)(int)_angle;
        set => Angle = (int)value;
    }

    /// <summary>[S-774] The turn in degrees, clockwise.</summary>
    public double Angle
    {
        get => _angle;
        set
        {
            if (_angle.Equals(value))
            {
                return;
            }

            _angle = value;
            InvalidateMeasure();
            InvalidateArrange();
        }
    }

    /// <summary>[S-774] The child's width over its height, or null to give it the frame's shape (turned with it).</summary>
    public double? ChildAspect
    {
        get => _aspect;
        set
        {
            if (Nullable.Equals(_aspect, value))
            {
                return;
            }

            _aspect = value;
            InvalidateMeasure();
            InvalidateArrange();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var inner = Inner(availableSize);

        foreach (var child in Children)
        {
            child.Measure(inner);
        }

        // Take the whole frame: the picture inside decides how much of it to paint.
        return new Size(
            double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var inner = Inner(finalSize);

        // Centred on the frame, so the turn about the child's own centre lands it back in place.
        var origin = new Point((finalSize.Width - inner.Width) / 2, (finalSize.Height - inner.Height) / 2);

        foreach (var child in Children)
        {
            child.RenderTransformOrigin = new Point(0.5, 0.5);

            if (_angle == 0)
            {
                child.RenderTransform = null;
            }
            else
            {
                // One transform per child: a transform is not shared between elements.
                if (child.RenderTransform is not RotateTransform turn)
                {
                    turn = new RotateTransform();
                    child.RenderTransform = turn;
                }

                turn.Angle = _angle;
            }

            child.Arrange(new Rect(origin, inner));
        }

        _clip.Rect = new Rect(0, 0, finalSize.Width, finalSize.Height);
        return finalSize;
    }

    private Size Inner(Size frame)
    {
        if (double.IsInfinity(frame.Width) || double.IsInfinity(frame.Height))
        {
            // Unbounded: nothing to fit against, so the frame's own (swapped when sideways) measure stands.
            var sideways = Math.Abs(Math.Sin(_angle * Math.PI / 180)) > Math.Sqrt(0.5);
            return sideways ? new Size(frame.Height, frame.Width) : frame;
        }

        var box = StickerRules.FitRotated(frame.Width, frame.Height, _angle, _aspect);
        return new Size(box.Width, box.Height);
    }
}
