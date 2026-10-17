using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// The panel behind <see cref="Fluent.EmptyState"/>: a glyph over a sentence that sheds to fit the
/// height it is given ([S-642]), so no widget has to remember to.
/// <para>
/// A StackPanel subclass rather than a new type so every caller that holds the result as a
/// StackPanel keeps compiling, but it lays out its two children itself: a stack measures with
/// unbounded height, which is exactly why the sentence used to overflow the card.
/// </para>
/// <para>
/// The glyph is never hidden through its <c>Visibility</c>. Several views already collapse it on
/// their own rule, and two owners of one property would undo each other on every layout pass.
/// This panel leaves the property alone and simply gives a glyph that does not fit no room: it is
/// arranged into an empty rect, and a collapsed glyph measures to nothing, so the two compose.
/// </para>
/// </summary>
internal sealed partial class EmptyStatePanel : StackPanel
{
    private readonly FrameworkElement _icon;
    private readonly TextBlock _message;
    private bool _showIcon = true;

    public EmptyStatePanel(FrameworkElement icon, TextBlock message)
    {
        _icon = icon;
        _message = message;

        // Set once rather than per pass: trimming only acts when MaxLines actually cuts the text,
        // so it is harmless on a sentence that fits and needs no toggling.
        _message.TextTrimming = TextTrimming.CharacterEllipsis;

        Children.Add(icon);
        Children.Add(message);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var width = Math.Max(0, availableSize.Width - padding.Left - padding.Right);
        var height = availableSize.Height - padding.Top - padding.Bottom;

        // The budget depends on the box alone, never on the previous pass, so setting it cannot
        // start a measure loop: the same box always asks for the same value, and the property is
        // only written when it changes.
        var budget = EmptyStateFit.LineBudget(height, _message.LineHeight);
        if (_message.MaxLines != budget)
        {
            _message.MaxLines = budget;
        }

        _icon.Measure(new Size(width, double.PositiveInfinity));
        _message.Measure(new Size(width, double.PositiveInfinity));

        _showIcon = EmptyStateFit.ShowsIcon(height, _icon.DesiredSize.Height, _message.DesiredSize.Height);

        var iconWidth = _showIcon ? _icon.DesiredSize.Width : 0;
        var iconHeight = _showIcon ? _icon.DesiredSize.Height : 0;

        return new Size(
            Math.Max(iconWidth, _message.DesiredSize.Width) + padding.Left + padding.Right,
            iconHeight + _message.DesiredSize.Height + padding.Top + padding.Bottom);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = Padding;
        var width = Math.Max(0, finalSize.Width - padding.Left - padding.Right);
        var top = padding.Top;

        if (_showIcon)
        {
            _icon.Arrange(new Rect(padding.Left, top, width, _icon.DesiredSize.Height));
            top += _icon.DesiredSize.Height;
        }
        else
        {
            // No room: an empty rect clips it away without touching its Visibility.
            _icon.Arrange(new Rect(padding.Left, top, 0, 0));
        }

        _message.Arrange(new Rect(padding.Left, top, width, _message.DesiredSize.Height));
        return finalSize;
    }
}
