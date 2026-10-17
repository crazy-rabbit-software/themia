using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-814] What a press on a widget landed on: one of its items (marked through
/// <see cref="WidgetItems"/>), a control, or the widget's background.
/// </summary>
internal static class WidgetPress
{
    /// <summary>
    /// What a press at <paramref name="source"/> landed on, and in which card: the walk goes up to the
    /// first <see cref="WidgetCard"/> (null when the press was on the shell's own chrome).
    /// </summary>
    public static (WidgetCard? Card, PressTarget Target) Locate(DependencyObject? source, bool handled)
    {
        var onItem = false;
        var onControl = false;

        for (var element = source; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is WidgetCard card)
            {
                return (card, SessionInput.Classify(onItem, onControl, handled));
            }

            onItem |= element is UIElement ui && WidgetItems.IsItem(ui);
            onControl |= IsControl(element);
        }

        return (null, PressTarget.Control);
    }

    /// <summary>A control that acts on a press of its own; a scroll viewer or a plain content host does not.</summary>
    private static bool IsControl(DependencyObject element)
        => element is ButtonBase or TextBox or PasswordBox or RichEditBox or AutoSuggestBox or (RangeBase and not ProgressBar)
            or ToggleSwitch or Selector or SelectorItem or NumberBox or ColorPicker or CalendarDatePicker
            or TimePicker or DatePicker or Thumb;
}
