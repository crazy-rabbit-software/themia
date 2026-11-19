namespace Themia.Core.Links;

/// <summary>
/// [S-11903] [S-11950] What a whole card that is one thing (a drive's card, a meter of one drive, a folder on its
/// drive) publishes and how it stands, apart from drawing it. The pointer on a part the card publishes from sets
/// <see cref="Key"/> on the topic; the card is lit (<see cref="LinkLight.Pointed"/>, the item layer's hover fill
/// over the whole card) while the pointer is on the card itself ([S-11962], owner 2026-11-19: the hovered card shows
/// its hover) and while its thing is what the view's board holds and the pointer is not on one of its own parts
/// (the echo: another card of the same drive).
/// <para>
/// [S-11964] What the board holds is read when asked (<c>board</c>), never remembered from the last change heard: a member
/// never hears its own change ([S-3303]), so a card that took a hover over from another (the same value set again
/// owns it silently, [S-3304]) and then let it go would keep the value it last heard and stay lit with nothing
/// pointed at.
/// </para>
/// </summary>
public sealed class LinkCardState<T>
    where T : class, IEquatable<T>
{
    private readonly Func<T?> _board;
    private readonly Func<T?, bool> _publish;
    private T? _key;
    private bool _pointing;
    private bool _onCard;

    /// <param name="board">What the topic holds on the view's board now (null: nothing, or no board).</param>
    /// <param name="publish">Set the topic (null: let go of this card's own hover).</param>
    public LinkCardState(Func<T?> board, Func<T?, bool> publish)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(publish);
        _board = board;
        _publish = publish;
    }

    /// <summary>The thing the card is now (its drive), or null for none: it then publishes nothing and is never lit.</summary>
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
                _publish(value);
            }
        }
    }

    /// <summary>Whether the pointer is on one of the parts the card publishes from.</summary>
    public bool Pointing => _pointing;

    /// <summary>
    /// The pointer came onto a part the card publishes from: publish its thing. <paramref name="wholeCard"/>: the part
    /// is the card itself, which then shows the hover under its own pointer; a smaller part (a folder's header, its
    /// path) has its own hover and leaves the card as it is.
    /// </summary>
    public void Enter(bool wholeCard)
    {
        if (_key is null)
        {
            return;
        }

        _pointing = true;
        _onCard = wholeCard;
        _publish(_key);
    }

    /// <summary>The pointer left (or the card let go of its parts): let go of the hover this card set.</summary>
    public void Leave()
    {
        if (!_pointing)
        {
            return;
        }

        _pointing = false;
        _onCard = false;
        _publish(null);
    }

    /// <summary>How the whole card stands now.</summary>
    public LinkLight Light
    {
        get
        {
            if (_key is null)
            {
                return LinkLight.None;
            }

            if (_onCard)
            {
                return LinkLight.Pointed;
            }

            // Its own part's hover is on the board while the pointer is there: that is no echo.
            return !_pointing && _key.Equals(_board()) ? LinkLight.Pointed : LinkLight.None;
        }
    }
}
