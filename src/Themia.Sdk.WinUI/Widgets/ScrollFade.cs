using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1281] The bottom fade of a widget's scrolling list, for any list that is a panel of rows inside
/// a <see cref="ScrollViewer"/> (Mail, OneDrive; the file explorer and Contacts can take it too).
/// The rows near the viewport's bottom edge fade while there is more below; scrolled to the end,
/// every row is whole again. How much each row fades is <see cref="ListFade"/>'s.
/// <para>
/// Event-driven only: it re-fades when the list scrolls, when the list or the viewport changes
/// size, and — after the view rebuilt its rows — once on the next layout pass, when the new rows
/// have positions. No timer. The view calls <see cref="Refresh"/> after it rebuilds the rows and
/// disposes the fade on <c>Detach</c>.
/// </para>
/// </summary>
internal sealed class ScrollFade : IDisposable
{
    private readonly ScrollViewer _scroll;
    private readonly Panel _list;
    private bool _pending;
    private bool _disposed;

    public ScrollFade(ScrollViewer scroll, Panel list)
    {
        _scroll = scroll;
        _list = list;
        _scroll.ViewChanged += OnViewChanged;
        _scroll.SizeChanged += OnSizeChanged;
        _list.SizeChanged += OnSizeChanged;
    }

    /// <summary>Fade again once the rows the view just built have been laid out.</summary>
    public void Refresh()
    {
        if (_disposed || _pending)
        {
            return;
        }

        _pending = true;
        _list.LayoutUpdated += OnLayoutUpdated;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scroll.ViewChanged -= OnViewChanged;
        _scroll.SizeChanged -= OnSizeChanged;
        _list.SizeChanged -= OnSizeChanged;

        if (_pending)
        {
            _list.LayoutUpdated -= OnLayoutUpdated;
            _pending = false;
        }
    }

    private void OnViewChanged(object? sender, ScrollViewerViewChangedEventArgs args) => Apply();

    private void OnSizeChanged(object sender, SizeChangedEventArgs args) => Apply();

    private void OnLayoutUpdated(object? sender, object args)
    {
        _list.LayoutUpdated -= OnLayoutUpdated;
        _pending = false;
        Apply();
    }

    private void Apply()
    {
        if (_disposed)
        {
            return;
        }

        // In the list's own coordinates: the viewport's bottom is how far it has scrolled plus how
        // tall it is, less the padding above the list.
        var viewBottom = _scroll.VerticalOffset + _scroll.ViewportHeight - _scroll.Padding.Top;
        var more = _scroll.Visibility == Visibility.Visible && ListFade.MoreBelow(viewBottom, _list.ActualHeight);

        foreach (var child in _list.Children)
        {
            var top = child.ActualOffset.Y;
            child.Opacity = ListFade.Opacity(top, top + child.ActualSize.Y, viewBottom, more);
        }
    }
}
