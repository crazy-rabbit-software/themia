using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>[S-3721] [S-3727] What the menu asks beside itself: a line of text, an "are you sure?".</summary>
internal partial class MenuPanel
{
    /// <summary>
    /// [S-3721] Ask for a line of text beside the menu: a heading, the box (holding what is there now,
    /// for a name being changed), the reason it cannot be used yet under it, and the button that gives
    /// it (Enter does too; Escape or a click away drops it).
    /// </summary>
    protected void Ask(WidgetTextAsk ask)
    {
        ArgumentNullException.ThrowIfNull(ask);

        var heading = Fluent.Text(FluentType.BodyStrong);
        heading.Text = ask.Title;

        var box = new TextBox
        {
            Text = ask.Initial ?? string.Empty,
            PlaceholderText = ask.Placeholder,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
        };
        AutomationProperties.SetName(box, ask.Title);

        var reason = Fluent.Text(FluentType.Caption, "SystemFillColorCriticalBrush");
        reason.TextWrapping = TextWrapping.Wrap;
        reason.Visibility = Visibility.Collapsed;

        var accept = new Button { Content = ask.Accept, HorizontalAlignment = HorizontalAlignment.Right };
        FluentLayout.ApplyStyle(accept, "AccentButtonStyle");

        var flyout = new Flyout
        {
            Content = new StackPanel { Spacing = Fluent.Space2, Children = { heading, box, reason, accept } },
            Placement = FlyoutPlacementMode.RightEdgeAlignedTop,
            ShouldConstrainToRootBounds = true,
            FlyoutPresenterStyle = MenuWide(),
        };

        string? Check()
        {
            var typed = box.Text.Trim();
            return typed.Length == 0 ? (ask.AllowEmpty ? null : string.Empty) : ask.Refuse(typed);
        }

        void Judge()
        {
            var refused = Check();
            accept.IsEnabled = refused is null;
            reason.Text = refused ?? string.Empty;
            reason.Visibility = string.IsNullOrEmpty(refused) ? Visibility.Collapsed : Visibility.Visible;
        }

        void Give()
        {
            if (Check() is not null)
            {
                return;
            }

            var typed = box.Text.Trim();
            flyout.Hide();
            Run(() => ask.Done(typed));
        }

        Judge();
        box.TextChanged += (_, _) => Judge();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                Give();
                e.Handled = true;
            }
        };
        accept.Click += (_, _) => Give();
        flyout.Opened += (_, _) =>
        {
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
        };
        flyout.Closed += (_, _) => OnAskClosed();
        flyout.ShowAt(AskAnchor);
    }

    /// <summary>
    /// [S-3727] Ask beside the menu before a line destroys something: the app's one "are you sure?"
    /// (<see cref="ConfirmFlyout"/>: the question, the action and Cancel). Only the action runs it;
    /// Cancel, Escape or a click away leave everything as it was.
    /// </summary>
    protected void Confirm(WidgetConfirm confirm)
    {
        ArgumentNullException.ThrowIfNull(confirm);

        var flyout = ConfirmFlyout.Create(confirm.Question, confirm.Accept, () => Run(confirm.Done), FlyoutPlacementMode.RightEdgeAlignedTop, confirm.Detail);
        flyout.FlyoutPresenterStyle = MenuWide();
        flyout.Closed += (_, _) => OnAskClosed();
        flyout.ShowAt(AskAnchor);
    }

    /// <summary>
    /// [S-3734] The menu's one width for what it asks beside itself too (a line of text, an "are you
    /// sure?"): the question never grows or shrinks with its words or the reason under the box.
    /// </summary>
    private static Style MenuWide() => new(typeof(FlyoutPresenter))
    {
        Setters =
        {
            new Setter(FrameworkElement.WidthProperty, MenuWidth),
            new Setter(FrameworkElement.MinWidthProperty, MenuWidth),
            new Setter(FrameworkElement.MaxWidthProperty, MenuWidth),
        },
    };
}
