using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-13950] Where an input stands in for some words: their panel, their place in it, their grid cell and span, their
/// margin and alignment, the words collapsed while it stands there (a collapsed text takes no room, the input takes exactly
/// theirs). The one rule for the inline box (<see cref="InlineBox"/>) and the search on a tool line (<c>SearchBox</c> on its
/// line). Words whose panel is not known yet (a row built but not yet in the live tree) are stood in for once they load.
/// </summary>
internal sealed class WordsPlace(FrameworkElement stand, FrameworkElement words)
{
    /// <summary>The panel the input stands in, while it does.</summary>
    private Panel? _panel;

    /// <summary>Asked to stand in before the words stood in a panel of the live tree: it does once they load.</summary>
    private bool _waiting;

    /// <summary>The words stood in for.</summary>
    public FrameworkElement Words => words;

    /// <summary>Whether the input stands in for the words now.</summary>
    public bool Standing => _panel is not null;

    /// <summary>Stand in for the words (or, their panel not known yet, as soon as they load).</summary>
    public void In()
    {
        if (_panel is not null)
        {
            return;
        }

        if ((words.Parent ?? VisualTreeHelper.GetParent(words)) is not Panel panel)
        {
            if (!_waiting)
            {
                _waiting = true;
                words.Loaded += OnWordsLoaded;
            }

            return;
        }

        Grid.SetRow(stand, Grid.GetRow(words));
        Grid.SetColumn(stand, Grid.GetColumn(words));
        Grid.SetRowSpan(stand, Grid.GetRowSpan(words));
        Grid.SetColumnSpan(stand, Grid.GetColumnSpan(words));
        stand.Margin = words.Margin;

        // Words as wide as their text (a file's name) still give the input their whole room to type in; centred words keep the centre.
        stand.HorizontalAlignment = words.HorizontalAlignment == HorizontalAlignment.Center ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        stand.VerticalAlignment = words.VerticalAlignment;
        if (!double.IsNaN(words.Width))
        {
            stand.Width = words.Width;
        }

        stand.MaxWidth = words.MaxWidth;
        panel.Children.Insert(panel.Children.IndexOf(words), stand);
        words.Visibility = Visibility.Collapsed;
        _panel = panel;
    }

    /// <summary>Step out: the input leaves the panel and the words come back where they stood.</summary>
    public void Out()
    {
        StopWaiting();
        if (_panel is { } panel)
        {
            panel.Children.Remove(stand);
            _panel = null;
        }

        words.Visibility = Visibility.Visible;
    }

    private void OnWordsLoaded(object sender, RoutedEventArgs args)
    {
        StopWaiting();
        In();
    }

    private void StopWaiting()
    {
        if (_waiting)
        {
            _waiting = false;
            words.Loaded -= OnWordsLoaded;
        }
    }
}
