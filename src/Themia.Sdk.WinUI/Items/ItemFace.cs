using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Items;

namespace Themia.Sdk.WinUI.Items;

/// <summary>
/// [S-16637] [S-16957] One item's widget face (a Drive, a USB stick, a network drive, a Bluetooth device, a Meeting, a Contact): the
/// item itself ([S-16631]; owner, 2026-11-23: "from left to right: icon, then title and subtitle, then right aligned
/// percentage, under all of them the progress bar"), one List item of the list's one size from the card's top edge, its card
/// one item tall (<see cref="ItemLayout.Face"/>), so an item looks the same alone as in its list. A part it has none of, or
/// the user left out, takes no room and is not drawn ([S-16670]). The card lights it under the pointer (its view's
/// <c>OpensLight</c>): the list's own light is off. The end's button (Join) is the list's (<see cref="List"/>).
/// </summary>
public sealed partial class ItemFace : Grid
{
    public ItemFace(IWidgetHost host)
    {
        List = new ItemList(host) { LightsItems = false };
        Children.Add(List);
    }

    /// <summary>The one-item list: its end's action, its lead's own picture.</summary>
    public ItemList List { get; }

    /// <summary>[S-16637] Show <paramref name="item"/> without the parts <paramref name="draw"/> leaves out.</summary>
    public void Show(Item item, ItemDraw draw)
    {
        ArgumentNullException.ThrowIfNull(item);
        List.Show([item], draw);
    }

    /// <summary>The card's box: the item stands from its top edge.</summary>
    public void Size(WidgetArea area) => List.Size(area);
}
