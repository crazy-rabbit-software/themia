using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Icons;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Windows.System;

namespace Themia.App.Shell;

/// <summary>
/// [S-1803] The icon picker: the default cell and a search box over a virtualised grid of every
/// Segoe Fluent glyph (<see cref="FluentIcons"/>), ranked by <see cref="IconSearch"/>. [S-3542] It is
/// a submenu's content (a view's Icon ›), so it fits the menu's one width: eight 32-DIP cells and
/// the scroll bar beside them inside 300 DIPs.
/// <para>
/// A <see cref="GridView"/> of glyph strings rather than a button per glyph: its wrap panel only
/// realises the rows in view, so 1,500 glyphs cost what 80 do, and it brings arrow keys, Home/End,
/// Page Up/Down and Enter for free. The items are the stored values themselves (the glyph string),
/// drawn by the container's own text in the symbol font, so no data template is needed.
/// </para>
/// </summary>
internal sealed partial class IconPicker : Grid
{
    private const int Columns = 8;
    private const int VisibleRows = 8;
    private const double Cell = 32;

    /// <summary>The glyph in a cell: the 20-DIP icon box Settings draws its icons in ([S-903]).</summary>
    private const double GlyphSize = 20;

    /// <summary>The search for the language it was built in; rebuilt when the language changes.</summary>
    private static (Locale Locale, IconSearch Search)? _search;

    private readonly string? _value;
    private readonly Action<string?> _choose;
    private readonly TextBox _box;
    private readonly GridView _grid;
    private readonly TextBlock _empty;
    private List<string> _shown = [];

    public IconPicker(string? value, IconLook look, string? defaultName, Action<string?> choose)
    {
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(choose);

        _value = value;
        _choose = choose;
        defaultName ??= Str.Common_Default.Text();

        RowSpacing = Fluent.Space2;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // The default first, beside the box: it is a value of its own, not a glyph in the set.
        var useDefault = new ToggleButton
        {
            Content = look.For(null),
            Width = Cell,
            Height = Cell,
            Padding = new Thickness(0),
            IsChecked = value is null,
        };
        ToolTipService.SetToolTip(useDefault, defaultName);
        AutomationProperties.SetName(useDefault, defaultName);
        useDefault.Click += (_, _) => _choose(null);

        _box = new TextBox
        {
            PlaceholderText = Str.Shell_Icon_Search.Text(),
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(_box, Str.Shell_Icon_Search.Text());
        _box.TextChanged += (_, _) => Refresh();
        _box.KeyDown += OnBoxKeyDown;

        var header = new Grid { ColumnSpacing = Fluent.Space2 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        SetColumn(_box, 1);
        header.Children.Add(useDefault);
        header.Children.Add(_box);
        Children.Add(header);

        _grid = new GridView
        {
            SelectionMode = ListViewSelectionMode.Single,
            IsItemClickEnabled = true,
            Width = (Columns * Cell) + Fluent.Space4, // room for the scroll bar beside eight columns
            Height = VisibleRows * Cell,
            ItemContainerStyle = CellStyle(),
            TabNavigation = KeyboardNavigationMode.Once,
        };
        AutomationProperties.SetName(_grid, Str.Shell_Icon_Search.Text());
        _grid.ItemClick += (_, e) =>
        {
            if (e.ClickedItem is string glyph)
            {
                _choose(glyph);
            }
        };
        _grid.ContainerContentChanging += OnContainerContentChanging;

        _empty = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        _empty.TextWrapping = TextWrapping.Wrap;
        _empty.Margin = new Thickness(Fluent.Space1, Fluent.Space2, Fluent.Space1, 0);
        _empty.VerticalAlignment = VerticalAlignment.Top;
        _empty.Visibility = Visibility.Collapsed;

        // The grid's box stays when nothing matches, so the submenu does not jump while typing.
        var body = new Grid { Width = _grid.Width, Height = _grid.Height, Children = { _grid, _empty } };
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
        _shown = [.. Search().Find(query).Select(icon => icon.Glyph)];
        _grid.ItemsSource = _shown;

        if (_value is not null && _shown.Contains(_value))
        {
            _grid.SelectedItem = _value;
        }

        var none = _shown.Count == 0;
        _empty.Text = none ? Str.Shell_Icon_NoMatch.Format(("query", query.Trim())) : string.Empty;
        _empty.Visibility = none ? Visibility.Visible : Visibility.Collapsed;
        _grid.Visibility = none ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Enter takes the best match; Down moves into the grid, onto the first glyph.</summary>
    private void OnBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_shown.Count == 0)
        {
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            _choose(_shown[0]);
        }
        else if (e.Key == VirtualKey.Down)
        {
            e.Handled = true;
            if (_grid.ContainerFromIndex(0) is GridViewItem first)
            {
                first.Focus(FocusState.Keyboard);
            }
            else
            {
                _grid.Focus(FocusState.Keyboard);
            }
        }
    }

    /// <summary>A realised cell gets the glyph's name as its tooltip and accessible name; a recycled one keeps nothing.</summary>
    private static void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.Item is not string glyph || FluentIcons.ByGlyph(glyph) is not { } icon)
        {
            return;
        }

        var name = FluentIcons.NameFor(icon, Strings.Current);
        ToolTipService.SetToolTip(args.ItemContainer, name);
        AutomationProperties.SetName(args.ItemContainer, name);
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
}

/// <summary>[S-903] How an icon choice draws a value: the glyph, or for the default the default glyph or text (a view's initials).</summary>
internal sealed class IconLook(string defaultGlyph, string? defaultText)
{
    public UIElement For(string? glyph) => glyph is null && defaultText is not null
        ? new TextBlock { Text = defaultText, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }
        : new FontIcon { Glyph = glyph ?? defaultGlyph, FontSize = 16 };
}
