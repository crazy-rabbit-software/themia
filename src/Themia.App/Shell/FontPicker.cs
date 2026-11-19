using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Windows.System;

namespace Themia.App.Shell;

/// <summary>
/// [S-1723] [S-6442] The font picker's content: a search box over a virtualised list of the presets
/// and every installed family, each entry drawn in the font it names, the current one ticked.
/// <para>
/// A <see cref="ListView"/> of entries rather than a row per font: its stack panel
/// only realises the rows in view, so a PC with 600 families pays for the dozen on screen, and the
/// typeface of a row is only loaded when it scrolls in. The row is built in
/// <see cref="ListViewBase.ContainerContentChanging"/> into an empty template's grid.
/// </para>
/// <para>
/// Keys: every search's ([S-13607], <see cref="SearchBox"/>): typing goes to the search box (also from the list);
/// Down moves into the list, Up from its first row back to the box; Enter in the box takes the best match, on a
/// row that row; Escape empties the box, and with it empty closes the Font card it stands in, as any picker.
/// </para>
/// </summary>
internal sealed partial class FontPicker : Grid
{
    private const double ListHeight = ShellMetrics.Picker.FontListHeight;

    /// <summary>
    /// An empty grid per row: the template only gives each container a root of its own, filled in
    /// <see cref="OnContainerContentChanging"/>. No bindings, so nothing is reflected over.
    /// </summary>
    private static readonly Lazy<DataTemplate> EmptyRow = new(() => (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
        "<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Grid/></DataTemplate>"));

    private readonly Action<string> _choose;
    private readonly string _chosen;
    private readonly SearchBox _box;
    private readonly ListView _list;
    private readonly TextBlock _empty;
    private readonly Dictionary<string, FontFamily?> _families = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<FontOption> _options;
    private List<FontItem> _shown = [];

    public FontPicker(IReadOnlyList<FontOption> options, string chosen, Action<string> choose)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(choose);

        _options = options;
        _chosen = chosen;
        _choose = choose;

        RowSpacing = Fluent.Space2;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // [S-13606] The one search box; [S-13607] its keys: Enter takes the best match, Down goes into the list.
        _box = new SearchBox(Str.Shell_Font_Search.Text())
        {
            Count = () => _shown.Count,
            PickBest = PickBest,
            ToFirst = ToFirst,
        };
        _box.Changed += _ => Refresh();
        Children.Add(_box);

        _list = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            IsItemClickEnabled = true,
            Height = ListHeight,
            TabNavigation = KeyboardNavigationMode.Once,
            ItemTemplate = EmptyRow.Value,
        };
        AutomationProperties.SetName(_list, Str.Settings_Font_FamilyLabel.Text());
        _list.ItemClick += (_, e) =>
        {
            if (e.ClickedItem is FontItem item)
            {
                _choose(item.Option.Value);
            }
        };
        _list.ContainerContentChanging += OnContainerContentChanging;

        // Up from the first row, Escape, Backspace and typing on a row go back to the box.
        _box.Results(_list, source => source is ListViewItem item ? _list.IndexFromContainer(item) : null);

        // [S-13608] Nothing matching is said in the one status line, over the list.
        _empty = SearchBox.Status(string.Empty);
        _empty.Margin = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0);
        _empty.Visibility = Visibility.Collapsed;

        // The message above the list, in one box of a fixed height, so the submenu does not jump
        // while typing.
        var body = new Grid
        {
            Height = ListHeight,
            RowSpacing = Fluent.Space1,
            RowDefinitions = { new() { Height = GridLength.Auto }, new() { Height = new GridLength(1, GridUnitType.Star) } },
        };
        _list.Height = double.NaN;
        SetRow(_list, 1);
        body.Children.Add(_empty);
        body.Children.Add(_list);
        SetRow(body, 1);
        Children.Add(body);

        Refresh();
        _list.Loaded += (_, _) => ScrollToChosen();
    }

    /// <summary>Put the caret in the search box.</summary>
    public void FocusSearch() => _box.FocusEnd();

    /// <summary>Type into the search box, for the debug render.</summary>
    internal void SetQuery(string text) => _box.Text = text;

    /// <summary>[S-6443] The installed fonts changed while the picker is open: list the new set, keep the query.</summary>
    public void SetOptions(IReadOnlyList<FontOption> options)
    {
        _options = options;
        Refresh();
        if (_box.Text.Trim().Length == 0)
        {
            ScrollToChosen();
        }
    }

    private void Refresh()
    {
        var query = _box.Text;
        _shown = [.. FontSearch.Filter(_options, query).Select(option => new FontItem(option))];
        _list.ItemsSource = _shown;

        // Only the pinned default left: say nothing else matched.
        var none = query.Trim().Length > 0 && FontSearch.Best(_options, query) is null;
        _empty.Text = none ? Str.Shell_Font_NoMatch.Format(("query", query.Trim())) : string.Empty;
        _empty.Visibility = none ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ScrollToChosen()
    {
        foreach (var item in _shown)
        {
            if (item.Option.Value == _chosen)
            {
                _list.ScrollIntoView(item, ScrollIntoViewAlignment.Default);
                return;
            }
        }
    }

    /// <summary>Enter in the box: the best match, where one is.</summary>
    private void PickBest()
    {
        if (FontSearch.Best(_options, _box.Text) is { } best)
        {
            _choose(best.Value);
        }
    }

    /// <summary>Down in the box: onto the list's first row.</summary>
    private void ToFirst()
    {
        if (_list.ContainerFromIndex(0) is ListViewItem first)
        {
            first.Focus(FocusState.Keyboard);
        }
        else
        {
            _list.Focus(FocusState.Keyboard);
        }
    }

    /// <summary>
    /// A realised row: the name in its own typeface (a preset in the family it maps to, [S-826]; the
    /// system entry in the UI's own), with a tick on the current one. A recycled container rebuilds
    /// nothing until it is handed its next font.
    /// </summary>
    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.Item is not FontItem { Option: var option })
        {
            return;
        }

        var container = args.ItemContainer;
        if (container.ContentTemplateRoot is not Grid root)
        {
            return;
        }

        if (root.Tag is not Row row)
        {
            row = new Row(root);
            root.Tag = row;
        }

        row.Label.Text = option.Label;
        // A symbol font cannot spell its own name: it is named in the UI's font.
        if (!option.Symbol && Family(option.Value) is { } family)
        {
            row.Label.FontFamily = family;
        }
        else
        {
            row.Label.ClearValue(TextBlock.FontFamilyProperty);
        }

        var ticked = option.Value == _chosen;
        row.Tick.Visibility = ticked ? Visibility.Visible : Visibility.Collapsed;
        container.HorizontalContentAlignment = HorizontalAlignment.Stretch;

        // The row's text is in the font it shows, which a screen reader cannot read: name it plainly.
        AutomationProperties.SetName(container, option.Label);
        AutomationProperties.SetItemStatus(container, ticked ? Str.Shell_Font_Current.Text() : string.Empty);
        ToolTipService.SetToolTip(container, option.Label);
    }

    private FontFamily? Family(string value)
    {
        if (!_families.TryGetValue(value, out var family))
        {
            family = DesignLook.FontFamilyName(value) is { } name ? new FontFamily(name) : null;
            _families[value] = family;
        }

        return family;
    }

    /// <summary>
    /// A list item: the Core entry in a type of this assembly, which the WinRT projection can hand to
    /// the list under Native AOT (a Core record has no projection and is refused).
    /// </summary>
    private sealed partial class FontItem(FontOption option)
    {
        public FontOption Option { get; } = option;

        public override string ToString() => Option.Label;
    }

    /// <summary>One row's parts, kept on its container so a recycled one is refilled, not rebuilt.</summary>
    private sealed class Row
    {
        public Row(Grid root)
        {
            Label = Fluent.Text(FamilyType.Body);
            Label.TextTrimming = TextTrimming.CharacterEllipsis;
            Label.VerticalAlignment = VerticalAlignment.Center;

            Tick = Fluent.Icon(Glyphs.Done, ShellRoles.Key(ShellRole.AccentText));
            Tick.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
            SetColumn(Tick, 1);

            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            root.Children.Add(Label);
            root.Children.Add(Tick);
        }

        public TextBlock Label { get; }

        public FontIcon Tick { get; }
    }
}
