using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// A label with something small hugging its end — an app's colour dot, a session count — where the
/// label shrinks to its text, ellipsises when the room runs out, and the trailing piece always
/// stays right after it.
/// <para>
/// Neither stock panel does this. A horizontal stack measures the label with unbounded width, so
/// it never ellipsises and runs under whatever is beside it; a grid with an auto column does the
/// same; and a star column parks the trailing piece at the far end instead of after the text. So the
/// trailing piece is measured first and the label is given exactly what is left.
/// </para>
/// </summary>
internal sealed partial class HugRow : Panel
{
    private readonly TextBlock _label;
    private readonly FrameworkElement _trailing;

    public HugRow(TextBlock label, FrameworkElement trailing, double gap)
    {
        _label = label;
        _trailing = trailing;
        Gap = gap;
        Children.Add(label);
        Children.Add(trailing);
    }

    /// <summary>The space between the label and the trailing piece.</summary>
    public double Gap { get; }

    protected override Size MeasureOverride(Size availableSize)
    {
        _trailing.Measure(new Size(double.PositiveInfinity, availableSize.Height));

        var trailing = _trailing.Visibility == Visibility.Visible ? _trailing.DesiredSize.Width + Gap : 0;
        var room = double.IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width - trailing) : double.PositiveInfinity;

        _label.Measure(new Size(room, availableSize.Height));

        return new Size(
            _label.DesiredSize.Width + trailing,
            Math.Max(_label.DesiredSize.Height, _trailing.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var label = Math.Min(_label.DesiredSize.Width, finalSize.Width);
        _label.Arrange(new Rect(0, (finalSize.Height - _label.DesiredSize.Height) / 2, label, _label.DesiredSize.Height));

        if (_trailing.Visibility == Visibility.Visible)
        {
            var x = Math.Min(label + Gap, Math.Max(0, finalSize.Width - _trailing.DesiredSize.Width));
            _trailing.Arrange(new Rect(x, (finalSize.Height - _trailing.DesiredSize.Height) / 2, _trailing.DesiredSize.Width, _trailing.DesiredSize.Height));
        }

        return finalSize;
    }
}
