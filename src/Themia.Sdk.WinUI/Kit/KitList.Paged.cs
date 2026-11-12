using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7035] [S-7036] The paged list and the schedule look on the family's rows. A paged list shows
/// the rows that fit whole, one page at a time, turned by the widget's own controls and keys; it never
/// scrolls. The page flows down its columns, a day's heading (one row of 28, BodyStrong secondary;
/// primary for the day the widget has chosen) over its first row. A schedule row is a Row block: its
/// calendar's colour as a 2 px bar in the lead ([S-566], a data colour, [S-6705]) or its lead glyph,
/// the title as the name, the start as the value ("All day", "Now" Strong), the span, where and who
/// (or the item's note) on the second line where the plan gives two.
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

    /// <summary>A widget's own touch on a drawn row (the calendar's drag of an event).</summary>
    public Action<KitItem, FrameworkElement>? Decorate { get; init; }

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
        }
    }

    private void BuildPage(DateTimeOffset now)
    {
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

        // [S-2716] "Today" and "Tomorrow" carry their date where the heading line holds it whole.
        var dated = KitDays.Dated(Strings.Current, _plan.ColumnWidth - (2 * FamilySpace.Inset));
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
                if (KitPages.HeadingBefore(keys, at, i, heading))
                {
                    stack.Children.Add(Heading(item.Group, today, dated));
                }

                var row = ScheduleRow(item, i);
                stack.Children.Add(row);
                _rowElements[i] = row;
            }

            SetColumn(stack, c);
            _page.Children.Add(stack);
            at += count;
        }
    }

    /// <summary>[S-7035] A day's heading: one row of 28, BodyStrong, secondary (primary for the chosen day).</summary>
    private static Grid Heading(KitGroup? group, DateOnly today, bool dated)
    {
        var text = Family.Text(FamilyType.BodyStrong, group is { Selected: true } ? ColourRole.TextPrimary : ColourRole.TextSecondary);
        text.Text = group?.Heading ?? (group?.Day is { } day ? KitDays.Heading(Strings.Current, day, today, dated) : string.Empty);
        text.VerticalAlignment = VerticalAlignment.Center;

        return new Grid { Children = { text }, Height = FamilyShape.Row1, Padding = Family.SideInset };
    }

    /// <summary>[S-7036] One scheduled item as a Row block.</summary>
    private Border ScheduleRow(KitItem item, int index)
    {
        var schedule = item.Schedule ?? new KitSchedule(null);
        var block = new RowBlock(RowKind.List) { Lines = _plan.Lines, Strong = item.Emphasis };

        string? second;
        if (schedule.Note is { } note)
        {
            second = note;
        }
        else
        {
            second = string.Join(" · ", new[] { schedule.Span, schedule.Where, schedule.Who }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        block.Show(TitleText(item), schedule.Start, second is { Length: > 0 } ? second : null);
        if (schedule.Strong)
        {
            Family.Step(block.ValueText, FamilyType.BodyStrong);
        }

        block.ValueText.Foreground = Family.Brush(ToneRole(item.Tone, ColourRole.TextSecondary));

        // [S-566] The calendar's colour as a 2 px bar at the lead's start; with none, the item's glyph.
        if (schedule.Mark is { } mark)
        {
            block.Lead(new Border
            {
                Width = FamilyShape.Stroke,
                Height = FamilyShape.Icon,
                CornerRadius = Family.Round(FamilyShape.Stroke, FamilyShape.Icon),
                Background = Family.Data(mark),
                HorizontalAlignment = HorizontalAlignment.Left,
            });
        }
        else if (Lead(item) is { } lead)
        {
            block.Lead(lead);
        }

        var row = Item(block, item, index);
        Decorate?.Invoke(item, row);
        return row;
    }
}
