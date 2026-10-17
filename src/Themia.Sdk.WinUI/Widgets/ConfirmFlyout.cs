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
/// whole monitor-sized surface, far from the widget, and drawn inside the same region (the
/// countdown's remove, the clipboard's clear and the notes' delete were, and could be cut off).
/// </para>
/// </summary>
public static class ConfirmFlyout
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
    public static Flyout Create(
        string question,
        string action,
        Action confirmed,
        FlyoutPlacementMode placement = FlyoutPlacementMode.Bottom,
        string? detail = null)
    {
        var words = new StackPanel { Spacing = Fluent.Space1 };
        words.Children.Add(Wrapped(question, detail is null ? FluentType.Body : FluentType.BodyStrong, "TextFillColorPrimaryBrush"));

        if (detail is { Length: > 0 })
        {
            // What cannot be undone, under the question: the detail a dialog's body used to carry.
            words.Children.Add(Wrapped(detail, FluentType.Body, "TextFillColorSecondaryBrush"));
        }

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
                    words,
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

#if DEBUG
        // Debug renders only: a popup of its own window does not draw to a bitmap, so a render opens
        // the question inside its window. On the desktop the flag is never set.
        if (RenderInWindow)
        {
            flyout.ShouldConstrainToRootBounds = RenderInWindow;
        }
#endif

        return flyout;
    }

    /// <summary>
    /// Ask beside <paramref name="anchor"/> and answer once: true on the action, false on Cancel,
    /// Escape, a click elsewhere, or a flyout that could not be shown — never mistaken for a yes.
    /// </summary>
    public static Task<bool> AskAsync(
        FrameworkElement anchor,
        string question,
        string action,
        string? detail = null,
        FlyoutPlacementMode placement = FlyoutPlacementMode.Bottom)
    {
        var answer = new TaskCompletionSource<bool>();
        var flyout = Create(question, action, () => answer.TrySetResult(true), placement, detail);
        flyout.Closed += (_, _) => answer.TrySetResult(false);

        if (anchor.XamlRoot is null)
        {
            // Not in a window: refusing beats destroying something unasked.
            Log.Warn(Scope, $"no window for the '{question}' confirmation; nothing was changed");
            answer.TrySetResult(false);
            return answer.Task;
        }

        try
        {
            flyout.ShowAt(anchor);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"the '{question}' confirmation could not be shown; nothing was changed", ex);
            answer.TrySetResult(false);
        }

        return answer.Task;
    }

#if DEBUG
    /// <summary>Set by the widget render before any view is built; see the end of <see cref="Create"/>.</summary>
    internal static bool RenderInWindow { get; set; }
#endif

    private static TextBlock Wrapped(string words, TypeStep step, string colorKey)
    {
        var text = Fluent.Text(step, colorKey);
        text.Text = words;
        text.TextWrapping = TextWrapping.Wrap;
        text.TextTrimming = TextTrimming.None;
        text.MaxWidth = MaxWidth;
        return text;
    }
}
