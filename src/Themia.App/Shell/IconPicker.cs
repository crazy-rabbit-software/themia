using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Icons;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.System;

namespace Themia.App.Shell;

/// <summary>
/// [S-1803] [S-13605] The icon picker, Karpo's (karpo-app, docs/widgets.md §249, §306, §682): inline in its
/// card, the search box across the top, then one dense grid of every Segoe Fluent glyph
/// (<see cref="FluentIcons"/>, ranked by <see cref="IconSearch"/>) in 40-DIP squares, no headings, as many
/// across as the card is wide, one height (400) whatever a search leaves, scrolling inside. Its first square
/// is the cross, the default (a view's initials, a widget's own glyph): it takes the icon chosen away,
/// ringed while nothing is chosen; a search leaves the glyphs that match in place of the grid, without it.
/// <para>
/// A <see cref="GridView"/> rather than a button per glyph: its wrap panel only realises the rows in view,
/// so 1,500 glyphs cost what 80 do, and it brings arrow keys, Home/End, Page Up/Down and Enter for free.
/// The items are the stored values themselves (the glyph string; the default a <see cref="DefaultCell"/>),
/// drawn by the container's own text in the symbol font, so no data template is needed.
/// </para>
/// </summary>
internal sealed partial class IconPicker : Grid
{
    private const double Cell = ShellMetrics.Picker.Cell;

    /// <summary>The glyph in a cell: the 20-DIP icon box Settings draws its icons in ([S-903]).</summary>
    private const double GlyphSize = ShellMetrics.Picker.Glyph;

    /// <summary>The search for the language it was built in; rebuilt when the language changes.</summary>
    private static (Locale Locale, IconSearch Search)? _search;

    private readonly string? _value;
    private readonly Action<string?> _choose;
    private readonly DefaultCell _default;
    private readonly SearchBox _box;
    private readonly GridView _grid;
    private readonly TextBlock _empty;
    private List<object> _shown = [];

    /// <param name="value">The glyph chosen, or null for the default.</param>
    /// <param name="defaultName">What the default is called ("Initials"; a widget's "Its own icon").</param>
    /// <param name="defaultText">What the default draws where it is words (a view's initials), said after its name; null for none.</param>
    public IconPicker(string? value, string? defaultName, string? defaultText, Action<string?> choose)
    {
        ArgumentNullException.ThrowIfNull(choose);

        _value = value;
        _choose = choose;
        defaultName ??= Str.Common_Default.Text();
        _default = new DefaultCell(string.IsNullOrEmpty(defaultText) ? defaultName : $"{defaultName} ({defaultText})");

        RowSpacing = Fluent.Space2;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // [S-13606] The one search box; [S-13607] its keys: Enter takes the best match, Down goes into the grid.
        _box = new SearchBox(Str.Shell_Icon_Search.Text())
        {
            Count = () => _shown.Count,
            PickBest = () => _choose(_shown[0] as string),
        };
        _box.Changed += _ => Refresh();
        Children.Add(_box);

        _grid = new GridView
        {
            SelectionMode = ListViewSelectionMode.Single,
            IsItemClickEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = ShellMetrics.Picker.IconsTall,
            ItemContainerStyle = CellStyle(),
            TabNavigation = KeyboardNavigationMode.Once,
        };
        AutomationProperties.SetName(_grid, Str.Shell_Icon_Search.Text());
        _grid.ItemClick += (_, e) =>
        {
            switch (e.ClickedItem)
            {
                case DefaultCell:
                    _choose(null);
                    break;
                case string glyph:
                    _choose(glyph);
                    break;
            }
        };
        _grid.ContainerContentChanging += OnContainerContentChanging;
        _box.ToFirst = () =>
        {
            if (_grid.ContainerFromIndex(0) is GridViewItem first)
            {
                first.Focus(FocusState.Keyboard);
            }
            else
            {
                _grid.Focus(FocusState.Keyboard);
            }
        };

        // Up from the top row, Escape, Backspace and typing on a square go back to the box.
        _box.Results(_grid, source => source is GridViewItem item ? _grid.IndexFromContainer(item) < Columns() ? 0 : 1 : null);

        // [S-13608] Nothing matching is said in the one status line, where the grid stood.
        _empty = SearchBox.Status(string.Empty);
        _empty.Margin = new Thickness(Fluent.Space1, Fluent.Space2, Fluent.Space1, 0);
        _empty.VerticalAlignment = VerticalAlignment.Top;
        _empty.Visibility = Visibility.Collapsed;

        // The grid's box stays when nothing matches, so the card does not jump while typing.
        var body = new Grid { Height = _grid.Height, Children = { _grid, _empty } };
        SetRow(body, 1);
        Children.Add(body);

        Refresh();
        _grid.Loaded += (_, _) =>
        {
            if (_grid.SelectedItem is { } selected)
            {
                _grid.ScrollIntoView(selected, ScrollIntoViewAlignment.Default);
            }
        };
    }

    /// <summary>Type into the search box, for the debug render.</summary>
    internal void SetQuery(string text) => _box.Text = text;

    private static IconSearch Search()
    {
        var locale = Strings.Current;
        if (_search is not { } cached || cached.Locale != locale)
        {
            cached = (locale, FluentIcons.SearchFor(locale));
            _search = cached;
        }

        return cached.Search;
    }

    private void Refresh()
    {
        var query = _box.Text;
        var searching = !string.IsNullOrWhiteSpace(query);
        List<object> shown = searching ? [] : [_default];
        shown.AddRange(Search().Find(query).Select(icon => (object)icon.Glyph));
        _shown = shown;
        _grid.ItemsSource = _shown;

        if (_value is null && !searching)
        {
            _grid.SelectedItem = _default;
        }
        else if (_value is not null && _shown.Contains(_value))
        {
            _grid.SelectedItem = _value;
        }

        var none = _shown.Count == 0;
        _empty.Text = none ? Str.Shell_Icon_NoMatch.Format(("query", query.Trim())) : string.Empty;
        _empty.Visibility = none ? Visibility.Visible : Visibility.Collapsed;
        _grid.Visibility = none ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>How many squares stand across the grid now (at least one).</summary>
    private int Columns() => Math.Max(1, (int)Math.Floor(_grid.ActualWidth / Cell));

    /// <summary>A realised square gets its name as its tooltip and accessible name; a recycled one keeps nothing.</summary>
    private static void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue)
        {
            return;
        }

        var name = args.Item switch
        {
            DefaultCell cell => cell.Name,
            string glyph when FluentIcons.ByGlyph(glyph) is { } icon => FluentIcons.NameFor(icon, Strings.Current),
            _ => null,
        };
        if (name is not null)
        {
            ToolTipService.SetToolTip(args.ItemContainer, name);
            AutomationProperties.SetName(args.ItemContainer, name);
        }
    }

    /// <summary>
    /// A square cell drawing its item (the glyph string) in the symbol font. Set on the container
    /// because the item style pins its own font family, which a value on the grid would not reach.
    /// </summary>
    private static Style CellStyle()
    {
        var symbols = Application.Current.Resources.TryGetValue("SymbolThemeFontFamily", out var font) && font is FontFamily family
            ? family
            : new FontFamily("Segoe Fluent Icons");

        return new Style(typeof(GridViewItem))
        {
            Setters =
            {
                new Setter(WidthProperty, Cell),
                new Setter(HeightProperty, Cell),
                new Setter(MinWidthProperty, Cell),
                new Setter(MinHeightProperty, Cell),
                new Setter(MarginProperty, new Thickness(0)),
                new Setter(Control.PaddingProperty, new Thickness(0)),
                new Setter(Control.FontFamilyProperty, symbols),
                new Setter(Control.FontSizeProperty, GlyphSize),
                new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
                new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center),
            },
        };
    }

    /// <summary>
    /// [S-13605] The default's square: Karpo's cross (§306), drawn as its text; a value of its own, never a
    /// glyph of the set (the cross is one too), so choosing it is choosing no icon.
    /// </summary>
    private sealed class DefaultCell(string name)
    {
        public string Name { get; } = name;

        public override string ToString() => Glyphs.Dismiss;
    }
}
