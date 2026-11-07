using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-2404] A detail widget's list (processes, adapters): the family's list rows ([S-6711]: the name
/// primary, its figure secondary, a second and a third line by the tier, [S-6712]) in a scroller of
/// whole rows that fades at its foot while there is more ([S-1281]), a focus ring ([S-1288]) walked
/// with the keys of the shell's keyboard lease ([S-800]): Up/Down, Page Up/Down, Home/End, Enter to
/// open where a row opens, Escape to let go. The focus follows its row's key across reads, so a list
/// that reorders (processes) keeps the ring on the same process. Rows are reused by position.
/// <para>
/// [S-6603] Only the rows in view and one viewport below are built (<see cref="FolderRealize"/>, the
/// folder widget's rule); the rest is a spacer of their height, so the scroll range is right. A
/// scroll, the keyboard cursor or a taller tile builds more; nothing built is taken down.
/// </para>
/// </summary>
public sealed partial class DetailView : IWidgetKeys
{
    private readonly ScrollViewer _scroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalAlignment = VerticalAlignment.Top,
    };

    private readonly StackPanel _list = new();
    private readonly List<ListRow> _rows = [];

    /// <summary>[S-6603] The height of the rows not built yet; always the list's last child.</summary>
    private readonly Border _tail = new() { IsHitTestVisible = false };
    private ScrollFade? _fade;
    private int _focus = -1;
    private string? _focusKey;
    private int _lines = 1;

    private void BuildList()
    {
        _scroll.Content = _list;
        _list.Children.Add(_tail);
        _scroll.ViewChanged += (_, _) => GrowList();
        _scroll.Visibility = Visibility.Collapsed;
        SetRow(_scroll, 2);
        Children.Add(_scroll);
        _fade = new ScrollFade(_scroll, _list);
    }

    private void DetachList()
    {
        _fade?.Dispose();
        _fade = null;

        foreach (var row in _rows)
        {
            row.Block.PointerPressed -= OnRowPressed;
            row.Block.Tapped -= OnRowTapped;
            row.Block.Detach();
        }
    }

    /// <summary>Show the list for a plan (its whole rows, under the facts or beside the figure), or hide it.</summary>
    private void ShowList(DetailFacePlan? plan)
    {
        var shown = plan is { List: true };
        _scroll.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        if (plan is not { List: true } p)
        {
            return;
        }

        // [S-6712] As tall as its whole rows: never a part of one at its foot.
        _scroll.Height = p.ListHeight;
        _lines = p.ListLines;
        SetColumn(_scroll, p.Side ? 1 : 0);
        SetRow(_scroll, p.Side ? 1 : 2);
        SetRowSpan(_scroll, p.Side ? 2 : 1);

        // Under the figure and no facts: 8 down from it, as the facts would be.
        var underHero = !p.Side && p.Facts == 0 && _hero.Visibility == Visibility.Visible;
        _scroll.Margin = new Thickness(0, underHero ? FamilySpace.S2 : 0, 0, 0);
    }

    private int ListCount => _scroll.Visibility != Visibility.Visible ? 0
        : _part.MaxRows > 0 ? Math.Min(_part.MaxRows, _sheet.Rows.Count) : _sheet.Rows.Count;

    /// <summary>The rows for the sheet: reused by position, created as needed, extra ones hidden.</summary>
    private void FillList()
    {
        if (_scroll.Visibility != Visibility.Visible)
        {
            return;
        }

        var count = ListCount;
        var rowHeight = FamilyShape.RowHeight(_lines);
        var lead = _sheet.Rows.Any(r => r.Symbol != RowSymbol.None || r.IconPath is not null);
        var built = FolderRealize.Target(count, _rows.Count, _scroll.VerticalOffset, _scroll.ViewportHeight, rowHeight, 1, _focus);

        while (_rows.Count < built)
        {
            var row = new ListRow();
            row.Block.Tag = _rows.Count;
            row.Block.PointerPressed += OnRowPressed;
            row.Block.Tapped += OnRowTapped;
            _rows.Add(row);
            _list.Children.Insert(_list.Children.Count - 1, row.Block);
        }

        _tail.Height = FolderRealize.SpacerHeight(count, _rows.Count, rowHeight, 1);

        for (var i = 0; i < _rows.Count; i++)
        {
            var shown = i < count;
            _rows[i].Block.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;

            if (shown)
            {
                _rows[i].Show(_sheet.Rows[i], _lines, lead, !_sheet.Absent);
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
            if (_rows[i].Block.Focused != focused)
            {
                _rows[i].Block.Focused = focused;
            }
            else if (focused && bringIntoView)
            {
                _rows[i].Block.StartBringIntoView();
            }
        }
    }

    /// <summary>A click on a row puts the ring on it; the card takes the keyboard lease for the click.</summary>
    private void OnRowPressed(object sender, PointerRoutedEventArgs args)
    {
        if (sender is RowBlock { Tag: int index } && !_detached)
        {
            Focus(index, bringIntoView: false);
        }
    }

    /// <summary>[S-2004] A click on a row is the row's (it focused it): it never reaches the card.</summary>
    private static void OnRowTapped(object sender, TappedRoutedEventArgs args) => args.Handled = true;

    /// <summary>
    /// One list row (<see cref="RowBlock"/>): a 16 lead (an adapter's kind, a program's own icon,
    /// [S-2416], [S-2433]) where the list has any, the name, its figure at the end, a second figure on
    /// the second line and a detail on the third, as the tier allows.
    /// </summary>
    private sealed class ListRow
    {
        private readonly FontIcon _symbol = Family.Glyph(Glyphs.Program);
        private readonly Image _image = new() { Width = FamilyShape.Icon, Height = FamilyShape.Icon, Stretch = Stretch.Uniform };
        private readonly Grid _icon;
        private string? _iconPath;
        private LogoTheme _iconTheme;
        private DetailRow? _shown;
        private int _lines;
        private bool _available = true;

        public ListRow()
        {
            _icon = new Grid { Width = FamilyShape.Icon, Height = FamilyShape.Icon, Children = { _symbol, _image } };
            Block = new RowBlock(RowKind.List) { Interactive = true };
            Block.Lead(_icon);

            // [S-814] A row is an item: a press on it is not a press on the background.
            WidgetItems.Mark(Block);
        }

        public RowBlock Block { get; }

        public void Show(DetailRow row, int lines, bool lead, bool available)
        {
            // [S-6332] Rows are reused by position: one showing this row already is not touched.
            if (_shown == row && _lines == lines && _available == available)
            {
                return;
            }

            _shown = row;
            _lines = lines;
            _available = available;
            Block.Show(row.Name, row.Value, row.Second, row.Detail);
            Block.Lines = lines;
            Block.Available = available && row.Value != "--";
            _icon.Visibility = lead ? Visibility.Visible : Visibility.Collapsed;
            ShowIcon(row);
            ToolTipService.SetToolTip(Block, row.Detail is null ? null : $"{row.Name} · {row.Detail}");
        }

        /// <summary>
        /// [S-2433] The row's symbol at once, and the file's own icon over it once it is extracted —
        /// asked only when the row shows another file than before (rows are reused by position and a
        /// list reorders every read), cached by path and size in <see cref="AppIcons"/>.
        /// </summary>
        private void ShowIcon(DetailRow row)
        {
            _symbol.Glyph = Glyph(row.Symbol);

            // [S-3965] A packaged app's logo is chosen per theme, so a theme change asks again.
            if (string.Equals(_iconPath, row.IconPath, StringComparison.OrdinalIgnoreCase) && _iconTheme == AppIconSource.ThemeOf(Block))
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
            var scale = Block.XamlRoot?.RasterizationScale ?? 2;
            var theme = AppIconSource.ThemeOf(Block);
            _iconTheme = theme;
            var image = await AppIconSource.GetAsync(path, (int)Math.Ceiling(FamilyShape.Icon * Math.Max(1, scale)), theme).ConfigureAwait(true);

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
}
