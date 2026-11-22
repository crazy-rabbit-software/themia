using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-15745] A whole card that opens what it is about on a click (<see cref="IWidgetOpens"/>: a meter's Task Manager, a
/// drive's root in Explorer) lights as that one item: the item layer's hover fill over the card's whole box under the
/// pointer, its pressed fill pressed (WR-13, owner 2026-11-19: "when hovering over a widget it also does not show the hover
/// effect"; WR-12: only the fill changes), through <see cref="LinkLook.Opens"/>; registered for the renders' <c>--hover</c>
/// and <c>--pressed</c> (<see cref="PointerPreview"/>). It lights only while <c>lights</c> says the card opens something
/// now and is not in edit mode (where the press selects it). Four events, as a row's hover, and the press seen even when a
/// part took it.
/// </summary>
public sealed class OpensLight
{
    private readonly FrameworkElement _view;
    private readonly Func<bool> _lights;
    private readonly PointerEventHandler _pressed;
    private readonly PointerEventHandler _released;
    private bool _attached;
    private bool _pointed;
    private ItemState _shown = ItemState.Rest;

    /// <param name="view">The view, the card's whole content.</param>
    /// <param name="lights">Whether a click on the card opens something now (and the card is not in edit mode).</param>
    public OpensLight(FrameworkElement view, Func<bool> lights)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _lights = lights ?? throw new ArgumentNullException(nameof(lights));
        _pressed = (_, _) => Paint(ItemState.Pressed);
        _released = (_, _) => Paint(_pointed ? ItemState.Hover : ItemState.Rest);
    }

    /// <summary>Follow the pointer (the view's Attach).</summary>
    public void Attach()
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        _view.PointerEntered += OnEntered;
        _view.PointerExited += OnGone;
        _view.PointerCaptureLost += OnGone;
        _view.PointerCanceled += OnGone;
        _view.AddHandler(UIElement.PointerPressedEvent, _pressed, handledEventsToo: true);
        _view.AddHandler(UIElement.PointerReleasedEvent, _released, handledEventsToo: true);
        PointerPreview.Register(_view, pressed => Paint(pressed ? ItemState.Pressed : ItemState.Hover));
    }

    /// <summary>Let go of the pointer and of the light (the view's Detach).</summary>
    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        _view.PointerEntered -= OnEntered;
        _view.PointerExited -= OnGone;
        _view.PointerCaptureLost -= OnGone;
        _view.PointerCanceled -= OnGone;
        _view.RemoveHandler(UIElement.PointerPressedEvent, _pressed);
        _view.RemoveHandler(UIElement.PointerReleasedEvent, _released);
        PointerPreview.Forget(_view);
        _pointed = false;
        Paint(ItemState.Rest);
    }

    private void OnEntered(object sender, PointerRoutedEventArgs args)
    {
        _pointed = true;
        Paint(ItemState.Hover);
    }

    private void OnGone(object sender, PointerRoutedEventArgs args)
    {
        _pointed = false;
        Paint(ItemState.Rest);
    }

    private void Paint(ItemState state)
    {
        // A card that opens nothing now (or in edit mode) stays as it is; a light it showed is let go.
        var wanted = state == ItemState.Rest || _lights() ? state : ItemState.Rest;
        if (wanted == _shown && wanted == ItemState.Rest)
        {
            return;
        }

        _shown = wanted;
        LinkLook.Opens(_view, wanted);
    }
}
