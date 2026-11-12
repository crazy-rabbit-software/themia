using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7031] [S-7032] [S-7034] The kit's rows as the family's Row block ([S-6711]): the item's title as
/// the name (BodyStrong for the emphasised one: unread, today; a read row of a list that tells unread by
/// its names in the secondary role, [S-2270]), the time as the value (secondary, or
/// its tone's Caution or Critical; never the accent), the subtitle (and the badge after it) on the
/// second line, the preview on the third, the lead a 16 glyph or the site's icon; the lines the plan
/// chose ([S-6712]). The table look is the same row's tokens laid out in the user's columns, their
/// titles in Caption over them. Each row sits in a full-width item (the item layer's fill and hover,
/// [S-6513]) that carries its tag, menu and pointer.
/// </summary>
public sealed partial class KitList
{
    /// <summary>[S-7034] The table's column titles over its rows.</summary>
    private readonly Grid _columnHeader = new();

    /// <summary>Each drawn time, so the ages are rewritten without rebuilding the rows.</summary>
    private readonly List<(TextBlock Text, KitWhen? When, KitTime Time)> _times = [];

    /// <summary>[S-7061] Rewrite every drawn time for <paramref name="now"/> (the ages on the ticker).</summary>
    public void Retime(DateTimeOffset now)
    {
        foreach (var (text, when, time) in _times)
        {
            text.Text = KitText.When(Strings.Current, when, time, now);
        }
    }

    /// <summary>[S-1288] The keyboard's ring on a row (the Row block's own, over its whole width).</summary>
    private void Mark(int index, bool focused)
    {
        if (index >= 0 && index < _rowElements.Count && _rowElements[index].Child is RowBlock block)
        {
            block.Focused = focused;
        }
    }

    private string TitleText(KitItem item) => item.Title.Length > 0 ? item.Title : _spec.Untitled();

    private static string? BadgeText(KitItem item) => item.Badges is [{ Text: { Length: > 0 } text }, ..] ? text : null;

    /// <summary>A text's colour for a tone: Caution and Critical say a state; every other tone is the plain role.</summary>
    private static ColourRole ToneRole(KitTone tone, ColourRole plain) => tone switch
    {
        KitTone.Caution => ColourRole.Caution,
        KitTone.Critical => ColourRole.Critical,
        _ => plain,
    };

    private Border BuildFamilyRow(KitItem item, int index, DateTimeOffset now)
    {
        FrameworkElement content;
        if (_plan.Look == KitRowLook.Table)
        {
            content = TableRow(item, now);
        }
        else
        {
            var block = new RowBlock(RowKind.List, _plan.Lead ? FamilyShape.Icon : 0) { Lines = _plan.Lines, Strong = item.Emphasis, Quiet = _spec.Unread && !item.Emphasis };
            var second = _plan.Subtitle ? SubtitleLine(item) : _plan.Preview ? item.Preview : null;
            var third = _plan.Subtitle && _plan.Preview ? item.Preview : null;
            // [S-7072] A reading stands in the time's place, and is not rewritten on the ticker.
            var when = _plan.When ? item.Value ?? KitText.When(Strings.Current, item.When, _spec.Time, now) : null;
            block.Show(TitleText(item), when, second, third);
            block.ValueText.Foreground = Family.Brush(ToneRole(item.Tone, ColourRole.TextSecondary));

            if (_plan.When && item.Value is null)
            {
                _times.Add((block.ValueText, item.When, _spec.Time));
            }

            if (_plan.Lead && Lead(item) is { } lead)
            {
                block.Lead(lead);
            }

            content = block;
        }

        return Item(content, item, index);
    }

    /// <summary>The second line: the subtitle, the first badge after it where the plan has room.</summary>
    private string? SubtitleLine(KitItem item)
    {
        var badge = _plan.Badges ? BadgeText(item) : null;
        var subtitle = item.Subtitle is { Length: > 0 } s ? s : null;
        return (subtitle, badge) switch
        {
            ({ } a, { } b) => $"{a} · {b}",
            ({ } a, null) => a,
            (null, { } b) => b,
            _ => null,
        };
    }

    /// <summary>The row's lead, or null when it has none to draw.</summary>
    private FrameworkElement? Lead(KitItem item) => item.Lead.Kind switch
    {
        KitLeadKind.SiteIcon when item.Lead.Value is { Length: > 0 } link => SiteIcon(link),
        KitLeadKind.Icon when Enum.TryParse<KitIcon>(item.Lead.Value, out var icon) => Family.Glyph(KitIcons.Glyph(icon), FamilyShape.Icon, ToneRole(item.Lead.Tone, ColourRole.TextSecondary)),
        _ => null,
    };

    /// <summary>A full-width item around a row: the item layer's fill and hover, its tag, menu, tooltip and pointer.</summary>
    private Border Item(FrameworkElement content, KitItem item, int index)
    {
        var row = new Border { Child = content, Tag = index };
        ItemFill.Paint(row, ItemRole.Row);
        WidgetItems.Mark(row);
        _rowHooks.Add(Fluent.Hover(row));
        _rowHooks.Add(ItemMenu.Attach(row, () => Entries(item)));

        if ((item.Tooltip ?? item.Link?.OriginalString) is { } tip)
        {
            ToolTipService.SetToolTip(row, tip);
        }

        row.Tapped += OnRowTapped;
        row.DoubleTapped += OnRowDoubleTapped;
        return row;
    }

    // ---- the table ([S-7034]) -------------------------------------------------------------------

    /// <summary>
    /// [S-7034] A table row: one row of 28 in the Row block's tokens — the lead, then the user's
    /// columns 12 apart at the widths the plan gave them, the title primary (BodyStrong when
    /// emphasised), the rest secondary, the time end-aligned in its tone. The keyboard's ring is a Row
    /// block's (a table row is one with its cells in the name's place, RowBlock.Cells).
    /// </summary>
    private RowBlock TableRow(KitItem item, DateTimeOffset now)
    {
        var columns = _plan.Table ?? [];
        var block = new RowBlock(RowKind.List, _plan.Lead ? FamilyShape.Icon : 0) { Lines = 1 };
        block.Show(string.Empty);

        var grid = ColumnGrid(columns);
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i].Column;
            var cell = Family.Text(column == KitColumn.Title && item.Emphasis ? FamilyType.BodyStrong : FamilyType.Body, column == KitColumn.Title && !(_spec.Unread && !item.Emphasis) ? ColourRole.TextPrimary : ColourRole.TextSecondary);
            cell.VerticalAlignment = VerticalAlignment.Center;
            cell.Text = column switch
            {
                KitColumn.Title => TitleText(item),
                KitColumn.Subtitle => item.Subtitle ?? string.Empty,
                KitColumn.Preview => item.Preview ?? string.Empty,
                KitColumn.Badge => BadgeText(item) ?? string.Empty,
                _ => item.Value ?? KitText.When(Strings.Current, item.When, _spec.ColumnTime, now),
            };

            if (column == KitColumn.When)
            {
                cell.TextAlignment = TextAlignment.Right;
                cell.HorizontalAlignment = HorizontalAlignment.Right;
                cell.Foreground = Family.Brush(ToneRole(item.Tone, ColourRole.TextSecondary));
                if (item.Value is null)
                {
                    _times.Add((cell, item.When, _spec.ColumnTime));
                }
            }

            SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        if (_plan.Lead && Lead(item) is { } lead)
        {
            block.Lead(lead);
        }

        block.Cells(grid);
        return block;
    }

    private void BuildColumnHeader()
    {
        _columnHeader.Children.Clear();
        _columnHeader.ColumnDefinitions.Clear();

        var columns = _plan.Table ?? [];
        var template = ColumnGrid(columns);
        foreach (var definition in template.ColumnDefinitions.ToList())
        {
            template.ColumnDefinitions.Remove(definition);
            _columnHeader.ColumnDefinitions.Add(definition);
        }

        _columnHeader.ColumnSpacing = template.ColumnSpacing;
        _columnHeader.Height = KitLadder.ColumnHeaderHeight;
        _columnHeader.Padding = new Thickness(FamilySpace.Inset + (_plan.Lead ? RowRules.NameStart(FamilyShape.Icon) : 0), 0, FamilySpace.Inset, 0);

        for (var i = 0; i < columns.Count; i++)
        {
            var title = Family.Text(FamilyType.Caption, ColourRole.TextSecondary, _spec.ColumnHeading?.Invoke(columns[i].Column) ?? string.Empty);
            title.VerticalAlignment = VerticalAlignment.Top;
            if (columns[i].Column == KitColumn.When)
            {
                title.TextAlignment = TextAlignment.Right;
                title.HorizontalAlignment = HorizontalAlignment.Right;
            }

            SetColumn(title, i);
            _columnHeader.Children.Add(title);
        }

        // Titles over two rows of data are a third of the tile: the plan drops them where there is no room.
        _columnHeader.Visibility = _plan.ColumnHeader ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The table's columns as a grid: fixed widths, the flexible ones sharing the rest, 12 apart.</summary>
    private static Grid ColumnGrid(IReadOnlyList<KitColumnFit> columns)
    {
        var grid = new Grid { ColumnSpacing = KitLadder.ColumnGap };

        foreach (var column in columns)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = column.Star > 0 ? new GridLength(column.Star, GridUnitType.Star) : new GridLength(column.Width),
            });
        }

        return grid;
    }

    /// <summary>A hook undone on dispose (a handler pair a row's part added).</summary>
    private sealed class Unhook(Action undo) : IDisposable
    {
        private Action? _undo = undo;

        public void Dispose()
        {
            _undo?.Invoke();
            _undo = null;
        }
    }
}
