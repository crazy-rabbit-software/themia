using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1500] The one "are you sure?" a widget asks at itself: a question, the action and Cancel, in a
/// flyout beside what was clicked. Escape, a click elsewhere or Cancel are all "no".
/// <para>
/// <b>Never constrained to the root.</b> A flyout's default is to open as a popup inside the window,
/// and the desktop surface's window is cut to its widgets by <c>SetWindowRgn</c> ([S-851]): a
/// question that opens past the card's edge is clipped to the card (the scratchpad's clear) or
/// vanishes entirely (the folder's delete, which opens under the tile). Unconstrained, the popup is
/// its own window, outside the region, and the framework keeps it inside the display's work area,
/// flipping it to the side that has room — the path the card's context menu (<c>MenuFlyout</c>'s
/// default) and the fault details already take. Not a <c>ContentDialog</c>: that is centred on the
/// whole monitor-sized surface, far from the widget, and drawn inside the same region.
/// </para>
/// </summary>
internal static class ConfirmFlyout
{
    private const string Scope = "confirm";

    /// <summary>The widest the question runs before it wraps; it never needs the tile's width.</summary>
    private const double MaxWidth = 260;

    /// <summary>
    /// Build the flyout; <paramref name="confirmed"/> runs on the action button only. The caller
    /// attaches it to a button (<c>Button.Flyout</c>) or shows it with <c>ShowAt</c>. Below by default,
    /// over the widget's own content: a header button has the tile under it, and the framework flips
    /// it above when the work area has no room below.
    /// </summary>
    public static Flyout Create(string question, string action, Action confirmed, FlyoutPlacementMode placement = FlyoutPlacementMode.Bottom)
    {
        var text = Fluent.Text(FluentType.Body);
        text.Text = question;
        text.TextWrapping = TextWrapping.Wrap;
        text.TextTrimming = TextTrimming.None;
        text.MaxWidth = MaxWidth;

        var yes = new Button { Content = action };
        if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var style) && style is Style accent)
        {
            yes.Style = accent;
        }

        var no = new Button { Content = Str.Common_Cancel.Text() };

        var flyout = new Flyout
        {
            Content = new StackPanel
            {
                Spacing = Fluent.Space3,
                Children =
                {
                    text,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = Fluent.Space2,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { yes, no },
                    },
                },
            },
            Placement = placement,
            ShouldConstrainToRootBounds = false,
        };

        yes.Click += (_, _) =>
        {
            // The answer before the hide: a caller that reads Closed as "no" must already have its "yes".
            try
            {
                confirmed();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, $"'{action}' could not be carried out", ex);
            }

            flyout.Hide();
        };

        no.Click += (_, _) => flyout.Hide();

        return flyout;
    }
}
