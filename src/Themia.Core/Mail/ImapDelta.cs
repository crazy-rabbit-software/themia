using System.Globalization;
using System.Text;
using Themia.Core.Sync;

namespace Themia.Core.Mail;

/// <summary>A mailbox as EXAMINE reported it: how many messages, which incarnation, the next UID and (CONDSTORE) the highest mod-sequence.</summary>
public readonly record struct ImapOpenedState(long Exists, uint? UidValidity, uint? UidNext, ulong? HighestModSeq);

/// <summary>What an IMAP folder sync does, given what is held and what the server says now.</summary>
public enum ImapPlan
{
    /// <summary>The newest N again, whole: nothing usable held, or the mailbox is another incarnation.</summary>
    Full,

    /// <summary>CONDSTORE says nothing changed: no FETCH at all.</summary>
    Unchanged,

    /// <summary>The flags of the held UIDs (a missing one was expunged) and only the messages that arrived since.</summary>
    Incremental,
}

/// <summary>[S-2689] The IMAP half of incremental mail: the plan, the UID sets, and the responses it reads.</summary>
public static class ImapDelta
{
    /// <summary>
    /// The plan. A missing mark, an unknown or changed UIDVALIDITY (RFC 3501 §2.3.1.1: the UIDs held
    /// now name other messages), or a UIDNEXT that went backwards is a full fetch. With CONDSTORE on
    /// both sides, the same HIGHESTMODSEQ, UIDNEXT and EXISTS mean nothing changed. Anything else
    /// is incremental.
    /// </summary>
    public static ImapPlan Plan(ImapMark? held, ImapOpenedState now)
    {
        if (held is null || now.UidValidity is not { } validity || validity != held.UidValidity
            || now.UidNext is not { } next || next < held.UidNext)
        {
            return ImapPlan.Full;
        }

        if (now.HighestModSeq is { } modseq && held.HighestModSeq == modseq && next == held.UidNext && now.Exists == held.Exists)
        {
            return ImapPlan.Unchanged;
        }

        return ImapPlan.Incremental;
    }

    /// <summary>What to keep after a read: null when the server gave no UIDVALIDITY or UIDNEXT (nothing incremental can follow).</summary>
    public static ImapMark? Mark(ImapOpenedState now)
        => now.UidValidity is { } validity and > 0 && now.UidNext is { } next and > 0
            ? new ImapMark { UidValidity = validity, UidNext = next, Exists = Math.Max(0, now.Exists), HighestModSeq = now.HighestModSeq }
            : null;

    /// <summary>A UID set (<c>3:5,9,12</c>) of the given UIDs, ascending and merged into runs; null when there are none.</summary>
    public static string? UidSet(IEnumerable<uint> uids)
    {
        ArgumentNullException.ThrowIfNull(uids);

        var sorted = uids.Where(u => u > 0).Distinct().Order().ToList();
        if (sorted.Count == 0)
        {
            return null;
        }

        var text = new StringBuilder();
        var start = sorted[0];
        var end = start;

        void Flush()
        {
            if (text.Length > 0)
            {
                text.Append(',');
            }

            text.Append(start.ToString(CultureInfo.InvariantCulture));
            if (end != start)
            {
                text.Append(':').Append(end.ToString(CultureInfo.InvariantCulture));
            }
        }

        foreach (var uid in sorted.Skip(1))
        {
            if (uid == end + 1)
            {
                end = uid;
                continue;
            }

            Flush();
            start = end = uid;
        }

        Flush();
        return text.ToString();
    }

    /// <summary>
    /// The newest <paramref name="count"/> of the UIDs a search returned that are at least
    /// <paramref name="from"/>: <c>UID SEARCH UID n:*</c> always answers the last message even when
    /// its UID is below n (RFC 3501 §6.4.8), so the bound is applied here.
    /// </summary>
    public static IReadOnlyList<uint> Arrived(IEnumerable<uint> uids, uint from, int count)
    {
        ArgumentNullException.ThrowIfNull(uids);
        return [.. uids.Where(u => u >= from).Distinct().OrderDescending().Take(Math.Max(0, count))];
    }

    /// <summary><c>* OK [UIDNEXT 4392] …</c> → 4392.</summary>
    public static uint? UidNext(ImapResponse response)
        => Code(response, "UIDNEXT") is { } text && uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary><c>* OK [HIGHESTMODSEQ 715194045007]</c> → 715194045007 (RFC 7162).</summary>
    public static ulong? HighestModSeq(ImapResponse response)
        => Code(response, "HIGHESTMODSEQ") is { } text && ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>Whether a <c>* CAPABILITY …</c> response names <paramref name="capability"/>.</summary>
    public static bool HasCapability(ImapResponse response, string capability)
    {
        ArgumentNullException.ThrowIfNull(response);
        return response.Tag == "*" && response.Word(0) == "CAPABILITY"
            && response.Values.Skip(1).Any(v => string.Equals(v.Text, capability, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary><c>* SEARCH 2 84 882</c> → the UIDs (or sequence numbers) listed; empty for anything else.</summary>
    public static IReadOnlyList<uint> Search(ImapResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Tag != "*" || response.Word(0) != "SEARCH")
        {
            return [];
        }

        var found = new List<uint>();
        foreach (var value in response.Values.Skip(1))
        {
            if (value.Text is { } text && uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            {
                found.Add(n);
            }
        }

        return found;
    }

    /// <summary>The value of a bracketed response code such as <c>[UIDNEXT 4392]</c> on an untagged OK.</summary>
    private static string? Code(ImapResponse response, string name)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Tag != "*" || response.Word(0) != "OK" || response.Values.Count < 2 || response.Values[1] is not ImapAtom code)
        {
            return null;
        }

        var parts = code.Value.Trim('[', ']').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && parts[0].Equals(name, StringComparison.OrdinalIgnoreCase) ? parts[1] : null;
    }
}
