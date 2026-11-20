using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.System;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1500] A widget's one way to rename one of its own things (a note, a task list): a text box in a small popup under
/// <c>anchor</c>, its own window kept in the display's work area by the framework wherever the card sits (never
/// constrained to the root, as <see cref="ConfirmFlyout"/>). Enter or a click elsewhere keeps what is typed; Escape keeps
/// the old name. While it is open the widget holds the keyboard (<c>editing</c>).
/// </summary>
public static class RenameFlyout
{
    /// <param name="anchor">What the popup hangs under.</param>
    /// <param name="current">The name now.</param>
    /// <param name="placeholder">The box's hint.</param>
    /// <param name="minWidth">The box's least width.</param>
    /// <param name="editing">True as the box opens (the widget asks for the keyboard), false as it closes.</param>
    /// <param name="rename">The name typed, when it was kept (after <paramref name="editing"/>(false)).</param>
    /// <param name="closed">After everything, kept or not.</param>
    public static void Show(FrameworkElement anchor, string current, string placeholder, double minWidth, Action<bool> editing, Action<string> rename, Action? closed = null)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(editing);
        ArgumentNullException.ThrowIfNull(rename);

        var box = new TextBox { Text = current, MinWidth = minWidth, PlaceholderText = placeholder };
        var flyout = new Flyout { Content = box, Placement = FlyoutPlacementMode.Bottom, ShouldConstrainToRootBounds = false };
        var cancelled = false;

        box.KeyDown += (_, e) =>
        {
            if (e.Key is VirtualKey.Enter or VirtualKey.Escape)
            {
                e.Handled = true;
                cancelled = e.Key == VirtualKey.Escape;
                flyout.Hide();
            }
        };

        flyout.Opened += (_, _) =>
        {
            editing(true);
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
        };

        flyout.Closed += (_, _) =>
        {
            editing(false);
            if (!cancelled)
            {
                rename(box.Text);
            }

            closed?.Invoke();
        };

        flyout.ShowAt(anchor);
    }
}
