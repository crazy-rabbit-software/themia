using CommunityToolkit.WinUI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Settings;

namespace Themia.App.Shell.Config;

/// <summary>
/// "Find a setting", centred in the title bar as in Windows 11 Settings. [S-1542..S-1546]
/// <para>
/// Typing lists the best matches across every page (each with its icon and where it lives);
/// choosing one opens its page, scrolls the row into view and rings it in the accent for a moment,
/// the way Settings lands on a search result. Enter takes the best match.
/// </para>
/// </summary>
internal sealed partial class ConfigWindow
{
    /// <summary>How long a landed-on row keeps its ring. Cosmetic only; nothing waits on it.</summary>
    private static readonly TimeSpan HighlightTime = TimeSpan.FromMilliseconds(1800);

    private IReadOnlyList<SettingHit> _hits = [];

    private void BuildSearch()
    {
        _search.QueryIcon = new FontIcon { Glyph = Glyphs.Search, FontSize = 12 };
        _search.VerticalAlignment = VerticalAlignment.Center;
        _search.HorizontalAlignment = HorizontalAlignment.Stretch;
        _search.Margin = new Thickness(Fluent.Space4, 0, Fluent.Space4, 0);
        _search.UpdateTextOnSelect = false;

        _search.TextChanged += (sender, args) =>
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                Suggest(sender.Text);
            }
        };

        _search.SuggestionChosen += (_, args) =>
        {
            if (args.SelectedItem is FrameworkElement { Tag: SettingHit hit })
            {
                _search.Text = hit.Entry.Title;
            }
        };

        _search.QuerySubmitted += (_, args) =>
        {
            var hit = args.ChosenSuggestion is FrameworkElement { Tag: SettingHit chosen }
                ? chosen
                : _hits.Count > 0 ? _hits[0] : null;

            if (hit is not null)
            {
                Land(hit);
            }
        };
    }

    /// <summary>Fill the list under the box with the best matches for what is typed.</summary>
    private void Suggest(string query)
    {
        try
        {
            _hits = _index.Find(query);
        }
        catch (Exception ex)
        {
            // Search failing must not take typing down with it.
            Log.Error(Scope, "settings search failed", ex);
            _hits = [];
        }

        var items = new List<object>();

        foreach (var hit in _hits)
        {
            items.Add(SuggestionRow(hit));
        }

        if (items.Count == 0 && query.Trim().Length > 0)
        {
            var none = new TextBlock { Text = Str.Common_NoResults.Text(), IsHitTestVisible = false };
            FluentLayout.ApplyStyle(none, "SettingsCaptionTextBlockStyle");
            items.Add(none);
        }

        _search.ItemsSource = items;
        _search.IsSuggestionListOpen = items.Count > 0;
    }

    /// <summary>One suggestion: the row's icon, its title, and the page it is on.</summary>
    private static Grid SuggestionRow(SettingHit hit)
    {
        var icon = new FontIcon { Glyph = hit.Glyph, FontSize = 16, VerticalAlignment = VerticalAlignment.Center };

        var title = new TextBlock { Text = hit.Entry.Title, TextTrimming = TextTrimming.CharacterEllipsis };
        var where = new TextBlock
        {
            // The section only when it adds something: "Desktop › Clock", not "Desktop › Clock › Clock".
            Text = hit.Entry.IsPage || hit.Entry.Section is null || hit.Entry.PagePath.EndsWith(hit.Entry.Section, StringComparison.Ordinal)
                ? hit.Entry.PagePath
                : $"{hit.Entry.PagePath} › {hit.Entry.Section}",
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        FluentLayout.ApplyStyle(where, "SettingsCaptionTextBlockStyle");
        where.TextWrapping = TextWrapping.NoWrap;

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, where } };
        Grid.SetColumn(text, 1);

        var row = new Grid
        {
            Tag = hit,
            ColumnSpacing = Fluent.Space3,
            Padding = new Thickness(0, Fluent.Space1, 0, Fluent.Space1),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Children = { icon, text },
        };

        AutomationProperties.SetName(row, Str.Shell_Settings_SearchHitName.Format(("title", hit.Entry.Title), ("page", hit.Entry.PagePath)));
        return row;
    }

    /// <summary>
    /// Open the hit's page and, for a row, bring it into view and ring it. A page hit lands at the
    /// top of its page, as Settings does.
    /// </summary>
    private void Land(SettingHit hit)
    {
        _search.IsSuggestionListOpen = false;
        var page = Show(hit.Target, record: true);

        if (hit.Entry.IsPage)
        {
            return;
        }

        // [S-1832] On a list page the same row is in every group: the one in the hit's group.
        var item = (hit.Target as ConfigTarget.App)?.Item;

        SettingRow? row = null;
        foreach (var (candidate, section) in SettingsIndex.Collect(page))
        {
            if (candidate.Title == hit.Entry.Title && section == hit.Entry.Section && candidate.Item == item)
            {
                row = candidate;
                break;
            }
        }

        if (row?.Card is not { } card)
        {
            return; // ignore: the row is not on the page as it is now (a source switched it off)
        }

        var expander = row.Parent;

        // After layout, or there is nothing to scroll to yet. Loaded is the page's first layout.
        if (page.IsLoaded)
        {
            Open(expander, card);
        }
        else
        {
            void OnLoaded(object sender, RoutedEventArgs args)
            {
                page.Loaded -= OnLoaded;
                Open(expander, card);
            }

            page.Loaded += OnLoaded;
        }
    }

    /// <summary>
    /// Open the expander the row is folded into, then ring the row once the opened group has been
    /// laid out — the next dispatcher turn after the expansion, not a guessed delay.
    /// </summary>
    private void Open(SettingsExpander? expander, Control card)
    {
        if (expander is null || expander.IsExpanded)
        {
            Ring(card);
            return;
        }

        expander.IsExpanded = true;

        void OnLayout(object? sender, object args)
        {
            if (card.ActualHeight <= 0)
            {
                return; // not laid out yet; the next pass comes
            }

            card.LayoutUpdated -= OnLayout;
            Ring(card);
        }

        card.LayoutUpdated += OnLayout;
    }

    /// <summary>Scroll the card a third of the way down the page and ring it in the accent.</summary>
    private void Ring(Control card)
    {
        card.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0.3, AnimationDesired = true });

        card.BorderBrush = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        card.BorderThickness = new Thickness(2);

        var timer = _window.DispatcherQueue.CreateTimer();
        timer.Interval = HighlightTime;
        timer.IsRepeating = false;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            card.ClearValue(Control.BorderBrushProperty);
            card.ClearValue(Control.BorderThicknessProperty);
        };

        timer.Start();
    }

#if DEBUG
    /// <summary>
    /// Debug renders only: type <paramref name="query"/> into the box and open its suggestions, so
    /// <c>--settings-png … --search &lt;text&gt;</c> can draw the list.
    /// </summary>
    internal void DebugSearch(string query)
    {
        _search.Text = query;
        Suggest(query);
    }

    /// <summary>Debug renders only: land on the best match for <paramref name="query"/>.</summary>
    internal void DebugLand(string query)
    {
        if (_index.Find(query) is { Count: > 0 } hits)
        {
            Land(hits[0]);
        }
    }

    /// <summary>Debug renders only: show a page without activating the window.</summary>
    internal FrameworkElement DebugShow(ConfigTarget target) => Show(target, record: true);

    /// <summary>Debug renders only: how tall the title row and page are, laid out at their width.</summary>
    internal double DebugPageHeight()
        => _crumbs.ActualHeight + _crumbs.Margin.Bottom + (_bodyHost.Content is FrameworkElement body ? body.ActualHeight : 0);

    /// <summary>Debug renders only: open the drop-down (a colour picker) on the row with this title.</summary>
    internal bool DebugOpenFlyout(string title)
    {
        if (_pageColumn.Child is not FrameworkElement page)
        {
            return false;
        }

        foreach (var (row, _) in SettingsIndex.Collect(page))
        {
            // [S-1723] A drop-down list's popup cannot be captured, so its items are drawn the way the
            // list draws them — each in its own font — in a card under the row, following the list
            // as it fills.
            if (row.Title == title && row.Control is ComboBox combo && page is Panel listHost && row.Card?.Parent is UIElement rowFrame)
            {
                var list = new StackPanel { Spacing = 2 };

                void Fill()
                {
                    list.Children.Clear();
                    foreach (var item in combo.Items.OfType<ComboBoxItem>().Take(16))
                    {
                        list.Children.Add(new TextBlock { Text = item.Content as string, FontFamily = item.FontFamily, Margin = new Thickness(12, 6, 12, 6) });
                    }
                }

                Fill();
                combo.Items.VectorChanged += (_, _) => Fill();

                listHost.Children.Insert(listHost.Children.IndexOf(rowFrame) + 1, new Border
                {
                    Child = list,
                    Padding = new Thickness(4),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    MinWidth = FluentLayout.ChoiceMinWidth,
                    CornerRadius = new CornerRadius(8),
                    Background = FluentLayout.Themed("AcrylicInAppFillColorDefaultBrush", Colors.Gray),
                    BorderBrush = FluentLayout.Themed("SurfaceStrokeColorFlyoutBrush", Colors.Gray),
                    BorderThickness = new Thickness(1),
                });
                return true;
            }

            if (row.Title == title && row.Control is DropDownButton button && ColorChoice.DebugPanel(button) is { } panel
                && page is Panel host && SettingRow.Of(row.Card?.Parent as FrameworkElement) is not null)
            {
                // Drawn as the flyout draws it, in a flyout-like card right under its row.
                var card = new Border
                {
                    Child = panel,
                    Padding = new Thickness(16),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    CornerRadius = new CornerRadius(8),
                    Background = FluentLayout.Themed("AcrylicInAppFillColorDefaultBrush", Colors.Gray),
                    BorderBrush = FluentLayout.Themed("SurfaceStrokeColorFlyoutBrush", Colors.Gray),
                    BorderThickness = new Thickness(1),
                };

                host.Children.Insert(host.Children.IndexOf((UIElement)row.Card!.Parent) + 1, card);
                return true;
            }

            // [S-1708] A several-of-many choice, also inside a group: its flyout's panel in a
            // flyout-like card under the page's element that holds the row (the group).
            if (row.Title == title && row.Control is DropDownButton multi && FluentLayout.DebugMultiChoicePanel(multi) is { } choices
                && page is Panel listPage && TopLevel(listPage, row.Card) is { } holder)
            {
                listPage.Children.Insert(listPage.Children.IndexOf(holder) + 1, new Border
                {
                    Child = choices,
                    Padding = new Thickness(16),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    CornerRadius = new CornerRadius(8),
                    Background = FluentLayout.Themed("AcrylicInAppFillColorDefaultBrush", Colors.Gray),
                    BorderBrush = FluentLayout.Themed("SurfaceStrokeColorFlyoutBrush", Colors.Gray),
                    BorderThickness = new Thickness(1),
                });
                return true;
            }
        }

        return false;
    }

    /// <summary>The child of <paramref name="page"/> that holds <paramref name="node"/>, or null.</summary>
    private static UIElement? TopLevel(Panel page, DependencyObject? node)
    {
        for (; node is not null; node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
        {
            if (node is UIElement element && page.Children.Contains(element))
            {
                return element;
            }
        }

        return null;
    }

    /// <summary>The window's root element, for the render to capture.</summary>
    internal FrameworkElement DebugRoot => (FrameworkElement)_window.Content;

    internal Window DebugWindow => _window;
#endif
}
