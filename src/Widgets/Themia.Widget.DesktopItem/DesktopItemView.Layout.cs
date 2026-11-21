using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-13970]..[S-13975] The icon and its name in the desktop's one icon layout in force (<see cref="DesktopCell.CurrentLayout"/>,
/// never the icon's own): text below (Explorer's), text beside (the name to the icon's right, from its start) or icon only
/// (the icon centred in its cell; the name, shown while the icon is pointed at, keyboard-focused or renamed, on a plate
/// under the icon drawn over the neighbours: the view's overhang, <see cref="IWidgetOverhang"/>, which its card lifts
/// above every card).
/// </summary>
internal sealed partial class DesktopItemView : IWidgetOverhang
{
    /// <summary>[S-13970] The desktop's icon layout this view is drawn in: the one in force when it last rendered.</summary>
    private DesktopIconLayout _layout = DesktopCell.CurrentLayout;

    /// <summary>[S-13975] What the view draws past its cell, in its own DIPs from its top-left: icon only's name, or its rename box.</summary>
    private readonly Canvas _overhang = new();

    /// <summary>[S-13974] Icon only's name column under the icon, centred on it: the plate, or the rename box in its place.</summary>
    private readonly Grid _hang = new() { Visibility = Visibility.Collapsed };

    /// <summary>
    /// [S-13974] Icon only's name on its plate: the name's shadow colour under its light text, so it reads over the icons it
    /// covers; [S-13709] on a light surface of Themia's, that surface's ground under its own words (<c>Paint</c>).
    /// </summary>
    private readonly Grid _plate = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Top,
        IsHitTestVisible = false,
    };

    private readonly Border _scrim = new()
    {
        Background = Family.Brush(ColourRole.OnWallpaperShadow),
        Opacity = SelectionLook.NamePlate,
        CornerRadius = new CornerRadius(FamilyShape.Radius),
    };

    /// <inheritdoc />
    public FrameworkElement Overhang => _overhang;

    /// <summary>The plate's parts, once, in the constructor.</summary>
    private void BuildOverhang()
    {
        _plate.Children.Add(_scrim);
        _hang.Children.Add(_plate);
        _overhang.Children.Add(_hang);
    }

    /// <summary>
    /// [S-5593] [S-13971] The icon's square at <paramref name="size"/> and the icon and its name laid out for
    /// <paramref name="layout"/>: the cell is the host's to size ([S-11204]).
    /// </summary>
    private void Relook(DesktopIconSize size, DesktopIconLayout layout)
    {
        _size = size;
        _layout = layout;
        var icon = DesktopCell.Icon(size);
        _cell.Icon.Side = icon;

        // [S-5601] [S-13973] The icon over its name, or beside it, or (icon only) alone in the cell, its name on the plate
        // under it, past the cell.
        DesktopCellLayout.Arrange(_cell.Stack, layout);
        (_name.Parent as Panel)?.Children.Remove(_name);
        var overhangs = DesktopCell.NameOverhangs(layout);
        _name.Alignment = DesktopCellLayout.NameAlignment(layout);
        _name.VerticalAlignment = layout == DesktopIconLayout.Beside ? VerticalAlignment.Center : VerticalAlignment.Top;
        _name.Margin = overhangs ? new Thickness(FamilySpace.S1, 0, FamilySpace.S1, FamilyShape.Hairline) : default;
        (overhangs ? (Panel)_plate : _cell.Stack).Children.Add(_name);

        // [S-5607] A rename box open as the layout changes moves with the name.
        if (_box is { } box)
        {
            (box.Parent as Panel)?.Children.Remove(box);
            LayBox(box);
            BoxHost.Children.Add(box);
        }

        PlaceOverhang();
    }

    /// <summary>
    /// [S-13974] Icon only's name column: as wide as the name it wraps (<see cref="DesktopCell.NameWidth(DesktopIconSize, DesktopIconLayout, double)"/>)
    /// and its plate's sides, centred on the icon, <see cref="DesktopCell.Gap"/> under it (<see cref="DesktopCell.HoverNameTop"/>).
    /// </summary>
    private void PlaceOverhang()
    {
        var width = DesktopCell.NameWidth(_size, DesktopIconLayout.IconOnly, 0) + (2 * FamilySpace.S1);
        var cell = ActualWidth > 0 ? ActualWidth : DesktopCell.Width(_size, DesktopIconLayout.IconOnly);
        _hang.Width = width;
        Canvas.SetLeft(_hang, (cell - width) / 2);
        Canvas.SetTop(_hang, DesktopCell.HoverNameTop(_size, ActualHeight));
    }

    /// <summary>
    /// [S-13974] Whether the name shows now (<see cref="DesktopCell.ShowsName"/>): in icon only while pointed at, focused
    /// after keyboard use (<paramref name="focused"/>) or renamed; the name gives way to the rename box wherever it stands.
    /// </summary>
    private void PaintName(bool focused)
    {
        var renaming = _box is not null;
        var overhangs = DesktopCell.NameOverhangs(_layout);
        var shows = DesktopCell.ShowsName(_layout, _hovered || LinkedElsewhere, focused, renaming);
        _name.Visibility = renaming ? Visibility.Collapsed : Visibility.Visible;
        _plate.Visibility = renaming ? Visibility.Collapsed : Visibility.Visible;
        _hang.Visibility = overhangs && shows ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>[S-5607] [S-13974] Where the rename box stands: in the name's place, in the cell or (icon only) on the plate's column past it.</summary>
    private Panel BoxHost => DesktopCell.NameOverhangs(_layout) ? _hang : _cell.Stack;

    /// <summary>[S-5607] [S-13973] The rename box laid out as the name it edits: centred under the icon, or from its start beside it.</summary>
    private void LayBox(TextBox box)
    {
        switch (_layout)
        {
            case DesktopIconLayout.Beside:
                box.TextAlignment = TextAlignment.Left;
                box.HorizontalAlignment = HorizontalAlignment.Left;
                box.VerticalAlignment = VerticalAlignment.Center;
                box.Width = DesktopCell.NameWidth(_size, _layout, ActualWidth);
                box.Margin = default;
                break;
            case DesktopIconLayout.IconOnly:
                box.TextAlignment = TextAlignment.Center;
                box.HorizontalAlignment = HorizontalAlignment.Stretch;
                box.VerticalAlignment = VerticalAlignment.Top;
                box.Margin = default;
                break;
            default:
                // Out into the cell's inset: Explorer's box is wider than the name it edits.
                box.TextAlignment = TextAlignment.Center;
                box.HorizontalAlignment = HorizontalAlignment.Center;
                box.Margin = new Thickness(-DesktopCell.SideInset, 0, -DesktopCell.SideInset, 0);
                break;
        }
    }
}
