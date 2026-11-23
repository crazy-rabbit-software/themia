using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Sdk.WinUI.Items;

/// <summary>[S-16954] What every item of a draw shares: the parts left out, the parts' order, what the list draws, a table's columns.</summary>
/// <param name="Hidden">The parts the user left out.</param>
/// <param name="Shape">The parts the list draws (its items' one size follows it).</param>
/// <param name="Stock">Drives wear their kinds' stock icons (a render's).</param>
/// <param name="Columns">A table's columns, every row's the same; empty in the other arrangements.</param>
/// <param name="Inner">A table row's width inside its side insets: its columns' room.</param>
public sealed record ItemDraw(IReadOnlyCollection<ItemPart> Hidden, ItemShape Shape, bool Stock = false, IReadOnlyList<ItemColumn>? Columns = null, double Inner = 0)
{
    /// <summary>Whether <paramref name="item"/> draws <paramref name="part"/>: it has one and the user did not leave it out.</summary>
    public bool Draws(Item item, ItemPart part) => item.Has(part) && !Hidden.Contains(part);
}

/// <summary>
/// [S-16940] [S-16631] One item of the item base, in its arrangement's face (owner, 2026-11-23: "they have to share the base ...
/// it is just so that they look as similar as possible"). <b>List</b>: in the card's inset its lead (36: a drive's own picture,
/// a glyph at 28), 8, its title (Body, primary, left-aligned, cut with its ellipsis) with its subtitle right under it
/// (Caption, secondary), 12, its end (a reading at 28, a text, a button) at the item's end; 12 under that row the bar across
/// it, the inset under the bar. <b>Column list</b>: the same in a compact row (a 16 lead, the end in Body), the bar under it.
/// <b>Row list</b> and <b>Grid</b>: a column of its parts, centred: the title, the 32 lead, the end, the bar (standing up in a
/// row list), the subtitle, the note. <b>Table</b>: each part in its column. A part the item has none of, or the user left
/// out ([S-16670]), takes no room and is not drawn: the others close up. The item is its box edge to edge (WR-3): the list
/// lights it under the pointer.
/// </summary>
public sealed partial class ItemView : Grid
{
    private readonly ItemStyle _style;
    private readonly ItemLeadSlot _lead;
    private readonly TextBlock _title;
    private readonly TextBlock _subtitle;
    private readonly TextBlock _note;
    private readonly ReadoutBlock? _reading;
    private readonly TextBlock _end;
    private readonly TextBlock _second;
    private readonly StackPanel _ends;
    private readonly Button _action;
    private readonly ChartBar _bar;
    private readonly StackPanel? _texts;

    public ItemView(IWidgetHost host, ItemStyle style)
    {
        ArgumentNullException.ThrowIfNull(host);
        _style = style;
        Background = Family.Brush(ColourRole.Clear);

        // [S-12831] An item of its widget: the layout dump and the item check see its box.
        ItemMark.Mark(this);

        var vertical = style is ItemStyle.RowList or ItemStyle.Grid;
        var compact = style is ItemStyle.ColumnList or ItemStyle.Table;
        var (side, glyph) = style switch
        {
            ItemStyle.List => (FamilyType.Big28.LineHeight, FamilyType.Big28.FontSize),
            _ when vertical => (FamilyShape.IconLarge, FamilyShape.IconLarge),
            _ => (FamilyShape.Icon, FamilyShape.Icon),
        };
        _lead = new ItemLeadSlot(host, side, glyph);

        _title = Line(FamilyType.Body, ColourRole.TextPrimary);
        _subtitle = Line(vertical || style == ItemStyle.Table ? FamilyType.Body : FamilyType.Caption, ColourRole.TextSecondary);
        _note = Line(FamilyType.Caption, ColourRole.TextSecondary);
        _end = Line(FamilyType.Body, ColourRole.TextSecondary);
        _second = Line(FamilyType.Body, ColourRole.TextSecondary);
        _end.TextTrimming = TextTrimming.None;
        _second.TextTrimming = TextTrimming.None;
        Family.Figures(_end);
        Family.Figures(_second);
        _ends = new StackPanel { Orientation = Orientation.Horizontal, Spacing = FamilySpace.S2, VerticalAlignment = VerticalAlignment.Center, Children = { _end, _second } };
        _action = ControlsBlock.TextButton(string.Empty, main: true, () => Acted?.Invoke(this));
        _action.VerticalAlignment = VerticalAlignment.Center;
        _bar = new ChartBar(vertical: style == ItemStyle.RowList);

        if (style == ItemStyle.List)
        {
            _reading = new ReadoutBlock { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            _reading.Use(FamilyType.Big28);
        }

        if (vertical)
        {
            BuildStack();
        }
        else if (style == ItemStyle.Table)
        {
            Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, 0);
            foreach (var part in new FrameworkElement[] { _lead, _title, _subtitle, _note, _ends, _action, _bar })
            {
                part.VerticalAlignment = VerticalAlignment.Center;
                Children.Add(part);
            }

            _bar.HorizontalAlignment = HorizontalAlignment.Stretch;
            _ends.HorizontalAlignment = HorizontalAlignment.Right;
            _action.HorizontalAlignment = HorizontalAlignment.Right;
        }
        else
        {
            _texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _title, _subtitle } };
            BuildRow(compact);
        }
    }

    /// <summary>The item it shows; null before the first.</summary>
    public Item? Item { get; private set; }

    /// <summary>The lead's slot (a widget's own picture is hooked on it).</summary>
    public ItemLeadSlot Lead => _lead;

    /// <summary>[S-16948] The end's button was pressed (Join).</summary>
    public event Action<ItemView>? Acted;

    /// <summary>A text line of the item: one line, cut with its ellipsis.</summary>
    private static TextBlock Line(TypeStep step, ColourRole role)
    {
        var text = Family.Text(step, role);
        text.TextWrapping = TextWrapping.NoWrap;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.MaxLines = 1;
        return text;
    }

    /// <summary>[S-16631] [S-16943] The row faces: the lead, the texts, the end; the bar under them.</summary>
    private void BuildRow(bool compact)
    {
        Padding = compact
            ? new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, 0)
            : new Thickness(FamilySpace.Inset, FamilySpace.ContentTop, FamilySpace.Inset, 0);
        _lead.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);
        _ends.Margin = new Thickness(FamilySpace.S3, 0, 0, 0);
        _action.Margin = new Thickness(FamilySpace.S3, 0, 0, 0);
        _bar.Margin = new Thickness(0, compact ? 0 : FamilySpace.S3, 0, 0);

        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Place(_lead, 0, 0);
        Place(_texts!, 0, 1);
        if (_reading is not null)
        {
            _reading.Margin = new Thickness(FamilySpace.S3, 0, 0, 0);
            Place(_reading, 0, 2);
        }

        Place(_ends, 0, 2);
        Place(_action, 0, 2);
        Place(_bar, 1, 0, 3);
    }

    /// <summary>[S-16942] [S-16944] The column faces: the parts stacked and centred; a row list's bar standing up in what is left.</summary>
    private void BuildStack()
    {
        var standing = _style == ItemStyle.RowList;
        Padding = standing
            ? new Thickness(FamilySpace.S1, FamilySpace.ContentTop, FamilySpace.S1, FamilySpace.Inset)
            : new Thickness(FamilySpace.S2, FamilySpace.S2, FamilySpace.S2, FamilySpace.S2);
        RowSpacing = FamilySpace.S1;
        if (!standing)
        {
            VerticalAlignment = VerticalAlignment.Stretch;
        }

        _title.TextAlignment = TextAlignment.Center;
        _subtitle.TextAlignment = TextAlignment.Center;
        _note.TextAlignment = TextAlignment.Center;
        _ends.HorizontalAlignment = HorizontalAlignment.Center;
        _action.HorizontalAlignment = HorizontalAlignment.Center;
        _lead.HorizontalAlignment = HorizontalAlignment.Center;
        _bar.HorizontalAlignment = standing ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        _bar.Margin = standing ? default : new Thickness(FamilySpace.S2, 0, FamilySpace.S2, 0);

        // Without a standing bar the parts keep together in the card's middle.
        var parts = new FrameworkElement[] { _title, _lead, _ends, _action, _bar, _subtitle, _note };
        for (var i = 0; i < parts.Length; i++)
        {
            var stretch = standing && ReferenceEquals(parts[i], _bar);
            RowDefinitions.Add(new RowDefinition { Height = stretch ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            Place(parts[i], i, 0);
        }

        if (!standing)
        {
            RowDefinitions.Insert(0, new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            foreach (var part in parts)
            {
                SetRow(part, GetRow(part) + 1);
            }
        }
    }

    /// <summary>
    /// [S-16631] Show <paramref name="item"/> without the parts <paramref name="draw"/> leaves out; what is the same as before
    /// is not touched (a drive's picture is asked once).
    /// </summary>
    public void Show(Item item, ItemDraw draw)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(draw);
        Item = item;

        if (item.Lead is { } lead && !draw.Hidden.Contains(ItemPart.Lead))
        {
            _lead.Visibility = Visibility.Visible;
            _lead.Show(lead, draw.Stock);
        }
        else
        {
            // [S-16590] A table's lead column keeps its width on a row that has none.
            _lead.Visibility = _style == ItemStyle.Table ? Visibility.Visible : Visibility.Collapsed;
            if (_style == ItemStyle.Table)
            {
                _lead.Show(new ItemLead(string.Empty), draw.Stock);
            }
        }

        _title.Text = item.Title;
        Family.Step(_title, item.Strong ? FamilyType.BodyStrong : FamilyType.Body);
        _title.Foreground = Family.Brush(item.Muted ? ColourRole.TextSecondary : ColourRole.TextPrimary);
        _title.Visibility = Shown(draw.Draws(item, ItemPart.Title));

        // The row faces carry the note on the subtitle's line; the columns and the table draw it on its own.
        var rowFace = _style is ItemStyle.List or ItemStyle.ColumnList;
        var note = draw.Draws(item, ItemPart.Note) ? item.Note : string.Empty;
        var subtitle = draw.Draws(item, ItemPart.Subtitle) ? item.Subtitle : string.Empty;
        _subtitle.Text = rowFace && note.Length > 0 ? (subtitle.Length > 0 ? $"{subtitle} · {note}" : note) : subtitle;
        _subtitle.Visibility = Shown(_subtitle.Text.Length > 0);
        _note.Text = note;
        _note.Visibility = Shown(!rowFace && note.Length > 0);

        ShowEnd(item.End is { } end && !draw.Hidden.Contains(ItemPart.End) ? end : null);

        if (item.Bar is { } bar && !draw.Hidden.Contains(ItemPart.Bar))
        {
            _bar.Visibility = Visibility.Visible;
            _bar.Show(Math.Clamp(bar.To, 0, 1), Math.Clamp(bar.From, 0, 1));
            _bar.Role(bar.Role);
        }
        else
        {
            _bar.Visibility = Visibility.Collapsed;
        }

        if (_style == ItemStyle.Table && draw.Columns is { } columns)
        {
            Columns(columns, draw.Inner);
        }

        ToolTipService.SetToolTip(this, item.Tooltip.Length > 0 ? item.Tooltip : null);
    }

    /// <summary>[S-16948] The end slot: a reading (at 28 in a List item, Body elsewhere), a text, or the action's button.</summary>
    private void ShowEnd(ItemEnd? end)
    {
        _action.Visibility = Visibility.Collapsed;
        _ends.Visibility = Visibility.Collapsed;
        if (_reading is not null)
        {
            _reading.Visibility = Visibility.Collapsed;
        }

        switch (end)
        {
            case null:
                return;

            case { Kind: ItemEndKind.Action }:
                _action.Visibility = Visibility.Visible;
                if (_action.Content is TextBlock label)
                {
                    label.Text = end.Value;
                }
                else
                {
                    _action.Content = end.Value;
                }

                return;

            case { Kind: ItemEndKind.Reading } when _reading is not null:
                _reading.Visibility = Visibility.Visible;
                _reading.Show(end.Value, end.Unit);
                return;

            default:
                _ends.Visibility = Visibility.Visible;
                _end.Text = end.Kind == ItemEndKind.Reading ? end.Value + end.Unit : end.Value;
                _end.Foreground = Family.Brush(end.Kind == ItemEndKind.Reading ? ColourRole.TextPrimary : end.Role);
                _second.Text = end.Second;
                _second.Visibility = Shown(end.Second.Length > 0);
                return;
        }
    }

    /// <summary>[S-16956] A table row: each part shown in its column, every row's the same; the end on its column's end.</summary>
    private void Columns(IReadOnlyList<ItemColumn> columns, double inner)
    {
        FrameworkElement[] all = [_lead, _title, _subtitle, _note, _ends, _action, _bar];
        var placed = new HashSet<FrameworkElement>();
        foreach (var column in columns)
        {
            FrameworkElement[] parts = column.Part switch
            {
                ItemPart.Lead => [_lead],
                ItemPart.Title => [_title],
                ItemPart.Subtitle => [_subtitle],
                ItemPart.Note => [_note],
                ItemPart.End => [_ends, _action],
                _ => [_bar],
            };
            foreach (var part in parts)
            {
                part.Margin = new Thickness(column.X, 0, Math.Max(0, inner - column.Right), 0);
                part.HorizontalAlignment = column.Part switch
                {
                    ItemPart.End => HorizontalAlignment.Right,
                    ItemPart.Lead => HorizontalAlignment.Left,
                    _ => HorizontalAlignment.Stretch,
                };
                placed.Add(part);
            }
        }

        foreach (var part in all)
        {
            if (!placed.Contains(part))
            {
                part.Visibility = Visibility.Collapsed;
            }
        }
    }

    private static Visibility Shown(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;

    private void Place(FrameworkElement element, int row, int column, int span = 1)
    {
        SetRow(element, row);
        SetColumn(element, column);
        SetColumnSpan(element, span);
        Children.Add(element);
    }
}
