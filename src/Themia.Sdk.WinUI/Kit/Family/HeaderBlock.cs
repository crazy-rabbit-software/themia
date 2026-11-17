using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6710] [S-9601] The Header block, the family's header look: [glyph 16] 8 [name, BodyStrong, secondary,
/// ellipsis], one line of 20, and an optional caption under it (Caption, tertiary). It is the Header widget's
/// ([S-9600]) and nobody else's: no card has a header ([S-12480]). It lies at the start, the middle or the
/// end of its box; a glyph left empty takes no room.
/// </summary>
public sealed partial class HeaderBlock : StackPanel
{
    private readonly FontIcon _glyph = Family.Glyph(string.Empty);
    private readonly TextBlock _title = new();
    private readonly TextBlock _caption = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, string.Empty);
    private readonly Grid _line;

    public HeaderBlock()
    {
        _glyph.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);
        _glyph.VerticalAlignment = VerticalAlignment.Center;

        Family.Step(_title, FamilyType.BodyStrong);
        _title.Foreground = Family.Brush(ColourRole.TextSecondary);
        _title.VerticalAlignment = VerticalAlignment.Center;

        // [S-640] A name that does not fit ends in an ellipsis: a Grid does not clip a child to its cell.
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        _title.TextWrapping = TextWrapping.NoWrap;
        _caption.TextTrimming = TextTrimming.CharacterEllipsis;
        _caption.TextWrapping = TextWrapping.NoWrap;

        _line = new Grid
        {
            Height = FamilySpace.HeaderLine,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Children = { _glyph, _title },
        };
        Grid.SetColumn(_title, 1);
        Children.Add(_line);
        Children.Add(_caption);
        Show(string.Empty, null, null, HorizontalAlignment.Left);
    }

    /// <summary>
    /// Show <paramref name="title"/> with <paramref name="glyph"/> before it (none: no room for one) and
    /// <paramref name="caption"/> under it (none: no line), the lot at <paramref name="align"/>.
    /// </summary>
    public void Show(string title, string? glyph, string? caption, HorizontalAlignment align)
    {
        _title.Text = title ?? string.Empty;
        _glyph.Glyph = glyph ?? string.Empty;
        _glyph.Visibility = string.IsNullOrEmpty(glyph) ? Visibility.Collapsed : Visibility.Visible;
        _caption.Text = caption ?? string.Empty;
        _caption.Visibility = string.IsNullOrWhiteSpace(caption) ? Visibility.Collapsed : Visibility.Visible;

        // Centred or at the end the line is as wide as its words; at the start the name takes the rest, so it can end in its ellipsis.
        _line.ColumnDefinitions[1].Width = align == HorizontalAlignment.Left ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        _line.HorizontalAlignment = align;
        _caption.HorizontalAlignment = align;
        _caption.TextAlignment = align switch
        {
            HorizontalAlignment.Center => TextAlignment.Center,
            HorizontalAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left,
        };
    }
}
