namespace Themia.Core.Widgets;

/// <summary>
/// [S-3920] The order a toggle's readings are taken in. Reads of one setting may run at once (a
/// notification, the switch's own read, the pointer's re-read, each on the pool), and the one that
/// started first can finish last: published then, it would put back a state Windows has already left.
/// Each read takes a ticket before it asks Windows; a reading whose ticket is older than the one last
/// shown is dropped, since a read that started later saw at least as much. Thread-safe.
/// </summary>
public sealed class ReadingOrder
{
    private readonly Lock _gate = new();
    private long _next;
    private long _shown;

    /// <summary>A ticket for a read about to ask Windows: take it before asking.</summary>
    public long Begin()
    {
        lock (_gate)
        {
            return ++_next;
        }
    }

    /// <summary>Whether the reading of the read holding <paramref name="ticket"/> may be shown; it is then the latest.</summary>
    public bool Accept(long ticket)
    {
        lock (_gate)
        {
            if (ticket < _shown)
            {
                return false;
            }

            _shown = ticket;
            return true;
        }
    }
}

/// <summary>Where a registry watch is armed.</summary>
public enum KeyWatched
{
    /// <summary>On the key itself.</summary>
    Key,

    /// <summary>On its parent, waiting for the key to appear (or appear again).</summary>
    Parent,

    /// <summary>Nowhere: neither could be watched.</summary>
    Nothing,
}

/// <summary>What a registry watch can do: the native arms behind <see cref="KeyFollow"/>.</summary>
public interface IKeyArms
{
    /// <summary>Open the key and ask for its next change, in place of whatever was armed; false where it is not there.</summary>
    bool ArmKey();

    /// <summary>Open the key's parent and ask for its next change, in place of whatever was armed; false where it is not there.</summary>
    bool ArmParent();

    /// <summary>Ask the key that is armed now for its next change; false where Windows refused (the key went).</summary>
    bool AskAgain();
}

/// <summary>
/// [S-3921] The steps of a registry watch (<c>RegNotifyChangeKeyValue</c> is one-shot): what is armed
/// when it opens and after each signal, and whether the owner is told. Pure over <see cref="IKeyArms"/>,
/// so the races are tested without a registry.
/// <para>
/// <b>Armed before it is read.</b> Every step asks for the next change before the owner reads, so a
/// change made meanwhile signals again. <b>The key is looked for after the parent is armed</b>: a key
/// that went (the CloudStore writes a value's key again from scratch) and came back before the parent
/// was watched would otherwise never be seen again, the parent hearing nothing more while the key's
/// own changes go unheard.
/// </para>
/// </summary>
public static class KeyFollow
{
    /// <summary>Where the watch is armed when it opens.</summary>
    public static KeyWatched Open(IKeyArms arms) => arms.ArmKey() ? KeyWatched.Key : Fall(arms);

    /// <summary>
    /// The armed watch at <paramref name="at"/> was signalled: where it is armed now, and whether the
    /// owner reads again (the key changed, went, or appeared).
    /// </summary>
    public static (KeyWatched At, bool Tell) Heard(KeyWatched at, IKeyArms arms)
    {
        switch (at)
        {
            case KeyWatched.Key:
                return arms.AskAgain() ? (KeyWatched.Key, true) : (Fall(arms), true);

            case KeyWatched.Parent:
            {
                // Something under the parent changed. The parent is asked again first, then the key
                // looked for: one appearing in between is then either found or heard.
                var waiting = arms.AskAgain();
                if (arms.ArmKey())
                {
                    return (KeyWatched.Key, true);
                }

                return (waiting ? KeyWatched.Parent : KeyWatched.Nothing, false);
            }

            default:
                return (KeyWatched.Nothing, false);
        }
    }

    /// <summary>The key is not there: the parent armed first, then the key looked for once more.</summary>
    private static KeyWatched Fall(IKeyArms arms)
    {
        var waiting = arms.ArmParent();
        if (arms.ArmKey())
        {
            return KeyWatched.Key;
        }

        return waiting ? KeyWatched.Parent : KeyWatched.Nothing;
    }
}
