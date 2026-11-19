using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12809] A part drawn larger by <see cref="Factor"/> in the room it is given: laid out in that room shrunk by the
/// scale, then drawn scaled up, so it fills the room exactly and its type grows with it (the month's cells and
/// weekday line). A scale under 1 is 1.
/// </summary>
public sealed partial class ScaleBox : Panel
{
    private readonly ScaleTransform _scale = new();
    private double _factor = 1;

    /// <summary>How much larger the part is drawn than it is laid out.</summary>
    public double Factor
    {
        get => _factor;
        set
        {
            var next = double.IsFinite(value) ? Math.Max(1, value) : 1;
            if (Math.Abs(next - _factor) > 1e-6)
            {
                _factor = next;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>The part.</summary>
    public UIElement? Child
    {
        get => Children.Count > 0 ? Children[0] : null;
        set
        {
            Children.Clear();
            if (value is not null)
            {
                value.RenderTransform = _scale;
                Children.Add(value);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }

        child.Measure(new Size(availableSize.Width / _factor, availableSize.Height / _factor));
        return new Size(child.DesiredSize.Width * _factor, child.DesiredSize.Height * _factor);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _scale.ScaleX = _factor;
        _scale.ScaleY = _factor;
        Child?.Arrange(new Rect(0, 0, finalSize.Width / _factor, finalSize.Height / _factor));
        return finalSize;
    }
}
