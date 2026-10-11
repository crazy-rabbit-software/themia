using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Diagnostics;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Windows.System;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1977]/[S-1978] The settings window's keyboard: Ctrl+F / Ctrl+E to "Find a setting", F6 between
/// the title bar, the navigation and the page, Ctrl+W to close, Escape to empty the search box, and
/// Home / End / type-ahead in the navigation. Arrows, Enter and Space in the navigation are
/// <c>NavigationView</c>'s own; Alt+Left / Alt+Right are <see cref="AttachHistoryKeys"/>.
/// </summary>
internal sealed partial class ConfigWindow
{
    private readonly TypeAhead _typeAhead = new();

    private void AttachWindowKeys(UIElement root)
    {
        // Tunnelling, so a focused control that would swallow F6 or Ctrl+F never sees them.
        root.PreviewKeyDown += OnWindowPreviewKeyDown;
        _nav.PreviewKeyDown += OnNavPreviewKeyDown;
        _nav.CharacterReceived += OnNavCharacterReceived;
        _search.PreviewKeyDown += OnSearchPreviewKeyDown;
    }

    private static bool IsDown(VirtualKey key)
        => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private void OnWindowPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        var key = SettingsKeys.Decide((int)args.Key, IsDown(VirtualKey.Shift), IsDown(VirtualKey.Control), IsDown(VirtualKey.Menu));
        switch (key)
        {
            case SettingsKey.FocusSearch:
                _search.Focus(FocusState.Keyboard);
                break;

            case SettingsKey.NextPane:
            case SettingsKey.PreviousPane:
                FocusPane(SettingsKeys.Next(CurrentPane(), key == SettingsKey.PreviousPane, CanFocus));
                break;

            case SettingsKey.Close:
                Close();
                break;

            default:
                return;
        }

        args.Handled = true;
    }

    /// <summary>[S-1977] The pane the focus is in; the title bar when it is in none of them.</summary>
    private SettingsPane CurrentPane()
    {
        var focused = _window.Content?.XamlRoot is { } xamlRoot ? FocusManager.GetFocusedElement(xamlRoot) as DependencyObject : null;
        for (var node = focused; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, _bodyHost) || ReferenceEquals(node, _crumbs))
            {
                return SettingsPane.Page;
            }

            if (ReferenceEquals(node, _nav))
            {
                return SettingsPane.Navigation;
            }
        }

        return SettingsPane.Search;
    }

    /// <summary>The navigation folded away in a narrow window has nothing to focus until it is opened.</summary>
    private bool CanFocus(SettingsPane pane) => pane switch
    {
        SettingsPane.Navigation => _nav.DisplayMode == NavigationViewDisplayMode.Expanded || _nav.IsPaneOpen,
        SettingsPane.Page => FocusManager.FindFirstFocusableElement(_pageColumn) is not null,
        _ => true,
    };

    private void FocusPane(SettingsPane pane)
    {
        switch (pane)
        {
            case SettingsPane.Navigation:
                // The page's own entry, as Settings does: F6 lands where the reader is.
                (_nav.SelectedItem as Control ?? Entries().FirstOrDefault())?.Focus(FocusState.Keyboard);
                break;

            case SettingsPane.Page:
                if (FocusManager.FindFirstFocusableElement(_pageColumn) is Control first)
                {
                    first.Focus(FocusState.Keyboard);
                }

                break;

            default:
                _search.Focus(FocusState.Keyboard);
                break;
        }
    }

    /// <summary>[S-1978] Home and End in the navigation: the first and the last entry.</summary>
    private void OnNavPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is not (VirtualKey.Home or VirtualKey.End) || FocusedEntry() is null)
        {
            return;
        }

        var entries = Entries().ToList();
        if (entries.Count == 0)
        {
            return;
        }

        (args.Key == VirtualKey.Home ? entries[0] : entries[^1]).Focus(FocusState.Keyboard);
        _typeAhead.Reset();
        args.Handled = true;
    }

    /// <summary>
    /// [S-1978] Letters typed on a navigation entry move the focus to the entry they spell. Read from
    /// the character, not the key, so every keyboard layout and dead key works. Enter then opens it.
    /// </summary>
    private void OnNavCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        try
        {
            var c = args.Character;
            if (FocusedEntry() is not { } focused || char.IsControl(c))
            {
                return;
            }

            var entries = Entries().ToList();
            var labels = entries.Select(e => e.Content as string ?? string.Empty).ToList();
            if (_typeAhead.Type(c, Environment.TickCount64, labels, entries.IndexOf(focused)) is { } at)
            {
                entries[at].Focus(FocusState.Keyboard);
                args.Handled = true;
            }
            else
            {
                // A lone Space stays NavigationView's (it selects); a letter that matches nothing is still eaten.
                args.Handled = !char.IsWhiteSpace(c);
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "type-ahead in the navigation failed", ex);
        }
    }

    private NavigationViewItem? FocusedEntry()
        => _window.Content?.XamlRoot is { } xamlRoot && FocusManager.GetFocusedElement(xamlRoot) is NavigationViewItem item && Entries().Contains(item)
            ? item
            : null;

    /// <summary>
    /// [S-1977] Escape in "Find a setting": the open list closes first (the box's own), then the text
    /// empties. An empty box leaves Escape alone.
    /// </summary>
    private void OnSearchPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Escape && !_search.IsSuggestionListOpen && !string.IsNullOrEmpty(_search.Text))
        {
            _search.Text = string.Empty;
            args.Handled = true;
        }
    }
}
