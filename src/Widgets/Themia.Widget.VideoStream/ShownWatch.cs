using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;

namespace Themia.App.Widgets;

/// <summary>
/// Whether a view can actually be seen: in the tree, and neither it nor anything above it collapsed.
/// <para>
/// Whatever hides a widget collapses an element above the view (its card, the layer), not the view
/// itself, so a view that wants to stop working while hidden has to watch its ancestors. Most views
/// do not care — a hidden clock costs nothing — but a video stream holds a socket and decodes every
/// frame, so for it "not visible" has to mean "not running".
/// </para>
/// <para>
/// The ancestor chain is captured on <c>Loaded</c> and released on <c>Unloaded</c> and on
/// <see cref="Dispose"/>: a card can be re-parented, and a callback left registered on the old
/// parent would keep both alive and report a visibility that no longer applies.
/// </para>
/// </summary>
internal sealed class ShownWatch : IDisposable
{
    private readonly FrameworkElement _element;
    private readonly Action<bool> _changed;
    private readonly List<(UIElement Element, long Token)> _watched = [];
    private bool _loaded;
    private bool _disposed;

    /// <param name="element">The view to watch.</param>
    /// <param name="changed">Called on the UI thread with the new value, only when it changes.</param>
    public ShownWatch(FrameworkElement element, Action<bool> changed)
    {
        _element = element;
        _changed = changed;
        _element.Loaded += OnLoaded;
        _element.Unloaded += OnUnloaded;

        if (_element.IsLoaded)
        {
            OnLoaded(_element, new RoutedEventArgs());
        }
    }

    /// <summary>The last value reported.</summary>
    public bool IsShown { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _loaded = true;
        Unwatch();

        for (DependencyObject? node = _element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is UIElement ui)
            {
                var token = ui.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, OnVisibilityChanged);
                _watched.Add((ui, token));
            }
        }

        Evaluate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // WinUI can raise a re-parented element's Unloaded after the Loaded for its new place. An
        // element that is loaded again by the time this arrives has not left the tree.
        if (_element.IsLoaded)
        {
            return;
        }

        _loaded = false;
        Unwatch();
        Evaluate();
    }

    private void OnVisibilityChanged(DependencyObject sender, DependencyProperty property) => Evaluate();

    private void Evaluate()
    {
        if (_disposed)
        {
            return;
        }

        var shown = _loaded;

        foreach (var (element, _) in _watched)
        {
            if (element.Visibility != Visibility.Visible)
            {
                shown = false;
                break;
            }
        }

        if (shown == IsShown)
        {
            return;
        }

        IsShown = shown;
        _changed(shown);
    }

    private void Unwatch()
    {
        foreach (var (element, token) in _watched)
        {
            element.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, token);
        }

        _watched.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _element.Loaded -= OnLoaded;
        _element.Unloaded -= OnUnloaded;
        Unwatch();
        IsShown = false;
    }
}
