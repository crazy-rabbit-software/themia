using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-16591] The days past what the card shows, scrolled to by whole days (owner, 2026-11-23: "the weather forecast lists
/// are not scrollable although they should be"): every fetched day is laid at its one fixed size in a scroller with no bar
/// (rows and the grid's lines down, columns across), the wheel and the touchpad turning it a day a notch, the keys a day,
/// a page, the first or the last; the rows fade at the foot while more are below, as the kit's lists do (ScrollFade).
/// Where the steps and the limits fall is <see cref="ForecastScroll"/>'s.
/// </summary>
internal sealed partial class WeatherForecastView : IWidgetKeys
{
    /// <summary>The scroller on screen: across or down, its days' one length, how many.</summary>
    private sealed record Scroller(ScrollViewer Viewer, bool Across, double Item, int Count);

    private Scroller? _scroller;
    private ScrollFade? _fade;

    // The first day the scroller stands at, and what the wheel turned short of a notch.
    private int _first;
    private int _carry;

    /// <summary>
    /// Lay <paramref name="content"/> (its margin the scroller's) in a scroller of <paramref name="count"/> days
    /// <paramref name="item"/> long, across or down; the rows of <paramref name="faded"/> fade at its foot.
    /// </summary>
    private ScrollViewer Scrolled(FrameworkElement content, bool across, double item, int count, Panel? faded)
    {
        var viewer = new ScrollViewer
        {
            Content = content,
            Margin = content.Margin,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = across ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = across ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Hidden,
            ZoomMode = ZoomMode.Disabled,
            IsTabStop = false,
        };
        content.Margin = new Thickness(0);
        viewer.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnWheel), true);

        // The first layout gives the viewport: the scroller goes back to the day it stood at.
        viewer.SizeChanged += (_, _) => GoTo();
        _scroller = new Scroller(viewer, across, item, count);
        if (faded is not null)
        {
            _fade = new ScrollFade(viewer, faded);
            _fade.Refresh();
        }

        return viewer;
    }

    /// <summary>Let the scroller go (a rebuild or a detach): its fade unhooked.</summary>
    private void DropScroller()
    {
        _fade?.Dispose();
        _fade = null;
        _scroller = null;
    }

    private static double Viewport(Scroller scroller) => scroller.Across ? scroller.Viewer.ViewportWidth : scroller.Viewer.ViewportHeight;

    /// <summary>Stand the scroller at <see cref="_first"/>, kept in its limits.</summary>
    private void GoTo()
    {
        if (_scroller is not { } s || !(Viewport(s) > 0))
        {
            return;
        }

        var viewport = Viewport(s);
        _first = ForecastScroll.Step(_first, 0, s.Count, s.Item, viewport);
        var offset = ForecastScroll.Offset(_first, s.Count, s.Item, viewport);
        s.Viewer.ChangeView(s.Across ? offset : null, s.Across ? null : offset, null, disableAnimation: true);
    }

    /// <summary>A wheel or touchpad turn: a day a notch, the rest carried; a forecast that shows every day lets it go on.</summary>
    private void OnWheel(object sender, PointerRoutedEventArgs args)
    {
        if (_scroller is not { } s || ForecastScroll.LastFirst(s.Count, s.Item, Viewport(s)) == 0)
        {
            return;
        }

        var properties = args.GetCurrentPoint(s.Viewer).Properties;

        // A tilt to the right reads as a turn down: the next days, as the columns run.
        var delta = properties.IsHorizontalMouseWheel ? -properties.MouseWheelDelta : properties.MouseWheelDelta;
        (_first, _carry) = ForecastScroll.Wheel(_first, _carry, delta, s.Count, s.Item, Viewport(s));
        GoTo();
        args.Handled = true;
    }

    /// <summary>[S-16591] A click takes the keyboard only where there are days to scroll to.</summary>
    public bool WantsKeys => _scroller is { } s && ForecastScroll.LastFirst(s.Count, s.Item, Viewport(s)) > 0;

    /// <summary>[S-16591] A day on (Down, or Right across), a day back, a page (the whole days shown), the first, the last.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_scroller is not { } s || !(Viewport(s) > 0))
        {
            return false;
        }

        var viewport = Viewport(s);
        var page = ForecastScroll.Page(s.Item, viewport);
        var last = ForecastScroll.LastFirst(s.Count, s.Item, viewport);
        int? next = key switch
        {
            WidgetKey.Down when !s.Across => _first + 1,
            WidgetKey.Up when !s.Across => _first - 1,
            WidgetKey.Right when s.Across => _first + 1,
            WidgetKey.Left when s.Across => _first - 1,
            WidgetKey.PageDown => _first + page,
            WidgetKey.PageUp => _first - page,
            WidgetKey.Home => 0,
            WidgetKey.End => last,
            _ => null,
        };

        if (next is not { } to)
        {
            return false;
        }

        _first = ForecastScroll.Step(to, 0, s.Count, s.Item, viewport);
        _carry = 0;
        GoTo();
        return true;
    }
}
