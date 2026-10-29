using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6711] The Row block: [lead] 8 [name, Body] 12 [inline meter] 12 [value, Body, end-aligned], a
/// second line in Caption secondary and a third in Caption tertiary under the name; 32, 48 or 64
/// tall by its lines ([S-6712]), never by the tier. What is read is primary (<see cref="RowRules.Roles"/>):
/// a fact's value, a list item's name. The row carries the card's side inset as its own padding, so
/// it and its hover reach both edges and its name stands at the widget's one text edge (40 with a
/// 16 lead, 16 without). No divider. Hover and selection are the item layer's (<see cref="ItemFill"/>)
/// when the row is <see cref="Interactive"/>.
/// </summary>
public sealed partial class RowBlock : Grid
{
    private readonly Border _lead;
    private readonly ContentPresenter _meter;
    private bool _interactive;
    private RowKind _kind;
    private int _lines = 1;
    private bool _strong;

    public RowBlock(RowKind kind = RowKind.List, double lead = FamilyShape.Icon)
    {
        _kind = kind;

        _lead = new Border
        {
            Width = lead,
            Height = lead,
            Margin = new Thickness(0, 0, lead > 0 ? FamilySpace.GlyphGap : 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = lead > 0 ? Visibility.Visible : Visibility.Collapsed,
        };

        NameText = Family.Text(FamilyType.Body);
        Second = Family.Text(FamilyType.Caption);
        Third = Family.Text(FamilyType.Caption);
        ValueText = Family.Text(FamilyType.Body);
        ValueText.TextAlignment = TextAlignment.Right;
        ValueText.VerticalAlignment = VerticalAlignment.Center;
        ValueText.Margin = new Thickness(FamilySpace.ColumnGap, 0, 0, 0);

        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { NameText, Second, Third } };
        _meter = new ContentPresenter { VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };

        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        SetColumn(lines, 1);
        SetColumn(_meter, 2);
        SetColumn(ValueText, 3);
        Children.Add(_lead);
        Children.Add(lines);
        Children.Add(_meter);
        Children.Add(ValueText);

        Padding = Family.SideInset;
        Background = Family.Brush(ColourRole.Clear);
        Apply();
    }

    /// <summary>The name (the first line).</summary>
    public TextBlock NameText { get; }

    /// <summary>The value at the row's end.</summary>
    public TextBlock ValueText { get; }

    /// <summary>The second line (Caption, secondary).</summary>
    public TextBlock Second { get; }

    /// <summary>The third line (Caption, tertiary).</summary>
    public TextBlock Third { get; }

    /// <summary>Fill the row's texts; a value, second or third line that is null or empty takes no room.</summary>
    public void Show(string name, string? value = null, string? second = null, string? third = null)
    {
        NameText.Text = name;
        ValueText.Text = value ?? string.Empty;
        ValueText.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        Second.Text = second ?? string.Empty;
        Third.Text = third ?? string.Empty;
        Apply();
    }

    /// <summary>
    /// [S-6712] How many lines the row shows (1 to 3, by <see cref="RowRules.Lines"/>): its height
    /// follows, 32, 48 or 64, and the lines it does not show take no room.
    /// </summary>
    public int Lines
    {
        get => _lines;
        set
        {
            _lines = Math.Clamp(value, 1, 3);
            Apply();
        }
    }

    /// <summary>Whether the row's text is a fact's (value primary) or a list item's (name primary).</summary>
    public RowKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            Apply();
        }
    }

    /// <summary>The one emphasised row of a list (today, unread): its name in BodyStrong.</summary>
    public bool Strong
    {
        get => _strong;
        set
        {
            _strong = value;
            Apply();
        }
    }

    /// <summary>Lead the row with a 16 glyph in the lead's role.</summary>
    public void LeadGlyph(string glyph)
        => _lead.Child = Family.Glyph(glyph, _lead.Width, RowRules.Roles(_kind).Lead);

    /// <summary>Lead the row with an element of the lead's size (an avatar, an app or file icon, a check).</summary>
    public void Lead(FrameworkElement element) => _lead.Child = element;

    /// <summary>An inline meter between the name and the value (a <see cref="ChartBar"/>, 12 either side).</summary>
    public void Meter(FrameworkElement? meter, double width)
    {
        _meter.Content = meter;
        _meter.Width = width;
        _meter.Margin = new Thickness(FamilySpace.ColumnGap, 0, 0, 0);
        _meter.Visibility = meter is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>A row the pointer lights and the user selects: the item layer's row fill, hover included.</summary>
    public bool Interactive
    {
        get => _interactive;
        set
        {
            if (value == _interactive)
            {
                return;
            }

            _interactive = value;
            if (value)
            {
                ItemFill.Paint(this, ItemRole.Row);
                PointerEntered += OnEntered;
                PointerExited += OnGone;
                PointerCaptureLost += OnGone;
                PointerCanceled += OnGone;
            }
            else
            {
                PointerEntered -= OnEntered;
                PointerExited -= OnGone;
                PointerCaptureLost -= OnGone;
                PointerCanceled -= OnGone;
                Background = Family.Brush(ColourRole.Clear);
            }
        }
    }

    // Four events, as Fluent.Hover: PointerExited alone strands a row lit when the list scrolls under it.
    private void OnEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args) => ItemFill.Paint(this, ItemRole.Row, ItemState.Hover);

    private void OnGone(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args) => ItemFill.Paint(this, ItemRole.Row);

    /// <summary>Unhook the hover, if any.</summary>
    public void Detach() => Interactive = false;

    private void Apply()
    {
        var roles = RowRules.Roles(_kind);
        Family.Step(NameText, _strong ? FamilyType.BodyStrong : FamilyType.Body);
        NameText.Foreground = Family.Brush(roles.Name);
        ValueText.Foreground = Family.Brush(roles.Value);
        Second.Foreground = Family.Brush(roles.Second);
        Third.Foreground = Family.Brush(roles.Third);
        Second.Visibility = _lines >= 2 && Second.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        Third.Visibility = _lines >= 3 && Third.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        Height = FamilyShape.RowHeight(_lines);
    }
}

/// <summary>
/// [S-6713] <c>Row.Cell</c>: one item of a grid (Shortcuts', a folder's icons) — its lead (32, or 48 on
/// an expanded tile, <see cref="RowRules.CellIcon"/>) over its name in Caption, primary, centred,
/// 4 apart. Cells stand 12 apart; the grid that lays them out is the widget's.
/// </summary>
public sealed partial class RowCell : StackPanel
{
    private readonly Border _lead;

    public RowCell(double icon = FamilyShape.IconLarge)
    {
        _lead = new Border { Width = icon, Height = icon, HorizontalAlignment = HorizontalAlignment.Center };
        NameText = Family.Text(FamilyType.Caption, ColourRole.TextPrimary);
        NameText.HorizontalAlignment = HorizontalAlignment.Center;
        NameText.TextAlignment = TextAlignment.Center;
        Spacing = FamilySpace.S1;
        Children.Add(_lead);
        Children.Add(NameText);
    }

    /// <summary>The cell's name; empty hides it (a cell on a tile too small for names).</summary>
    public TextBlock NameText { get; }

    /// <summary>The lead's size.</summary>
    public double Icon
    {
        get => _lead.Width;
        set
        {
            _lead.Width = value;
            _lead.Height = value;
        }
    }

    /// <summary>Fill the cell: its lead element and name.</summary>
    public void Show(FrameworkElement lead, string name)
    {
        _lead.Child = lead;
        NameText.Text = name;
        NameText.Visibility = name.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Lead the cell with a glyph at its size.</summary>
    public void Show(string glyph, string name) => Show(Family.Glyph(glyph, _lead.Width, ColourRole.TextSecondary), name);
}
