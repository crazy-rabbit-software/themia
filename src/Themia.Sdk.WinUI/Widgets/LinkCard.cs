using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Themia.Core.Links;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11903] A whole card that is one thing (a drive's card, a meter of one drive, a folder on its drive): the
/// pointer on the parts it is told about (<see cref="Publishes"/>) publishes <see cref="Key"/> on the topic, and
/// the card takes the item layer's hover fill over its whole box (<see cref="LinkLook.Card"/>, [S-11963]) while the
/// pointer is on the card itself ([S-11962]) and while that thing is pointed at elsewhere on the view, as the next
/// meeting does for its event. A card that is no such thing (<see cref="Key"/> null) takes no part. What it shows is
/// <see cref="LinkCardState{T}"/>'s; painted only when that changes, never rebuilt.
/// </summary>
public sealed class LinkCard<T>
    where T : class, IEquatable<T>
{
    private readonly WidgetLinks _links;
    private readonly FrameworkElement _card;
    private readonly LinkCardState<T> _state;
    private readonly List<UIElement> _parts = [];
    private LinkLight _shown;

    /// <param name="links">The widget's place on the links.</param>
    /// <param name="topic">The hover topic the card is a thing of, named by the kind.</param>
    /// <param name="card">The view, as a whole card: the pointer on it lights it.</param>
    public LinkCard(WidgetLinks links, LinkTopic<T> topic, FrameworkElement card)
    {
        _links = links;
        _card = card;
        _state = new LinkCardState<T>(() => links.Get(topic), value => links.Set(topic, value));
        links.Follow(topic, _ => Light());
    }

    /// <summary>The thing the card is now (its drive), or null for none.</summary>
    public T? Key
    {
        get => _state.Key;
        set
        {
            _state.Key = value;
            Light();
        }
    }

    /// <summary>Whether the card is lit now: under the pointer, or its thing pointed at elsewhere on the view.</summary>
    public bool Lit => _shown != LinkLight.None;

    /// <summary>The pointer on <paramref name="part"/> (the card, its header, its path) publishes the card's thing.</summary>
    public void Publishes(UIElement part)
    {
        ArgumentNullException.ThrowIfNull(part);
        if (_parts.Contains(part))
        {
            return;
        }

        _parts.Add(part);
        part.PointerEntered += OnEntered;
        part.PointerExited += OnLeft;
        part.PointerCanceled += OnLeft;
        part.PointerCaptureLost += OnLeft;
    }

    /// <summary>Let go of every part's pointer (Detach) and of the hover this card set.</summary>
    public void Release()
    {
        foreach (var part in _parts)
        {
            part.PointerEntered -= OnEntered;
            part.PointerExited -= OnLeft;
            part.PointerCanceled -= OnLeft;
            part.PointerCaptureLost -= OnLeft;
        }

        _parts.Clear();
        _state.Leave();
        Light();
    }

    private void OnEntered(object sender, PointerRoutedEventArgs args)
    {
        _state.Enter(wholeCard: ReferenceEquals(sender, _card));
        Light();
    }

    private void OnLeft(object sender, PointerRoutedEventArgs args)
    {
        _state.Leave();
        Light();
    }

    private void Light()
    {
        var light = _state.Light;
        if (light == _shown)
        {
            return;
        }

        _shown = light;
        LinkLook.Card(_card, light);
    }
}
