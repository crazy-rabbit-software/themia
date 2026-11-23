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
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Windows.Foundation;
using Windows.Graphics;

namespace Themia.App.Shell;

/// <summary>Adding a widget: the picker, the ghost and the click that places it.</summary>
internal sealed partial class DesktopWindow
{
    /// <summary>
    /// [S-13018] [S-16728] Add widget here, from a lassoed area's menu: edit mode on, the widget gallery opened; a widget
    /// placed from it with Enter is laid into <paramref name="area"/> at once (no ghost to aim); a drag lands where it is let go.
    /// </summary>
    internal void OpenAddPageFor(FracRect area)
    {
        if (_closed)
        {
            return;
        }

        if (!EditMode)
        {
            _host.SetEditMode(true);
        }

        Step(() =>
        {
            OpenWidgetGallery();
            if (_widgetGallery is not null)
            {
                _galleryArea = area;
            }
        });
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
            MinHeight = EditBar.BarHeight,
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
            // The fill is the stroke's colour as the family's wash: the same accent, so it reads as one
            // shape rather than a box inside a box.
            Frame = new Rectangle
            {
                Fill = HexBrush.Wash(ColourRole.Accent),
                Stroke = accent,
                StrokeThickness = FamilyShape.Stroke,
                StrokeDashArray = [4, 3],
            };

            Icon = new FontIcon { FontSize = FamilyShape.IconLarge, HorizontalAlignment = HorizontalAlignment.Center };
            Label = Fluent.Text(FamilyType.BodyStrong);
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
    /// is there. Outlines are pooled: a ghost redraws on every pointer move. [S-13708] A desktop icon
    /// is drawn as the desktop draws it instead (<see cref="ShowIconGhosts"/>).
    /// </summary>
    public void ShowGhosts(IReadOnlyList<GhostOutline> outlines)
    {
        ArgumentNullException.ThrowIfNull(outlines);

        var metrics = Metrics;
        if (_closed || !metrics.IsUsable)
        {
            return;
        }

        ShowIconGhosts([.. outlines.Where(o => o.Config is IDesktopItemConfig)], metrics);
        outlines = [.. outlines.Where(o => o.Config is not IDesktopItemConfig)];
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

            var box = GridGeometry.PaintedCell(outlines[i].Rect, metrics, RasterScale);
            // [S-16684] A preset's ghost wears its preset's glyph and name.
            var definition = outlines[i].Config is { } config ? WidgetPick.Of(config) : new WidgetPick(outlines[i].Kind);

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

        HideIconGhosts();
    }

    /// <summary>
    /// [S-11807] [S-11809] What a placement's new widgets are sized with on this display: its content
    /// scale for a widget of its own scale 1, in whole grid cells ([S-06]: always).
    /// </summary>
    internal PlacementSizes SizesNow
        => new(WidgetScale.Effective(_host.State.MonitorScales.TryGetValue(_monitor.Id, out var own) ? own : 1.0, 1.0));

    /// <summary>What a payload's ghost draws with the pointer at a physical screen point of this display.</summary>
    public IReadOnlyList<GhostOutline> OutlinesAt(Win32.Point cursor, PlacementPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var (x, y) = FractionOf(cursor);
        return payload.Outlines(x, y, Metrics, SizesNow);
    }

    /// <summary>What landing a payload with the pointer at a physical screen point of this display does.</summary>
    public PastePlan PlanAt(Win32.Point cursor, PlacementPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var (x, y) = FractionOf(cursor);
        return payload.Plan(x, y, Metrics, SizesNow);
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
    /// one, and the edit bar steps aside for a line saying what the click will do. The originals of a
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

        // [S-2460] No frame while a widget is placed.
        SyncFrame();

        if (placing is not null)
        {
            EndBand(null);
            _placementHintText.Text = Str.Shell_Place_Hint.Format(("action", PlacementHint.Verb(placing)));
            _placementHint.Visibility = Visibility.Visible;
            _editBar.Visibility = Visibility.Collapsed;
            CloseFlyout(focusEntry: false);
            PlaceFlyout();

            // The command was given with the pointer already somewhere: show the ghost there at once
            // rather than waiting for the first move.
            if (IsUnderCursor && Win32.GetCursorPos(out var cursor))
            {
                // [S-14916] Off the gallery its own cards are the ghost.
                var outlines = OutlinesAt(cursor, placing);
                if (!FollowGalleryDrag(outlines))
                {
                    ShowGhosts(outlines);
                }
            }
            else
            {
                HideGhost();
            }

            return;
        }

        _placementHint.Visibility = Visibility.Collapsed;
        _editBar.Visibility = EditMode ? Visibility.Visible : Visibility.Collapsed;
        PlaceFlyout();
        HideGhost();
        DropIconGhosts();
        GalleryAfterPlacing();
    }

    /// <summary>Where a point in this surface's own coordinates falls, as a fraction of it.</summary>
    private (double X, double Y) FractionOfDip(Point dip)
    {
        var metrics = Metrics;
        return metrics.IsUsable
            ? (Math.Clamp(dip.X / metrics.Width, 0, 1), Math.Clamp(dip.Y / metrics.Height, 0, 1))
            : (0.5, 0.5);
    }

    private void OnRootPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(_root).Position;

        if (_host.Placing is { } payload)
        {
            var (x, y) = FractionOfDip(position);
            var outlines = payload.Outlines(x, y, Metrics, SizesNow);
            if (!FollowGalleryDrag(outlines))
            {
                ShowGhosts(outlines);
            }
            return;
        }

        UpdateBand(position);
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
            OnDesktopPressed(e);
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
                _host.CompletePlacement(this, x, y);
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
