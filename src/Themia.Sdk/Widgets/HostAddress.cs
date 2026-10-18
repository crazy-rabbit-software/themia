namespace Themia.Core.Widgets;

/// <summary>
/// Whether a string is worth handing to a name resolver at all.
/// <para>
/// The ping host is typed by the user and then persisted, so it arrives at the probe from two
/// directions — the settings box and a JSON file that anyone can hand-edit. Both are trust
/// boundaries, and both go through here: the settings box so a typo is rejected while it is still
/// being made, the loader so a hand-edited file cannot get a value past the box. Rejecting up front
/// also means the widget can say "that is not a host" instead of waiting two seconds to say
/// "unreachable", which reads as a network fault rather than a typo.
/// </para>
/// <para>
/// This is a plausibility check, not a resolver: it says the string has the shape of a host name or
/// an address. Whether anything answers there is the probe's business.
/// </para>
/// </summary>
public static class HostAddress
{
    /// <summary>[S-109] What a ping widget probes with no host typed: a public resolver that answers.</summary>
    public const string DefaultPingHost = "1.1.1.1";

    /// <summary>A DNS name cannot exceed 253 characters, and an IPv6 literal is far shorter.</summary>
    private const int MaxLength = 253;

    /// <summary>True when <paramref name="host"/> has the shape of a host name or an IP address.</summary>
    public static bool IsPlausible(string? host)
    {
        var target = host?.Trim();

        if (string.IsNullOrEmpty(target) || target.Length > MaxLength)
        {
            return false;
        }

        // A leading '-' or '/' is how a host name turns into a command-line switch. Nothing here
        // spawns a process today, but the check costs nothing and the value is persisted, so it
        // would outlive that assumption.
        if (target[0] is '-' or '/' or '.')
        {
            return false;
        }

        if (target[^1] is '-')
        {
            return false;
        }

        var alphanumeric = false;

        for (var i = 0; i < target.Length; i++)
        {
            var c = target[i];

            if (char.IsAsciiLetterOrDigit(c))
            {
                alphanumeric = true;
                continue;
            }

            // ':' for IPv6 literals and nothing else — a port would be meaningless to ICMP, and
            // '%' zone suffixes are not worth the parsing surface.
            if (c is not ('.' or '-' or '_' or ':'))
            {
                return false;
            }

            // An empty label ("a..b") is not a name. '::' is the IPv6 run-of-zeroes shorthand, so
            // only a repeated dot is disqualifying.
            if (c == '.' && i + 1 < target.Length && target[i + 1] == '.')
            {
                return false;
            }
        }

        // "..." or "---" pass every rule above and resolve to nothing. At least one letter or digit
        // is what separates a name from punctuation.
        return alphanumeric;
    }
}
