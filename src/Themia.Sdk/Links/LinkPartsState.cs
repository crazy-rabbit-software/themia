namespace Themia.Core.Links;

/// <summary>
/// [S-11965] The parts one widget draws for a hover topic (rows, cells, marks) and how each stands, apart from drawing
/// them: a hover lights exactly the item under the pointer (owner, 2026-11-19: "it should only highglight exactly the
/// same item, not all from the sender"). In the widget the pointer is on, only the part under it is lit, never another
/// part that happens to carry the same value; its keyboard cursor lights nothing here (the cursor's own mark says
/// where it is). In the other widgets of the view a part lights only where it is that same item (its key, which is what
/// the part is, never something it carries: [S-11965]). A picked value ([S-3302]) lights every part that is it.
/// <para>
/// What the board holds is read when asked (<c>board</c>), never remembered from the last change heard ([S-11964]).
/// </para>
/// </summary>
/// <typeparam name="TPart">A drawn part (a row, a cell, a mark); compared by reference.</typeparam>
/// <typeparam name="T">The topic's value.</typeparam>
public sealed class LinkPartsState<TPart, T>
    where TPart : class
    where T : class, IEquatable<T>
{
    private readonly Func<T?> _board;
    private readonly Func<T?, bool> _publish;
    private readonly Dictionary<TPart, T?> _keys = new(ReferenceEqualityComparer.Instance);

    /// <summary>How many parts are each value, so "a value this widget shows" is one look-up ([S-11900]).</summary>
    private readonly Dictionary<T, int> _shown = [];

    private T? _own;
    private TPart? _ownPart;

    /// <param name="board">What the hover topic holds on the view's board now (null: nothing, or no board).</param>
    /// <param name="publish">Set the hover topic (null: let go of this widget's own hover).</param>
    public LinkPartsState(Func<T?> board, Func<T?, bool> publish)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(publish);
        _board = board;
        _publish = publish;
    }

    /// <summary>The value this widget's own pointer or keyboard cursor is on, or null.</summary>
    public T? Own => _own;

    /// <summary>The value pointed at now: this widget's own, else what the view's board holds.</summary>
    public T? Pointed => _own ?? _board();

    /// <summary>The value picked on the view now (the sticky topic of the same thing), or null.</summary>
    public T? Picked { get; set; }

    /// <summary>Every part, as registered.</summary>
    public IEnumerable<TPart> Parts => _keys.Keys;

    /// <summary>A part drawn as <paramref name="key"/> (null: a part of nothing, never lit and never publishing).</summary>
    public void Set(TPart part, T? key)
    {
        ArgumentNullException.ThrowIfNull(part);
        if (_keys.TryGetValue(part, out var before))
        {
            Uncount(before);
        }

        _keys[part] = key;
        Count(key);
    }

    /// <summary>A part taken down: forgotten, and with it the pointer on it.</summary>
    public void Forget(TPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        if (_keys.Remove(part, out var key))
        {
            Uncount(key);
        }

        if (ReferenceEquals(part, _ownPart))
        {
            _ownPart = null;
        }
    }

    /// <summary>Every part forgotten (the widget draws its parts again).</summary>
    public void ForgetAll()
    {
        _keys.Clear();
        _shown.Clear();
        _ownPart = null;
    }

    /// <summary>The pointer came onto <paramref name="part"/>: its value is the view's hover. False for a part of nothing.</summary>
    public bool Enter(TPart part)
    {
        if (!_keys.TryGetValue(part, out var key) || key is null)
        {
            return false;
        }

        _own = key;
        _ownPart = part;
        _publish(key);
        return true;
    }

    /// <summary>The pointer left <paramref name="part"/>: when it was the one under it, this widget's hover goes.</summary>
    public bool Leave(TPart part)
    {
        if (!ReferenceEquals(part, _ownPart))
        {
            return false;
        }

        Point(null);
        return true;
    }

    /// <summary>
    /// [S-3307] The keyboard cursor is on <paramref name="key"/> (null: it let go), or the widget points at a value
    /// without a part: publish it. Nothing here is lit for it.
    /// </summary>
    public void Point(T? key)
    {
        _own = key;
        _ownPart = null;
        _publish(key);
    }

    /// <summary>Another widget changed the hover: this widget's own pointer is no longer what the view points at.</summary>
    public void Heard()
    {
        _own = null;
        _ownPart = null;
    }

    /// <summary>[S-11901] [S-11965] How <paramref name="part"/> stands: picked wins, then pointed.</summary>
    public LinkLight Light(TPart part)
    {
        if (!_keys.TryGetValue(part, out var key) || key is null)
        {
            return LinkLight.None;
        }

        if (ShownValue(Picked) is { } picked && key.Equals(picked))
        {
            return LinkLight.Picked;
        }

        if (_own is not null)
        {
            // This widget's own pointer: the part under it alone, whatever else here carries the same value.
            return ReferenceEquals(part, _ownPart) ? LinkLight.Pointed : LinkLight.None;
        }

        return ShownValue(_board()) is { } pointed && key.Equals(pointed) ? LinkLight.Pointed : LinkLight.None;
    }

    /// <summary>
    /// [S-11902] The factor on a mark's own opacity: whole while it is lit or while nothing is, else the links' less
    /// (<see cref="LinkEmphasis.Others"/>) while something is pointed at (here: the part under the pointer) or picked.
    /// </summary>
    public double Strength(TPart part)
    {
        if (Light(part) != LinkLight.None)
        {
            return 1;
        }

        var pointed = _own is not null ? _ownPart is not null : ShownValue(_board()) is not null;
        return LinkEmphasis.Others(pointed, ShownValue(Picked) is not null);
    }

    /// <summary>[S-11900] The value as this widget shows it: itself when one of its parts is it, else null.</summary>
    private T? ShownValue(T? value) => value is not null && _shown.ContainsKey(value) ? value : null;

    private void Count(T? key)
    {
        if (key is not null)
        {
            _shown[key] = _shown.GetValueOrDefault(key) + 1;
        }
    }

    private void Uncount(T? key)
    {
        if (key is not null && _shown.TryGetValue(key, out var count))
        {
            if (count <= 1)
            {
                _shown.Remove(key);
            }
            else
            {
                _shown[key] = count - 1;
            }
        }
    }
}
