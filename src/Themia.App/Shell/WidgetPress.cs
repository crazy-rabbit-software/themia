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

            // [S-13975] What a view draws past its card (an icon's rename box in icon only) is the card's.
            if (element is CardOverhang overhang)
            {
                return (overhang.Card, SessionInput.Classify(onItem, onControl, handled));
            }

            onItem |= element is UIElement ui && WidgetItems.IsItem(ui);
            onControl |= IsControl(element);
        }

        return (null, PressTarget.Control);
    }

    /// <summary>
    /// [S-13060] What a press at <paramref name="source"/> met first on its way up to <paramref name="card"/>, for the card's
    /// move: a part marked so (<see cref="PressParts"/>: a seek bar's track and thumb), a slider's thumb, a text box (at rest
    /// when the press itself gave it the focus, <paramref name="focusedByPress"/>; typed in otherwise), a slider's track, a
    /// button or a toggle (a widget that is one button too), an item (a row, a tile); else the ground.
    /// </summary>
    public static PressPart Part(DependencyObject? source, DependencyObject card, DependencyObject? focusedByPress)
    {
        for (var element = source; element is not null && !ReferenceEquals(element, card); element = VisualTreeHelper.GetParent(element))
        {
            if (element is UIElement ui && PressParts.Marked(ui, out var marked))
            {
                return marked;
            }

            switch (element)
            {
                case Thumb:
                    return PressPart.Thumb;
                case TextBox or PasswordBox or RichEditBox:
                    return Within(focusedByPress, element) ? PressPart.Text : PressPart.Typing;
                case RangeBase and not ProgressBar:
                    return PressPart.Track;
                case ButtonBase or ToggleSwitch or Widgets.IWidgetButton:
                    return PressPart.Control;
                case FrameworkElement item when WidgetItems.IsItem(item) || Widgets.ItemMark.IsItem(item):
                    return PressPart.Item;
            }
        }

        return PressPart.Ground;
    }

    /// <summary>Whether <paramref name="node"/> is <paramref name="ancestor"/> or inside it.</summary>
    private static bool Within(DependencyObject? node, DependencyObject ancestor)
    {
        for (var at = node; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (ReferenceEquals(at, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A control that acts on a press of its own; a scroll viewer or a plain content host does not.</summary>
    private static bool IsControl(DependencyObject element)
        => element is ButtonBase or TextBox or PasswordBox or RichEditBox or AutoSuggestBox or (RangeBase and not ProgressBar)
            or ToggleSwitch or Selector or SelectorItem or NumberBox or ColorPicker or CalendarDatePicker
            or TimePicker or DatePicker or Thumb;
}
