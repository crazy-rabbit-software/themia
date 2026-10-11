using Microsoft.UI.Xaml;
using Themia.App.Shell.Motion;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Motion;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-1000..S-1013] The surface's intros: the startup intro on its first real layout, the view-change
/// intro when the display switches views, and the replays (the settings preview, the session unlock).
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-1001] Which layout passes are armed. Per surface: each display plays its own.</summary>
    private readonly IntroArming _arming = new();

    /// <summary>The intro playing on this surface, or null.</summary>
    private MotionShow? _show;

    /// <summary>[S-1005] While the hyperspace overlay plays, the hit region spans the work area so the veil is seen edge to edge.</summary>
    private bool MotionCoversScreen => _show?.Overlay is not null;

    /// <summary>
    /// [S-1001] Called at the end of every layout pass. Spends the arm only on a pass the user can see
    /// — the surface is loaded and has a size — so the startup intro is not played to nobody.
    /// </summary>
    private void PlayArmedIntro(ViewState view)
    {
        if (_closed || _suspended || !_root.IsLoaded || !Metrics.IsUsable)
        {
            return;
        }

        if (_arming.OnLayout(view.Id) is { } mode)
        {
            Play(mode, view);
        }
    }

    /// <summary>
    /// [S-1011]/[S-1014] Replay <paramref name="mode"/>'s intro on the cards already here — the settings
    /// preview, or the session unlock. [S-1012] Refused while an intro is still playing, so a burst of
    /// slider changes does not restart the show on every step.
    /// </summary>
    public bool ReplayIntro(IntroMode mode)
    {
        if (_closed || _suspended || !_root.IsLoaded || !Metrics.IsUsable)
        {
            return false;
        }

        if (_show is not null)
        {
            Log.Debug(Scope, $"{mode} replay ignored on {_monitor.GdiName}: an intro is still playing");
            return false;
        }

        return Play(mode, ViewForThisMonitor(_host.State));
    }

    private bool Play(IntroMode mode, ViewState view)
    {
        var style = IntroMotion.StyleFor(Design, mode);
        if (!IntroMotion.Plays(style, SystemMotion.AnimationsEnabled))
        {
            return false;
        }

        // [S-1012] A view change replaces whatever was still playing: the old cards are gone already.
        _show?.Stop();

        // [S-2106] The intro moves every card on its own, so each paints its own card while it plays.
        _groups.Apply(_cards, Metrics, Design, enabled: false);

        try
        {
            _show = MotionShow.Start(
                new MotionStage(_widgetLayer, Metrics.Width, Metrics.Height, Actors(view), Design.WidgetGapPx,
                    DesignLook.CornerRadius(Design.CornerRadiusPx)),
                mode,
                style,
                IntroMotion.MsFor(Design, mode),
                MountOverlay,
                OnShowEnded);
        }
        catch (Exception ex)
        {
            // Cosmetic: a failed intro leaves the widgets at rest, which is where they belong anyway.
            Log.Error(Scope, $"the {mode} intro failed to start on {_monitor.GdiName}", ex);
            _show?.Stop();
            _show = null;
            ApplyGroups();
            ApplyHitRegion();
            return false;
        }

        ApplyHitRegion();

        return true;
    }

    /// <summary>
    /// [S-1002] The cards, in the view's widget order — the cascade's order.
    /// </summary>
    private List<MotionActor> Actors(ViewState view)
    {
        var actors = new List<MotionActor>(_cards.Count);
        foreach (var model in view.Widgets)
        {
            if (_cards.Find(c => ReferenceEquals(c.Model, model)) is { Visibility: Visibility.Visible } card)
            {
                actors.Add(new MotionActor(card, model.Rect));
            }
        }

        return actors;
    }

    /// <summary>[S-1005] The overlay goes under the widgets: they crash into the black, not behind it.</summary>
    private void MountOverlay(FrameworkElement overlay) => _root.Children.Insert(0, overlay);

    private void OnShowEnded(MotionShow show)
    {
        if (show.Overlay is { } overlay)
        {
            _root.Children.Remove(overlay.Element);
        }

        if (!ReferenceEquals(_show, show))
        {
            return;
        }

        _show = null;

        // The region shrinks back to the widgets once the overlay is gone, and [S-2106] the cards
        // at rest join their groups again.
        if (!_closed)
        {
            ApplyGroups();
            ApplyHitRegion();
        }
    }
}
