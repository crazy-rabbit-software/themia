using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7034] The table look: one row of 28 in the Row block's tokens — the lead, then the user's columns 12 apart at the
/// widths the plan gave them, the title primary (BodyStrong when emphasised), the rest secondary, the time end-aligned
/// in its tone; their titles in Caption over them. The keyboard's ring is a Row block's (a table row is one with its
/// cells in the name's place, <see cref="RowBlock.Cells"/>).
/// </summary>
public sealed class KitTableLook : IKitLook
{
    public RowBlock Draw(KitItem item, KitDraw draw)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(draw);

        var plan = draw.Plan;
        var spec = draw.Spec;
        var columns = plan.Table ?? [];
        var block = new RowBlock(RowKind.List, plan.Lead ? FamilyShape.Icon : 0) { Lines = 1, Height = draw.Height };
        block.Show(string.Empty);

        var grid = ColumnGrid(columns);
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i].Column;
            var cell = Family.Text(column == KitColumn.Title && item.Emphasis ? FamilyType.BodyStrong : FamilyType.Body, column == KitColumn.Title && !(spec.Unread && !item.Emphasis) ? ColourRole.TextPrimary : ColourRole.TextSecondary);
            cell.VerticalAlignment = VerticalAlignment.Center;
            cell.Text = column switch
            {
                KitColumn.Title => draw.Title(item),
                KitColumn.Subtitle => item.Subtitle ?? string.Empty,
                KitColumn.Preview => item.Preview ?? string.Empty,
                KitColumn.Badge => KitDraw.Badge(item) ?? string.Empty,
                _ => item.Value ?? draw.When(item.When, spec.ColumnTime),
            };

            if (column == KitColumn.When)
            {
                cell.TextAlignment = TextAlignment.Right;
                cell.HorizontalAlignment = HorizontalAlignment.Right;
                cell.Foreground = Family.Brush(KitDraw.Tone(item.Tone, ColourRole.TextSecondary));
                if (item.Value is null)
                {
                    draw.Times(cell, item.When, spec.ColumnTime);
                }
            }

            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        if (plan.Lead && draw.Lead(item) is { } lead)
        {
            block.Lead(lead);
        }

        block.Cells(grid);
        return block;
    }

    public bool Titles(Grid titles, KitDraw draw)
    {
        ArgumentNullException.ThrowIfNull(titles);
        ArgumentNullException.ThrowIfNull(draw);

        titles.Children.Clear();
        titles.ColumnDefinitions.Clear();

        var plan = draw.Plan;
        var columns = plan.Table ?? [];
        var template = ColumnGrid(columns);
        foreach (var definition in template.ColumnDefinitions.ToList())
        {
            template.ColumnDefinitions.Remove(definition);
            titles.ColumnDefinitions.Add(definition);
        }

        titles.ColumnSpacing = template.ColumnSpacing;

        // [S-14500] The titles' band ends on a grid line, so the rows under it start on one. [S-12815] Its words stand on the
        // band's foot, right over the first row (WR-1: nothing between the first item and what stands over it).
        titles.Height = KitPlanner.TitlesBand(draw.Grid);
        titles.Padding = new Thickness(FamilySpace.Inset + (plan.Lead ? RowRules.NameStart(FamilyShape.Icon) : 0), 0, FamilySpace.Inset, 0);

        for (var i = 0; i < columns.Count; i++)
        {
            var title = Family.Text(FamilyType.Caption, ColourRole.TextSecondary, draw.Spec.ColumnHeading?.Invoke(columns[i].Column) ?? string.Empty);
            title.VerticalAlignment = VerticalAlignment.Bottom;
            if (columns[i].Column == KitColumn.When)
            {
                title.TextAlignment = TextAlignment.Right;
                title.HorizontalAlignment = HorizontalAlignment.Right;
            }

            Grid.SetColumn(title, i);
            titles.Children.Add(title);
        }

        // Titles over two rows of data are a third of the tile: the plan drops them where there is no room.
        return plan.ColumnHeader;
    }

    /// <summary>The table's columns as a grid: fixed widths, the flexible ones sharing the rest, 12 apart.</summary>
    private static Grid ColumnGrid(IReadOnlyList<KitColumnFit> columns)
    {
        var grid = new Grid { ColumnSpacing = KitPlanner.ColumnGap };

        foreach (var column in columns)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = column.Star > 0 ? new GridLength(column.Star, GridUnitType.Star) : new GridLength(column.Width),
            });
        }

        return grid;
    }
}
