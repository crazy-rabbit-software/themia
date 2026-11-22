using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7035] The paged list: the rows that fit whole, one page at a time, turned by the widget's own controls and keys;
/// it never scrolls. The page flows down its columns, a group's heading (the look's, <see cref="IKitLook.Heading"/>)
/// over its first row. [S-15783] Under a page's last whole row the next page's first stands cut by the card's foot
/// (WR-4: "the last item will be cutoff to some extent then"), never room left empty while more follow.
/// </summary>
public sealed partial class KitList
{
    private readonly Grid _page = new();

    /// <summary>The first item of each page.</summary>
    private IReadOnlyList<int> _pageStarts = [0];

    private int _pageIndex;
    private DateTimeOffset _drawnAt;

    /// <summary>The page on screen; set by the widget's controls, clamped to the pages there are.</summary>
    public int Page
    {
        get => _pageIndex;
        set => _pageIndex = Math.Max(0, value);
    }

    /// <summary>How many pages the items take at this size.</summary>
    public int PageCount => _pageStarts.Count;

    /// <summary>The first item of every page.</summary>
    public IReadOnlyList<int> PageStarts => _pageStarts;

    /// <summary>The first item on the page on screen.</summary>
    public KitItem? FirstOnPage => _pageStarts.Count > 0 && _pageIndex < _pageStarts.Count && _pageStarts[_pageIndex] < _items.Count ? _items[_pageStarts[_pageIndex]] : null;

    /// <summary>A widget's own touch on a drawn row, in every look (an event's links, [S-3623]; a task's day, [S-11925]).</summary>
    public Action<KitItem, FrameworkElement>? Decorate { get; init; }

    /// <summary>A widget's own touch on a drawn day heading of the schedule look (its day's links, [S-11923]).</summary>
    public Action<KitGroup, FrameworkElement>? DecorateHeading { get; init; }

    /// <summary>Called before the rows are drawn again, so a widget forgets what it touched on the old ones.</summary>
    public Action? Redrawing { get; init; }

    private bool Paged => _spec.Overflow == KitOverflow.Page;

    /// <summary>[S-7035] The cursor takes the page with it.</summary>
    private void TurnTo(int index)
    {
        if (!Paged || index < 0 || index >= _items.Count)
        {
            return;
        }

        var page = KitPages.PageOf(_pageStarts, index);
        if (page != _pageIndex)
        {
            _pageIndex = page;
            BuildRows(_drawnAt);
            return;
        }
    }

    private void BuildPage(KitDraw draw)
    {
        var now = draw.Now;
        _drawnAt = now;
        _page.Children.Clear();
        _page.ColumnDefinitions.Clear();

        // Every item has its slot in the rows, so the cursor and the menus find it by place; only the
        // page's rows are drawn.
        for (var i = 0; i < _items.Count; i++)
        {
            _rowElements.Add(new Border());
        }

        if (_items.Count == 0)
        {
            _pageStarts = [0];
            _pageIndex = 0;
            return;
        }

        var today = DateOnly.FromDateTime(now.DateTime);
        var keys = _items.Select(i => i.Group?.Key ?? string.Empty).ToList();
        var box = KitPlanner.ListHeight(_area);
        var firstHeading = KitPages.ShowsFirstHeading(_items[0].Group?.Day, today, _area.Height);

        _pageStarts = KitPages.Pages(keys, _plan.Columns, box, _plan.HeadingHeight, _plan.RowHeight, firstHeading);
        _pageIndex = Math.Clamp(_pageIndex, 0, _pageStarts.Count - 1);

        var at = _pageStarts[_pageIndex];

        for (var c = 0; c < _plan.Columns && at < _items.Count; c++)
        {
            _page.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var heading = firstHeading || at > 0;
            var count = KitPages.Fit(keys, at, box, _plan.HeadingHeight, _plan.RowHeight, heading);
            var stack = new StackPanel();

            for (var i = at; i < at + count; i++)
            {
                var item = _items[i];
                draw.Index = i;
                draw.Line = stack.Children.Count;
                if (KitPages.HeadingBefore(keys, at, i, heading))
                {
                    var group = item.Group ?? new KitGroup(string.Empty);
                    var head = _look.Heading(group, draw);
                    if (item.Group is not null)
                    {
                        DecorateHeading?.Invoke(group, head);
                    }

                    stack.Children.Add(head);
                    draw.Line = stack.Children.Count;
                }

                var row = Item(_look.Draw(item, draw), item, i);
                stack.Children.Add(row);
                _rowElements[i] = row;
            }

            // [S-15783] The last column's room under its whole lines holds the next page's first line, cut by the card's
            // foot: its heading where its group starts there, else its row (the next page's item: a click on it is that item's,
            // the cursor reaching it turns to its page).
            var next = at + count;
            if (c == _plan.Columns - 1 && next < _items.Count && KitPages.Room(box, stack.Children.Count, _plan.RowAt) > LayoutMath.Tolerance)
            {
                var item = _items[next];
                draw.Index = next;
                draw.Line = stack.Children.Count;
                if (KitPages.HeadingBefore(keys, at, next, heading))
                {
                    stack.Children.Add(_look.Heading(item.Group ?? new KitGroup(string.Empty), draw));
                }
                else
                {
                    stack.Children.Add(Item(_look.Draw(item, draw), item, next));
                }
            }

            // [S-14810] Each line of the page its one fixed size on whole device pixels; a page is whole lines, the next one cut by the foot.
            for (var k = 0; k < stack.Children.Count; k++)
            {
                var height = _plan.RowAt(k);
                switch (stack.Children[k])
                {
                    case Border { Child: RowBlock line }:
                        line.Height = height;
                        break;
                    case Grid head:
                        head.Height = height;
                        SectionHeading.Fill(head.Children.OfType<TextBlock>().First(), height);
                        break;
                }
            }

            SetColumn(stack, c);
            _page.Children.Add(stack);
            at += count;
        }
    }
}
