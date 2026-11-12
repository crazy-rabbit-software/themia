using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Themia.Core.Links;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11903] A whole card that is one thing (a drive's card, a meter of one drive, a folder on its drive): the
/// pointer on the parts it is told about (<see cref="Publishes"/>) publishes <see cref="Key"/> on the topic, and
/// while that thing is pointed at elsewhere on the view the card takes the item layer's hover fill
/// (<see cref="LinkLook.Row"/>), as the next meeting does for its event. Its own pointer never lights it; a card
/// that is no such thing (<see cref="Key"/> null) takes no part. Painted only when that changes, never rebuilt.
/// </summary>
public sealed class LinkCard<T>
    where T : class, IEquatable<T>
{
    private readonly WidgetLinks _links;
    private readonly LinkTopic<T> _topic;
    private readonly FrameworkElement _card;
    private readonly List<UIElement> _parts = [];
    private T? _key;
    private T? _pointed;
    private bool _pointing;
    private bool _lit;

    /// <param name="links">The widget's place on the links.</param>
    /// <param name="topic">The hover topic the card is a thing of, named by the kind.</param>
    /// <param name="card">What takes the fill: the view, as a whole card.</param>
    public LinkCard(WidgetLinks links, LinkTopic<T> topic, FrameworkElement card)
    {
        _links = links;
        _topic = topic;
        _card = card;
        links.Follow(topic, value =>
        {
            _pointed = value;
            Light();
        });
    }

    /// <summary>The thing the card is now (its drive), or null for none.</summary>
    public T? Key
    {
        get => _key;
        set
        {
            if (Equals(_key, value))
            {
                return;
            }

            _key = value;
            if (_pointing)
            {
                _links.Set(_topic, value);
            }

            Light();
        }
    }

    /// <summary>Whether the card is lit now: its thing is pointed at elsewhere on the view.</summary>
    public bool Lit => _lit;

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
        if (_pointing)
        {
            _pointing = false;
            _links.Set(_topic, null);
        }
    }

    private void OnEntered(object sender, PointerRoutedEventArgs args)
    {
        if (_key is null)
        {
            return;
        }

        _pointing = true;
        _links.Set(_topic, _key);
    }

    private void OnLeft(object sender, PointerRoutedEventArgs args)
    {
        if (_pointing)
        {
            _pointing = false;
            _links.Set(_topic, null);
        }
    }

    private void Light()
    {
        var lit = _key is not null && _key.Equals(_pointed);
        if (lit == _lit)
        {
            return;
        }

        _lit = lit;
        LinkLook.Row(_card, lit ? LinkLight.Pointed : LinkLight.None);
    }
}
