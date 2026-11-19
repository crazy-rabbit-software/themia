using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-5601] [S-13971] [S-13973] [S-13974] The one placing of a desktop cell's icon and name in the desktop's icon layout,
/// for everything drawn as a desktop icon (the desktop's icon view, a fence's own cells): the numbers are
/// <see cref="DesktopCell"/>'s, this only lays them on the cell's stack (the icon first, its name after it).
/// </summary>
public static class DesktopCellLayout
{
    /// <summary>
    /// Lay <paramref name="stack"/> (the icon, then its name) for <paramref name="layout"/>: below, the icon over its name
    /// from the cell's top by Explorer's insets, centred across; beside, the icon at the cell's start and its name after
    /// it, centred top to bottom; icon only, the icon alone centred in the cell (the name is not the cell's).
    /// </summary>
    public static void Arrange(StackPanel stack, DesktopIconLayout layout)
    {
        ArgumentNullException.ThrowIfNull(stack);
        switch (layout)
        {
            case DesktopIconLayout.Beside:
                stack.Orientation = Orientation.Horizontal;
                stack.Spacing = DesktopCell.BesideGap;
                stack.HorizontalAlignment = HorizontalAlignment.Left;
                stack.VerticalAlignment = VerticalAlignment.Center;
                stack.Margin = new Thickness(DesktopCell.BesideInset, 0, DesktopCell.BesideInset, 0);
                break;
            case DesktopIconLayout.IconOnly:
                stack.Orientation = Orientation.Vertical;
                stack.Spacing = 0;
                stack.HorizontalAlignment = HorizontalAlignment.Center;
                stack.VerticalAlignment = VerticalAlignment.Center;
                stack.Margin = default;
                break;
            default:
                stack.Orientation = Orientation.Vertical;
                stack.Spacing = DesktopCell.Gap;
                stack.HorizontalAlignment = HorizontalAlignment.Center;
                stack.VerticalAlignment = VerticalAlignment.Top;
                stack.Margin = new Thickness(DesktopCell.SideInset, DesktopCell.HighlightTop + DesktopCell.IconTop, DesktopCell.SideInset, 0);
                break;
        }
    }

    /// <summary>Where a name's lines stand in <paramref name="layout"/>: from the start beside the icon, else centred.</summary>
    public static TextAlignment NameAlignment(DesktopIconLayout layout)
        => layout == DesktopIconLayout.Beside ? TextAlignment.Left : TextAlignment.Center;
}
