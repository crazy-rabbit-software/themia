using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.System;
using P = Themia.Sdk.Kit.ShellMetrics.Page;

namespace Themia.App.Shell;

/// <summary>[S-2311] A page's preview: drawn once, shown again as the page is drawn again, told the state changed.</summary>
internal interface IPagePreview : IDisposable
{
    /// <summary>The settings changed: draw the cards in the look as it stands now.</summary>
    void Refresh();
}

/// <summary>
/// [S-2310] The user's own things as cards that open (a view, a display, an account: Karpo's things, docs/widgets.md
/// §585), and [S-2311] the Appearance's preview.
/// </summary>
internal sealed partial class PageView
{
    /// <summary>[S-2310] The grip of each thing put in order, by its row's key, as this drawing made them.</summary>
    private readonly Dictionary<string, UIElement> _grips = new(StringComparer.Ordinal);

    /// <summary>[S-2311] The preview, kept from one drawing to the next: its cards are built once.</summary>
    private FrameworkElement? _preview;

    /// <summary>
    /// [S-2310] [S-2317] The grip a thing is dragged by (Windows' gripper, in the secondary text colour), at its head's
    /// start, 16 × 32 (Karpo's §551 handle): a press on it is the drag's, never the head's click.
    /// </summary>
    private FrameworkElement Grip(PageRow row)
    {
        var grip = new Border
        {
            Width = P.Handle,
            Height = P.Control,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(), // hit-testable, clear
            Child = new FontIcon
            {
                Glyph = Glyphs.GripperVertical,
                FontSize = Fluent.IconSize,
                Foreground = Family.Brush(Themia.Sdk.Kit.ColourRole.TextSecondary),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Tag = row.Key + "#grip",
        };
        ToolTipService.SetToolTip(grip, Str.Menu_ReorderHint.Text());
        _grips[row.Key] = grip;
        return grip;
    }

    /// <summary>
    /// [S-2310] [S-3732] A thing's head with the keyboard: Ctrl+Up / Down moves it in its list, its icon buttons' keys
    /// (Delete) work on it.
    /// </summary>
    private void ThingKeys(Button head, PageRow row)
    {
        if (row.Order is null && row.Tools is not { Count: > 0 })
        {
            return;
        }

        head.KeyDown += (_, e) =>
        {
            var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            if (ctrl && row.Order is { } order && e.Key is VirtualKey.Up or VirtualKey.Down)
            {
                var to = order.Index + (e.Key == VirtualKey.Up ? -1 : 1);
                if (to >= 0 && to < order.Count)
                {
                    Do(() => order.Move(order.Index, to));
                    Refocus(new FocusTag(row.Key + "#head", 0));
                }

                e.Handled = true;
                return;
            }

            if (row.Tools?.FirstOrDefault(t => t.Shortcut == e.Key && !t.Disabled) is { } keyed)
            {
                Do(keyed.Run);
                e.Handled = true;
            }
        };
    }

    /// <summary>
    /// [S-2311] The Appearance's preview, bare at the page's top: built by its host once and kept while the page
    /// stands, told to draw the look again each time the page is (every change of the state).
    /// </summary>
    private FrameworkElement PreviewRow(PageRow row)
    {
        if (_preview is null)
        {
            try
            {
                _preview = row.Picker?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "the appearance preview could not be built; the page shows without it", ex);
            }
        }
        else if (_preview is IPagePreview live)
        {
            live.Refresh();
        }

        if (_preview is null)
        {
            return new Border();
        }

        _preview.Margin = new Thickness(0, 0, 0, P.CardGap);
        AutomationProperties.SetName(_preview, row.Text);
        return _preview;
    }

    /// <summary>[S-2311] The page goes: the preview's cards and their samples go with it.</summary>
    private void DropPreview()
    {
        if (_preview is IDisposable owned)
        {
            owned.Dispose();
        }

        _preview = null;
    }
}
