using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Files;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-13708] A placement's desktop icons under the pointer, drawn as the desktop draws them (owner, 2026-11-20: "when dragging a
/// desktop item from the topbar of non placed items it should already look like the ones on the desktop"), from the first
/// frame: each the desktop's own icon view (the desktop item kind's, made by its module as a card makes it) attached to the
/// icon's own config, in the box it lands in at the icon size and layout in force, with the corner marks an icon shows in edit
/// mode (<see cref="IconMarks"/>), at <see cref="SelectionLook.Landing"/> as a moving icon's ghost is where it lands
/// ([S-13046]). Their host is a preview's over the desktop's own listing (<see cref="IconPreviewHost"/>): they act on nothing.
/// Made as the placement first shows them, moved with the pointer, let go when the placement ends.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-13708] The icon views of the placement shown now, by the config each draws (one per placed icon).</summary>
    private readonly Dictionary<WidgetConfig, IconGhost> _iconGhosts = new(ReferenceEqualityComparer.Instance);

    /// <summary>[S-13708] The placed icons whose view could not be made (logged once): placed without their picture.</summary>
    private readonly HashSet<WidgetConfig> _iconGhostsFailed = new(ReferenceEqualityComparer.Instance);

    /// <summary>[S-13708] The icons' views and their marks on the ghost layer, made with the first icon placed here.</summary>
    private (Canvas Views, IconMarks Marks, IconPreviewHost Host)? _iconGhostLayer;

    /// <summary>One placed icon's view in the box it lands in.</summary>
    private sealed record IconGhost(Grid Root, FrameworkElement View);

    /// <summary>
    /// [S-13708] The desktop icons among a placement's outlines drawn where they land; the rest of the outlines are the
    /// ghost's frames (<see cref="ShowGhosts"/>). Views are kept from one pointer move to the next: only their boxes move.
    /// </summary>
    private void ShowIconGhosts(IReadOnlyList<GhostOutline> icons, ScreenMetrics metrics)
    {
        if (icons.Count == 0 && _iconGhosts.Count == 0)
        {
            return;
        }

        var (views, marks, host) = IconGhostLayer();
        var shown = new HashSet<WidgetConfig>(ReferenceEqualityComparer.Instance);
        var boxes = new List<(DipRect Box, bool Selected)>(icons.Count);
        foreach (var outline in icons)
        {
            if (outline.Config is not { } config || _iconGhostsFailed.Contains(config)
                || (_iconGhosts.TryGetValue(config, out var ghost) ? ghost : MakeIconGhost(outline.Kind, config, views, host)) is not { } made)
            {
                continue;
            }

            // On the device pixels a card lands on ([S-11873]).
            var box = GridGeometry.PaintedCell(outline.Rect, metrics, RasterScale);
            Canvas.SetLeft(made.Root, box.X);
            Canvas.SetTop(made.Root, box.Y);
            made.Root.Width = Math.Max(0, box.Width);
            made.Root.Height = Math.Max(0, box.Height);
            made.View.Width = made.Root.Width;
            made.View.Height = made.Root.Height;
            made.Root.Visibility = Visibility.Visible;
            shown.Add(config);
            boxes.Add((box, false));
        }

        foreach (var (config, ghost) in _iconGhosts)
        {
            if (!shown.Contains(config))
            {
                ghost.Root.Visibility = Visibility.Collapsed;
            }
        }

        marks.Draw(boxes, RasterScale);
    }

    /// <summary>[S-13708] The icon's view as the desktop makes it, attached to its config; null (logged) when it cannot be drawn.</summary>
    private IconGhost? MakeIconGhost(WidgetId kind, WidgetConfig config, Canvas views, IconPreviewHost host)
    {
        FrameworkElement view;
        try
        {
            view = ModuleHost.CreateView(kind, host);
            if (view is IWidgetView widget)
            {
                widget.Attach(Ticker, config);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a desktop icon being placed could not be drawn as on the desktop; it is placed without its picture", ex);
            _iconGhostsFailed.Add(config);
            return null;
        }

        // In the language's flow, as a card is ([S-1113]).
        var root = new Grid { IsHitTestVisible = false };
        UiLanguage.ApplyFlow(root);
        root.Children.Add(view);
        views.Children.Add(root);
        var ghost = new IconGhost(root, view);
        _iconGhosts[config] = ghost;
        return ghost;
    }

    /// <summary>[S-13708] The ghost layer's plates for placed icons: their views, then the marks' halos and bars, each at the landing's strength.</summary>
    private (Canvas Views, IconMarks Marks, IconPreviewHost Host) IconGhostLayer()
    {
        if (_iconGhostLayer is { } made)
        {
            return made;
        }

        var views = new Canvas { IsHitTestVisible = false, Opacity = SelectionLook.Landing };
        // Left to right in every language, as the surface's marker layer: the boxes are the surface's own.
        var marks = new IconMarks();
        UiLanguage.KeepLeftToRight(marks.Halos);
        UiLanguage.KeepLeftToRight(marks.Inks);
        marks.Halos.Opacity = SelectionLook.Landing;
        marks.Inks.Opacity = SelectionLook.Landing;
        _ghostLayer.Children.Add(views);
        _ghostLayer.Children.Add(marks.Halos);
        _ghostLayer.Children.Add(marks.Inks);
        var host = new IconPreviewHost(new CardWidgetHost(new PreviewCardHost(Ticker)), HostDesktopItems.Instance);
        _iconGhostLayer = (views, marks, host);
        return _iconGhostLayer.Value;
    }

    /// <summary>[S-13708] The placement left this display for now: its icons go with the frames.</summary>
    private void HideIconGhosts()
    {
        if (_iconGhostLayer is not { } layer)
        {
            return;
        }

        foreach (var ghost in _iconGhosts.Values)
        {
            ghost.Root.Visibility = Visibility.Collapsed;
        }

        layer.Marks.Draw([], RasterScale);
    }

    /// <summary>[S-13708] The placement is over (placed, given up, or the surface closing): its icons let go of the desktop and go.</summary>
    private void DropIconGhosts()
    {
        foreach (var ghost in _iconGhosts.Values)
        {
            try
            {
                (ghost.View as IWidgetView)?.Detach();
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, "a placed desktop icon's picture could not let go", ex);
            }

            (ghost.Root.Parent as Panel)?.Children.Remove(ghost.Root);
        }

        _iconGhosts.Clear();
        _iconGhostsFailed.Clear();
        _iconGhostLayer?.Marks.Draw([], RasterScale);
    }

    /// <summary>[S-13708] How many placed icons this display draws as the desktop does now (a check's question).</summary>
    internal int IconGhostsShown => _iconGhosts.Values.Count(g => g.Root.Visibility == Visibility.Visible);
}
