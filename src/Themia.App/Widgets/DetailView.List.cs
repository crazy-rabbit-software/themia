using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-2404] A detail widget's list (drives, processes, adapters): Windows 11 rows in a scroller that
/// fades at its foot while there is more ([S-1281]), a focus ring ([S-1288]) walked with the keys of
/// the shell's keyboard lease ([S-800]): Up/Down, Page Up/Down, Home/End, Enter to open where a row
/// opens (a drive), Escape to let go. The focus follows its row's key across reads, so a list that
/// reorders (processes) keeps the ring on the same process. Rows are reused by position.
/// <para>
/// [S-6603] Only the rows in view and one viewport below are built (<see cref="FolderRealize"/>, the
/// folder widget's rule); the rest is a spacer of their height, so the scroll range is right. A
/// scroll, the keyboard cursor or a taller tile builds more; nothing built is taken down. Every
/// process as a row in every list was about 19 MB of native XAML per Top processes tile.
/// </para>
/// </summary>
internal sealed partial class DetailView : IWidgetKeys
{
    /// <summary>From this width a row shows its second figure (memory beside the processor share).</summary>
    private const double SecondFrom = 280;

    private readonly ScrollViewer _scroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Padding = WidgetLayout.ListInset,
    };

    private readonly StackPanel _list = new();
    private readonly List<ListRow> _rows = [];

    /// <summary>[S-6603] The height of the rows not built yet; always the list's last child.</summary>
    private readonly Border _tail = new() { IsHitTestVisible = false };
    private readonly List<IDisposable> _hovers = [];
    private ScrollFade? _fade;
    private int _focus = -1;
    private string? _focusKey;

    private void BuildList()
    {
        _scroll.Content = _list;
        _list.Children.Add(_tail);
        _scroll.ViewChanged += (_, _) => GrowList();
        _scroll.Visibility = Visibility.Collapsed;
        SetRow(_scroll, 2);
        SetColumnSpan(_scroll, 2);
        Children.Add(_scroll);
        _fade = new ScrollFade(_scroll, _list);
    }

    private void DetachList()
    {
        _fade?.Dispose();
        _fade = null;

        foreach (var hover in _hovers)
        {
            hover.Dispose();
        }

        _hovers.Clear();

        foreach (var row in _rows)
        {
            row.Root.PointerPressed -= OnRowPressed;
        }
    }

    /// <summary>Show the list at <paramref name="height"/>, or hide it (0).</summary>
    private void ShowList(double height)
    {
        _scroll.Visibility = height > 0 ? Visibility.Visible : Visibility.Collapsed;

        // [S-3861] As tall as the plan made it (its rows, or the room): never a stretched band of nothing.
        _scroll.Height = height > 0 ? height : double.NaN;
        _scroll.VerticalAlignment = VerticalAlignment.Top;

        // Stacked under the facts: the list starts after the section gap, or at the top when it is all there is.
        var under = _heroBlock.Visibility == Visibility.Visible || _facts.Visibility == Visibility.Visible;
        _scroll.Margin = new Thickness(0, under ? DetailFace.SectionGap : 0, 0, 0);
    }

    /// <summary>The rows for the sheet: reused by position, created as needed, extra ones hidden.</summary>
    private void FillList()
    {
        if (_scroll.Visibility != Visibility.Visible)
        {
            return;
        }

        var count = _part.MaxRows > 0 ? Math.Min(_part.MaxRows, _sheet.Rows.Count) : _sheet.Rows.Count;
        var compact = _part.ListRowHeight <= Fluent.RowHeightCompact;
        var wide = _area.Width >= SecondFrom;
        var roomy = Roomy(count);
        var rowHeight = RowHeight(roomy);
        var built = FolderRealize.Target(count, _rows.Count, _scroll.VerticalOffset, _scroll.ViewportHeight, rowHeight, 1, _focus);

        while (_rows.Count < built)
        {
            var row = new ListRow(_part.ListRowHeight, compact);
            row.Root.Tag = _rows.Count;
            row.Root.PointerPressed += OnRowPressed;
            _hovers.Add(Fluent.Hover(row.Root));
            _rows.Add(row);
            _list.Children.Insert(_list.Children.Count - 1, row.Root);
        }

        _tail.Height = FolderRealize.SpacerHeight(count, _rows.Count, rowHeight, 1);

        for (var i = 0; i < _rows.Count; i++)
        {
            var shown = i < count;
            _rows[i].Root.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;

            if (shown)
            {
                _rows[i].Size(roomy, _part.ListRowHeight);
                _rows[i].Show(_sheet.Rows[i], wide, this);
            }
        }

        // The ring stays on its row's key; a row that left the list gives it to the one in its place.
        if (_focusKey is not null)
        {
            var at = -1;
            for (var i = 0; i < count; i++)
            {
                if (_sheet.Rows[i].Key == _focusKey)
                {
                    at = i;
                    break;
                }
            }

            Focus(at >= 0 ? at : ListKeys.AfterRemoval(_focus, count), bringIntoView: false);
        }

        _fade?.Refresh();
    }

    /// <summary>[S-6603] A scroll came near the end of what is built: build further.</summary>
    private void GrowList()
    {
        if (!_detached && _scroll.Visibility == Visibility.Visible && _tail.Height > 0)
        {
            FillList();
        }
    }

    /// <summary>A row's height for the spacer: a built row as drawn, else the planned height.</summary>
    private double RowHeight((double Height, TypeStep Value, double Bar)? roomy)
    {
        if (roomy is { } r)
        {
            return r.Height;
        }

        var drawn = _rows.Count > 0 ? _rows[0].Root.ActualHeight : 0;
        return drawn > 0 ? drawn : _part.ListRowHeight;
    }

    /// <summary>
    /// [S-2420] A list of a few rows in a tall tile (one drive in an L tile): each row grows into its share
    /// of the height, its figure by ramp step and its bar thicker, so the tile is not a row over a hole.
    /// Null when the rows stay standard. Only for a part whose rows are worth growing.
    /// </summary>
    private (double Height, TypeStep Value, double Bar)? Roomy(int count)
    {
        if (!_part.RowsGrow || count == 0 || _plan is not { List: true } plan)
        {
            return null;
        }

        var share = Math.Min((plan.ListHeight - WidgetLayout.ListInset.Bottom - WidgetLayout.ListInset.Top) / count, 320);

        if (share < _part.ListRowHeight * 1.6)
        {
            return null;
        }

        var value = FluentType.Hero(_area.Width / 2.5, Math.Min(share - FluentType.Body.LineHeight - 32, FluentType.Display.LineHeight * 1.5), 4);
        value = value.FontSize < FluentType.Subtitle.FontSize ? FluentType.Subtitle : value;
        return (share, value, 8);
    }

    private int ListCount => _scroll.Visibility != Visibility.Visible ? 0
        : _part.MaxRows > 0 ? Math.Min(_part.MaxRows, _sheet.Rows.Count) : _sheet.Rows.Count;

    /// <summary>[S-1564] Only a list on screen takes the keyboard.</summary>
    public bool WantsKeys => !_detached && ListCount > 0;

    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        var decision = ListKeys.Decide(key, _focus, ListCount);

        switch (decision.Command)
        {
            case ListCommand.Focus:
                Focus(decision.Index, bringIntoView: true);
                return true;

            case ListCommand.Open when _part.RowsOpen && decision.Index >= 0 && decision.Index < _sheet.Rows.Count:
                _part.Open(_sheet.Rows[decision.Index]);
                return true;

            case ListCommand.Release:
                var had = _focus >= 0;
                Focus(-1, bringIntoView: false);
                return had;

            default:
                return false;
        }
    }

    private void Focus(int index, bool bringIntoView)
    {
        _focus = index;
        _focusKey = index >= 0 && index < _sheet.Rows.Count ? _sheet.Rows[index].Key : null;

        // [S-6603] The cursor moved past what is built (End, Page down): build through it first.
        if (index >= _rows.Count && index < ListCount)
        {
            FillList();
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            var focused = i == index;

            if (focused && bringIntoView)
            {
                RowFocus.Mark(_rows[i].Root, true);
            }
            else
            {
                _rows[i].Root.BorderBrush = focused ? _accentBrush : _clearBrush;
            }
        }
    }

    /// <summary>A click on a row puts the ring on it; the card takes the keyboard lease for the click.</summary>
    private void OnRowPressed(object sender, PointerRoutedEventArgs args)
    {
        if (sender is Border { Tag: int index } && !_detached)
        {
            Focus(index, bringIntoView: false);
        }
    }

    /// <summary>
    /// One list row: an optional icon (an adapter's kind, a program's own icon, [S-2416], [S-2433]),
    /// the name, the figures at the right, an optional line and bar under them.
    /// </summary>
    private sealed class ListRow
    {
        private readonly FontIcon _symbol = Fluent.Icon(Glyphs.Program);
        private readonly Image _image = new() { Width = Fluent.IconSize, Height = Fluent.IconSize, Stretch = Stretch.Uniform };
        private readonly Grid _icon = new() { Width = Fluent.IconSize, Height = Fluent.IconSize, VerticalAlignment = VerticalAlignment.Center };
        private string? _iconPath;
        private LogoTheme _iconTheme;
        private readonly TextBlock _name = Fluent.Text(FluentType.Body);
        private readonly TextBlock _second = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        private readonly TextBlock _value = Fluent.Text(FluentType.Body);
        private readonly TextBlock _detail = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        private readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 1, Height = 3, MinHeight = 3 };

        public ListRow(double height, bool compact)
        {
            Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_value, FontNumeralAlignment.Tabular);
            Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_second, FontNumeralAlignment.Tabular);

            var grid = new Grid { RowSpacing = 2 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _second.Margin = new Thickness(Fluent.Space3, 0, 0, 0);
            _second.HorizontalAlignment = HorizontalAlignment.Right;
            _second.MinWidth = Fluent.Space6 * 3;
            _second.TextAlignment = TextAlignment.Right;
            _value.Margin = new Thickness(Fluent.Space3, 0, 0, 0);
            _value.HorizontalAlignment = HorizontalAlignment.Right;
            _value.MinWidth = Fluent.Space6 * 2;
            _value.TextAlignment = TextAlignment.Right;

            // The icon sits in its own column, centred on the name and the line under it, as a
            // Windows 11 list row's leading icon does; the text column starts after one gap.
            _icon.Children.Add(_symbol);
            _icon.Children.Add(_image);
            _icon.Margin = new Thickness(0, 0, Fluent.Space3, 0);
            _icon.Visibility = Visibility.Collapsed;
            Grid.SetRowSpan(_icon, 2);
            Grid.SetColumn(_name, 1);
            Grid.SetColumn(_second, 2);
            Grid.SetColumn(_value, 3);
            Grid.SetRow(_detail, 1);
            Grid.SetColumn(_detail, 1);
            Grid.SetColumnSpan(_detail, 3);
            Grid.SetRow(_bar, 2);
            Grid.SetColumn(_bar, 1);
            Grid.SetColumnSpan(_bar, 3);
            _bar.Margin = new Thickness(0, 2, 0, 0);

            grid.Children.Add(_icon);
            grid.Children.Add(_name);
            grid.Children.Add(_second);
            grid.Children.Add(_value);
            grid.Children.Add(_detail);
            grid.Children.Add(_bar);

            Root = Fluent.Row(grid, compact);
            Root.MinHeight = height;
            RowFocus.Prepare(Root);
        }

        public Border Root { get; }

        private bool? _roomy;

        /// <summary>Standard, or grown into <paramref name="roomy"/>'s share.</summary>
        public void Size((double Height, TypeStep Value, double Bar)? roomy, double standard)
        {
            if (roomy is { } r)
            {
                Root.MinHeight = r.Height;
                ((FrameworkElement)Root.Child).VerticalAlignment = VerticalAlignment.Center; // [S-3861] centred in its share: no band under a grown drive
                Root.Padding = new Thickness(Fluent.Space4, Fluent.Space2, Fluent.Space4, 0);
                _value.Apply(r.Value);
                _name.Apply(FluentType.BodyStrong);
                _detail.Apply(FluentType.Body);
                _bar.Height = _bar.MinHeight = r.Bar;
                _roomy = true;
            }
            else if (_roomy != false)
            {
                Root.MinHeight = standard;
                ((FrameworkElement)Root.Child).VerticalAlignment = VerticalAlignment.Center;
                Root.Padding = new Thickness(Fluent.Space4, 0, Fluent.Space4, 0);
                _value.Apply(FluentType.Body);
                _name.Apply(FluentType.Body);
                _detail.Apply(FluentType.Caption);
                _bar.Height = _bar.MinHeight = 3;
                _roomy = false;
            }
        }

        public void Show(DetailRow row, bool wide, DetailView owner)
        {
            _name.Text = row.Name;
            _value.Text = row.Value;
            _value.Foreground = row.Value == "--" ? owner._disabledBrush : owner._primaryBrush;

            _second.Text = row.Second ?? string.Empty;
            _second.Visibility = wide && row.Second is not null ? Visibility.Visible : Visibility.Collapsed;

            _detail.Text = row.Detail ?? string.Empty;
            _detail.Visibility = row.Detail is null ? Visibility.Collapsed : Visibility.Visible;

            ShowIcon(row);

            _bar.Visibility = row.Fraction is null ? Visibility.Collapsed : Visibility.Visible;
            _bar.Value = row.Fraction ?? 0;
            _bar.Foreground = owner.PressureBrush(row.Pressure);
            ToolTipService.SetToolTip(Root, row.Detail is null ? null : $"{row.Name} · {row.Detail}");
        }

        /// <summary>
        /// [S-2433] The row's symbol at once, and the file's own icon over it once it is extracted —
        /// asked only when the row shows another file than before (rows are reused by position and a
        /// list reorders every read), cached by path and size in <see cref="AppIcons"/>.
        /// </summary>
        private void ShowIcon(DetailRow row)
        {
            _icon.Visibility = row.Symbol == RowSymbol.None && row.IconPath is null ? Visibility.Collapsed : Visibility.Visible;
            _symbol.Glyph = Glyph(row.Symbol);

            // [S-3965] A packaged app's logo is chosen per theme, so a theme change asks again.
            if (string.Equals(_iconPath, row.IconPath, StringComparison.OrdinalIgnoreCase) && _iconTheme == AppIcons.ThemeOf(Root))
            {
                return;
            }

            _iconPath = row.IconPath;
            _image.Source = null;
            _symbol.Visibility = Visibility.Visible;

            if (row.IconPath is { } path)
            {
                _ = LoadIconAsync(path);
            }
        }

        private async Task LoadIconAsync(string path)
        {
            // Decoded at the row's size on this display, so a 16 DIP icon stays sharp at 200%.
            var scale = Root.XamlRoot?.RasterizationScale ?? 2;
            var theme = AppIcons.ThemeOf(Root);
            _iconTheme = theme;
            var image = await AppIcons.GetAsync(path, (int)Math.Ceiling(Fluent.IconSize * Math.Max(1, scale)), theme).ConfigureAwait(true);

            // A row that has moved on to another process (or theme) while this was extracted keeps its own.
            if (image is null || !string.Equals(_iconPath, path, StringComparison.OrdinalIgnoreCase) || _iconTheme != theme)
            {
                return;
            }

            _image.Source = image;
            _symbol.Visibility = Visibility.Collapsed;
        }

        private static string Glyph(RowSymbol symbol) => symbol switch
        {
            RowSymbol.Wired => Glyphs.Ethernet,
            RowSymbol.Wireless => Glyphs.Wifi,
            RowSymbol.Cellular => Glyphs.Signal,
            RowSymbol.Network => Glyphs.Network,
            _ => Glyphs.Program,
        };
    }

    private readonly Brush _accentBrush = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
    private readonly Brush _cautionBrush = FluentLayout.Themed("SystemFillColorCautionBrush", Colors.Orange);
    private readonly Brush _criticalBrush = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.Red);
    private readonly Brush _successBrush = FluentLayout.Themed("SystemFillColorSuccessBrush", Colors.Green);
    private readonly Brush _clearBrush = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);

    /// <summary>Looked up once per view: a resource lookup per row per read is a dictionary hit for nothing.</summary>
    private Brush PressureBrush(MeterPressure pressure) => pressure switch
    {
        MeterPressure.Critical => _criticalBrush,
        MeterPressure.Elevated => _cautionBrush,
        MeterPressure.Healthy => _successBrush,
        _ => _accentBrush,
    };
}
