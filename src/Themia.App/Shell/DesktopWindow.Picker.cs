using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;
using PlacementMode = Themia.Core.Model.PlacementMode;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell;

/// <summary>Adding a widget: the picker, the ghost and the click that places it.</summary>
internal sealed partial class DesktopWindow
{
    /// <summary>
    /// [S-3501] The add menu: a flyout under the edit bar's Add widget rather than a page in the
    /// settings window, because choosing a widget is a moment's decision made while looking at the
    /// desktop, and the settings window covers the desktop.
    /// <para>
    /// Its content is rebuilt every time it opens, so the names follow a language switch ([S-1109]),
    /// new kinds in the catalogue appear, and the cascade fits this display's work area ([S-3512]).
    /// </para>
    /// </summary>
    private Flyout BuildAddFlyout()
    {
        var flyout = new Flyout
        {
            // [S-1749] Centred under what opened it: the toolbar's Add button or the right-click
            // point. It flips above only when there is no room below.
            Placement = FlyoutPlacementMode.Bottom,

            // The presenter caps itself at 456px by default, sized for a menu of one column. Left at
            // that, the menu showed two and a half of its columns and clipped the rest.
            FlyoutPresenterStyle = new Style(typeof(FlyoutPresenter))
            {
                Setters =
                {
                    new Setter(FrameworkElement.MaxWidthProperty, double.PositiveInfinity),
                    new Setter(FrameworkElement.MaxHeightProperty, double.PositiveInfinity),
                },
            },
        };

        flyout.Opening += (_, _) =>
        {
            var area = Metrics.IsUsable ? GridGeometry.AllowedPixels(Metrics) : new PixelRect(0, 0, 1280, 720);
            flyout.Content = new WidgetPicker(definition => OnPicked(flyout, definition), flyout.Hide, area);
        };

        // [S-3514] Typing searches at once: the caret is in the box as soon as the picker is open.
        flyout.Opened += (_, _) => (flyout.Content as WidgetPicker)?.FocusSearch();
        return flyout;
    }

    /// <summary>[S-421] A widget was chosen: click-to-place starts.</summary>
    private void OnPicked(Flyout flyout, WidgetDescriptor definition)
    {
        flyout.Hide();
        _host.BeginPlacement(definition.Id);
    }

    /// <summary>
    /// [S-3500] The line that takes the edit bar's place while placing: the bar's own frame and
    /// height, so the one swaps for the other where it stood.
    /// </summary>
    private static Border BuildPlacementHint(TextBlock text)
    {
        var hint = new Border
        {
            Child = text,
            MinHeight = EditBar.ButtonSize + (2 * Fluent.Space1),
            Padding = new Thickness(Fluent.Space4, 0, Fluent.Space4, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,

            // Words, not a control: anything clickable here would have to be told apart from a click
            // meant to place the widget underneath it.
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };

        ChromeFrame.Apply(hint);
        return hint;
    }

    /// <summary>One outline of the ghost: a dashed accent frame with the widget's icon and name.</summary>
    private sealed class GhostCard
    {
        public GhostCard(Brush accent)
        {
            // The fill is the stroke's colour at low alpha: the same accent, so it reads as one shape
            // rather than a box inside a box.
            var colour = (accent as SolidColorBrush)?.Color ?? Colors.DodgerBlue;
            Frame = new Rectangle
            {
                Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, colour.R, colour.G, colour.B)),
                Stroke = accent,
                StrokeThickness = 2,
                StrokeDashArray = [4, 3],
            };

            Icon = new FontIcon { FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center };
            Label = Fluent.Text(FluentType.BodyStrong);
            Label.HorizontalAlignment = HorizontalAlignment.Center;
            Content = new StackPanel
            {
                Spacing = Fluent.Space2,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { Icon, Label },
            };

            Root = new Grid { Children = { Frame, Content }, Visibility = Visibility.Collapsed };
        }

        public Grid Root { get; }

        public Rectangle Frame { get; }

        public FontIcon Icon { get; }

        public TextBlock Label { get; }

        public StackPanel Content { get; }
    }

    /// <summary>Below this the icon and name do not fit in an outline, which then shows the frame alone.</summary>
    private const double GhostLabelMinWidth = 96;

    private const double GhostLabelMinHeight = 72;

    /// <summary>
    /// [S-423]/[S-1585] Show widget-shaped outlines on this display: where the widgets being placed,
    /// or dragged in from another display, will land — one per widget, at its offset in the block.
    /// Each traces the painted card, inside the gap, because that is what the user will see once it
    /// is there. Outlines are pooled: a ghost redraws on every pointer move.
    /// </summary>
    public void ShowGhosts(IReadOnlyList<GhostOutline> outlines)
    {
        ArgumentNullException.ThrowIfNull(outlines);

        var metrics = Metrics;
        if (_closed || !metrics.IsUsable)
        {
            return;
        }

        while (_ghosts.Count < outlines.Count)
        {
            var ghost = new GhostCard(Brush("AccentFillColorDefaultBrush", Colors.DodgerBlue));
            _ghosts.Add(ghost);
            _ghostLayer.Children.Add(ghost.Root);
        }

        for (var i = 0; i < _ghosts.Count; i++)
        {
            var ghost = _ghosts[i];
            if (i >= outlines.Count)
            {
                ghost.Root.Visibility = Visibility.Collapsed;
                continue;
            }

            var box = GridGeometry.Inset(GridGeometry.ToPixels(outlines[i].Rect, metrics), Design.WidgetGapPx);
            var definition = ModuleCatalog.Of(outlines[i].Kind);

            ghost.Icon.Glyph = definition.Glyph;
            ghost.Label.Text = definition.Name;
            ghost.Content.Visibility = box.Width >= GhostLabelMinWidth && box.Height >= GhostLabelMinHeight
                ? Visibility.Visible
                : Visibility.Collapsed;
            ghost.Frame.RadiusX = Design.CornerRadiusPx;
            ghost.Frame.RadiusY = Design.CornerRadiusPx;

            Canvas.SetLeft(ghost.Root, box.X);
            Canvas.SetTop(ghost.Root, box.Y);
            ghost.Root.Width = Math.Max(0, box.Width);
            ghost.Root.Height = Math.Max(0, box.Height);
            ghost.Root.Visibility = Visibility.Visible;
        }
    }

    public void HideGhost()
    {
        foreach (var ghost in _ghosts)
        {
            ghost.Root.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>[S-06] Snap now: the design's grid aid, unless Alt is held, as when dragging.</summary>
    private bool SnapNow => Design.GridSnap && !AltHeld();

    /// <summary>What a payload's ghost draws with the pointer at a physical screen point of this display.</summary>
    public IReadOnlyList<GhostOutline> OutlinesAt(Win32.Point cursor, PlacementPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var (x, y) = FractionOf(cursor);
        return payload.Outlines(x, y, Metrics, SnapNow);
    }

    /// <summary>What landing a payload with the pointer at a physical screen point of this display does.</summary>
    public PastePlan PlanAt(Win32.Point cursor, PlacementPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var (x, y) = FractionOf(cursor);
        return payload.Plan(x, y, Metrics, SnapNow);
    }

    /// <summary>
    /// [S-73] [S-3717] Fade the widgets that are about to go: the originals of a move while it is
    /// aimed, and in edit mode the widgets cut and not yet pasted, as Explorer dims a cut file, so a
    /// Cut shows at once that it took.
    /// </summary>
    private void FadeLeaving()
    {
        var placing = _host.Placing;
        var clipboard = _host.Clipboard;
        var cut = EditMode && clipboard.Mode == ClipboardMode.Cut ? clipboard.Payload : null;
        foreach (var card in _cards)
        {
            var id = card.Model.Id;
            card.SetLeaving((placing is { Mode: PlacementMode.Move } && placing.Holds(id)) || (cut?.Holds(id) ?? false));
        }
    }

    /// <summary>[S-3717] The clipboard changed: the cut's fade follows it.</summary>
    private void OnClipboardChanged()
    {
        if (_closed)
        {
            return;
        }

        FadeLeaving();
    }

    /// <summary>
    /// [S-421]/[S-1587] Enter or leave placing. The cards stop taking input while something is being
    /// placed, so a click over an existing widget drops the payload there instead of grabbing the old
    /// one, and the toolbar steps aside for a line saying what the click will do. The originals of a
    /// move are faded while they are aimed, so it is clear which widgets are about to go.
    /// </summary>
    private void OnPlacementChanged()
    {
        if (_closed)
        {
            return;
        }

        var placing = _host.Placing;
        _widgetLayer.IsHitTestVisible = placing is null;

        FadeLeaving();

        if (placing is not null)
        {
            EndLasso();
            _placementHintText.Text = Str.Shell_Place_Hint.Format(("action", PlacementVerb(placing)));
            _placementHint.Visibility = Visibility.Visible;
            _toolbar.Visibility = Visibility.Collapsed;
            CloseBarMenu(focusButton: false);
            PlaceProperties();

            // The command was given with the pointer already somewhere: show the ghost there at once
            // rather than waiting for the first move.
            if (IsUnderCursor && Win32.GetCursorPos(out var cursor))
            {
                ShowGhosts(OutlinesAt(cursor, placing));
            }
            else
            {
                HideGhost();
            }

            return;
        }

        _placementHint.Visibility = Visibility.Collapsed;
        _toolbar.Visibility = EditMode ? Visibility.Visible : Visibility.Collapsed;
        PlaceProperties();
        HideGhost();
    }

    /// <summary>What the click will do, in words: "Click to place Clock", "Click to move 3 widgets".</summary>
    private static string PlacementVerb(PlacementPayload payload)
    {
        if (payload.SingleKind is { } kind)
        {
            var name = ModuleCatalog.Of(kind).Name;
            return payload.Mode switch
            {
                PlacementMode.Move => Str.Shell_Place_MoveOne.Format(("name", name)),
                PlacementMode.Copy => Str.Shell_Place_CopyOne.Format(("name", name)),
                _ => Str.Shell_Place_PlaceOne.Format(("name", name)),
            };
        }

        return payload.Mode switch
        {
            PlacementMode.Move => Str.Shell_Place_MoveMany.Plural(payload.Count),
            PlacementMode.Copy => Str.Shell_Place_CopyMany.Plural(payload.Count),
            _ => Str.Shell_Place_PlaceMany.Plural(payload.Count),
        };
    }

    /// <summary>Where a point in this surface's own coordinates falls, as a fraction of it.</summary>
    private (double X, double Y) FractionOfDip(Point dip)
    {
        var metrics = Metrics;
        return metrics.IsUsable
            ? (Math.Clamp(dip.X / metrics.Width, 0, 1), Math.Clamp(dip.Y / metrics.Height, 0, 1))
            : (0.5, 0.5);
    }

    private static bool AltHeld() => InputKeyboardSource
        .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu)
        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private void OnRootPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(_root).Position;

        if (_host.Placing is { } payload)
        {
            var (x, y) = FractionOfDip(position);
            ShowGhosts(payload.Outlines(x, y, Metrics, SnapNow));
            return;
        }

        UpdateLasso(position);
    }

    /// <summary>The pointer went to another display: the ghost goes with it, so it leaves here.</summary>
    private void OnRootPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_host.Placing is not null)
        {
            HideGhost();
        }
    }

    private void OnRootPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!EditMode)
        {
            return;
        }

        var point = e.GetCurrentPoint(_root);
        _rightPressCancelledPlacement = false;

        if (_host.Placing is not null)
        {
            if (point.Properties.IsLeftButtonPressed)
            {
                // Dropped where the click is, not where the last move put the ghost: the two differ
                // whenever the pointer moved and clicked inside one frame.
                var (x, y) = FractionOfDip(point.Position);
                _host.CompletePlacement(this, x, y, SnapNow);
            }
            else
            {
                _host.CancelPlacement();
                _rightPressCancelledPlacement = true; // [S-1744] and opens no menu
            }

            _root.Focus(FocusState.Pointer);
            e.Handled = true;
            return;
        }

        // Clicking empty surface also gives the surface focus, or the shortcuts stop working as soon
        // as the user clicks away from a widget.
        _root.Focus(FocusState.Pointer);

        // [S-1581] A press on empty surface starts a rubber band; without Ctrl or Shift it starts
        // from nothing selected, which is also what a plain click on the desktop means.
        if (point.Properties.IsLeftButtonPressed)
        {
            BeginLasso(e, point.Position);
        }
        else
        {
            _host.Select(null);
        }
    }

    // [S-822] Through the appearance engine, so the ghost and the grid follow the theme and accent.
    private static Brush Brush(string key, Windows.UI.Color fallback) => DesignApplier.Brush(key, fallback);
}
