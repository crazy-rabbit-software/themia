using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Shell;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-880]..[S-883] The notice host: transient messages stacked at the bottom right of this
/// display's work area, above the taskbar, as Windows 11 places its own notifications.
/// <para>
/// Mounted only while something is showing, so an idle surface carries no extra elements. The
/// surface stays non-activating: a notice never takes focus or raises the window, and its buttons
/// refuse focus on click. Each notice leaves after <see cref="NoticeStack.Lifetime"/>, a timer that
/// waits while the pointer rests on it. The stack's rules (how many, which goes) live in Core
/// <see cref="NoticeStack"/>; this file only draws them and keeps the hit region round them —
/// outside edit mode the region is all that is painted and clickable ([S-872]).
/// </para>
/// </summary>
internal sealed partial class DesktopWindow
{
    private readonly NoticeStack _notices = new();
    private readonly Dictionary<long, (NoticeCard Card, DispatcherQueueTimer? Timer)> _noticeCards = [];
    private StackPanel? _noticeLayer;
    private bool _noticeResizeHooked;

    /// <summary>[S-880] Show a notice on this display.</summary>
    public void ShowNotice(Notice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);

        if (_closed)
        {
            return;
        }

        foreach (var gone in _notices.Push(notice))
        {
            TakeDown(gone.Id);
        }

        var layer = MountNotices();
        var card = new NoticeCard(notice, c => DismissNotice(c.Notice.Id));
        layer.Children.Add(card);

        DispatcherQueueTimer? timer = null;
        if (NoticeStack.Lifetime(notice) is { } lifetime)
        {
            timer = _window.DispatcherQueue.CreateTimer();
            timer.Interval = lifetime;
            timer.IsRepeating = false;
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (!_closed)
                {
                    DismissNotice(notice.Id);
                }
            };
            timer.Start();

            // [S-883] Never vanish under a hand on its way to the button; the full time again after.
            card.PointerEntered += (_, _) => timer.Stop();
            card.PointerExited += (_, _) => timer.Start();
        }

        _noticeCards[notice.Id] = (card, timer);
        PlaceNotices();
    }

    /// <summary>Take a notice down: timed out, closed, or acted on.</summary>
    public void DismissNotice(long id)
    {
        if (_notices.Dismiss(id))
        {
            TakeDown(id);
        }
    }

    private void TakeDown(long id)
    {
        if (!_noticeCards.Remove(id, out var shown))
        {
            return;
        }

        shown.Timer?.Stop();
        _noticeLayer?.Children.Remove(shown.Card);

        if (_noticeCards.Count == 0 && _noticeLayer is { } layer)
        {
            // Unmounted when empty: an idle surface holds nothing for notices.
            _root.Children.Remove(layer);
            _noticeLayer = null;
            ApplyHitRegion();
        }
    }

    private StackPanel MountNotices()
    {
        if (_noticeLayer is { } existing)
        {
            return existing;
        }

        var layer = new StackPanel
        {
            Spacing = Fluent.Space2,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,

            // Windows' own motion: a new notice fades in where it lands and the rest make room. Not a
            // slide from the edge: outside edit mode the hit region is the stack's final box, and it
            // clips painting, so a card sliding in from outside it would appear to wipe in.
            ChildrenTransitions =
            {
                new EntranceThemeTransition { FromHorizontalOffset = 0, FromVerticalOffset = 0 },
                new RepositionThemeTransition(),
            },
        };

        // Its real size arrives after layout: that, not the moment of adding, is when the hit region can follow it.
        layer.SizeChanged += (_, _) => ApplyHitRegion();

        // Last, so notices sit over the widgets, the view bar and the toolbar.
        UiLanguage.ApplyFlow(layer);
        _root.Children.Add(layer);
        _noticeLayer = layer;

        if (!_noticeResizeHooked)
        {
            _noticeResizeHooked = true;
            _root.SizeChanged += (_, _) => PlaceNotices();
        }

        return layer;
    }

    /// <summary>Inside the work area's bottom-right corner, a spacing step in from its edges.</summary>
    private void PlaceNotices()
    {
        if (_noticeLayer is not { } layer)
        {
            return;
        }

        var metrics = Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var allowed = GridGeometry.AllowedPixels(metrics);
        layer.Margin = new Thickness(
            0,
            0,
            Math.Max(0, metrics.Width - (allowed.X + allowed.Width)) + Fluent.Space4,
            Math.Max(0, metrics.Height - (allowed.Y + allowed.Height)) + Fluent.Space4);

        // A notice is never wider than the screen it is on, less its margins.
        var room = Math.Max(0, allowed.Width - (Fluent.Space4 * 2));
        foreach (var card in layer.Children.OfType<NoticeCard>())
        {
            card.Width = Math.Min(NoticeCard.CardWidth, room);
        }

        // A new margin moves the box without resizing it, so SizeChanged alone would miss it.
        ApplyHitRegion();
    }

    /// <summary>[S-880] The notices' box in the surface's DIPs, for the hit region; null when none is showing.</summary>
    private PixelRect? NoticeBox()
    {
        if (_noticeLayer is not { } layer || layer.ActualWidth <= 0 || layer.ActualHeight <= 0)
        {
            return null;
        }

        var origin = layer.TransformToVisual(_root).TransformPoint(new Point(0, 0));
        return new PixelRect(origin.X, origin.Y, layer.ActualWidth, layer.ActualHeight);
    }

    // -----------------------------------------------------------------------------------------
    // [S-890] Safe mode
    // -----------------------------------------------------------------------------------------

    /// <summary>Start every card safe mode paused on this display.</summary>
    public void StartPausedCards()
    {
        foreach (var card in _cards.Where(c => c.IsPaused).ToList())
        {
            card.Retry();
        }
    }

    /// <summary>Run <paramref name="then"/> once this display's root has loaded and laid out its cards.</summary>
    internal void WhenLaidOut(Action then)
    {
        ArgumentNullException.ThrowIfNull(then);

        // A surface held hidden (game mode, [S-928]) builds no cards and may never load: there is
        // nothing on it that could take the start down.
        if (_root.IsLoaded || _suspended)
        {
            then();
            return;
        }

        void OnLoaded(object sender, RoutedEventArgs args)
        {
            _root.Loaded -= OnLoaded;
            then();
        }

        _root.Loaded += OnLoaded;
    }
}
