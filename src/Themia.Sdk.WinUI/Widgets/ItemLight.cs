using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-14705] A one-item face that lights under the pointer and pressed because a click on it opens something (a single
/// metric, a text widget): the item layer's row fill at rest, hover and pressed (<see cref="ItemFill"/>, as every row that
/// opens; WR-13: exactly the item under the pointer, the card edge to edge; WR-12: only its fill changes), registered for
/// the renders' <c>--hover</c>/<c>--pressed</c> (<see cref="PointerPreview"/>). Off, the item is clear and takes no pointer
/// handler. Four events, as a row's hover: PointerExited alone strands the item lit when the card moves under it.
/// </summary>
public sealed class ItemLight
{
    private readonly Border _item;
    private readonly PointerEventHandler _pressed;
    private readonly PointerEventHandler _released;
    private bool _on;
    private bool _pointed;

    /// <param name="item">The item, the card edge to edge.</param>
    public ItemLight(Border item)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
        _item.Background = Family.Brush(ColourRole.Clear);
        _pressed = (_, _) => ItemFill.Paint(_item, ItemRole.Row, ItemState.Pressed);
        _released = (_, _) => ItemFill.Paint(_item, ItemRole.Row, _pointed ? ItemState.Hover : ItemState.Rest);
    }

    /// <summary>Whether the item lights (it opens something now).</summary>
    public bool On => _on;

    /// <summary>Light the item under the pointer, or stop (clear, no handler).</summary>
    public void Set(bool on)
    {
        if (on == _on)
        {
            return;
        }

        _on = on;
        if (on)
        {
            ItemFill.Paint(_item, ItemRole.Row);
            PointerPreview.Register(_item, pressed => ItemFill.Paint(_item, ItemRole.Row, pressed ? ItemState.Pressed : ItemState.Hover));
            _item.PointerEntered += OnEntered;
            _item.PointerExited += OnGone;
            _item.PointerCaptureLost += OnGone;
            _item.PointerCanceled += OnGone;
            _item.AddHandler(UIElement.PointerPressedEvent, _pressed, handledEventsToo: true);
            _item.AddHandler(UIElement.PointerReleasedEvent, _released, handledEventsToo: true);
        }
        else
        {
            PointerPreview.Forget(_item);
            _item.PointerEntered -= OnEntered;
            _item.PointerExited -= OnGone;
            _item.PointerCaptureLost -= OnGone;
            _item.PointerCanceled -= OnGone;
            _item.RemoveHandler(UIElement.PointerPressedEvent, _pressed);
            _item.RemoveHandler(UIElement.PointerReleasedEvent, _released);
            _pointed = false;
            _item.Background = Family.Brush(ColourRole.Clear);
        }
    }

    private void OnEntered(object sender, PointerRoutedEventArgs args)
    {
        _pointed = true;
        ItemFill.Paint(_item, ItemRole.Row, ItemState.Hover);
    }

    private void OnGone(object sender, PointerRoutedEventArgs args)
    {
        _pointed = false;
        ItemFill.Paint(_item, ItemRole.Row);
    }
}
