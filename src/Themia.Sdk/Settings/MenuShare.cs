namespace Themia.Core.Settings;

/// <summary>
/// [S-3734] How a menu row's words and the value at its end share the row, after Karpo's menus
/// (karpo-app, Flyout.Values.cs, §315): both whole where both fit; else the shorter keeps all it needs
/// where that is within its share (the value two fifths of the row, the words the rest) and the other
/// has what is left; else each has its share, both trimmed. A value is never left out.
/// </summary>
public static class MenuShare
{
    /// <summary>The value's share of a row whose words and value do not both fit; the words' is the rest.</summary>
    public const double ValueShare = 0.4;

    /// <summary>The widths the words and the value get of <paramref name="room"/>, needing <paramref name="words"/> and <paramref name="value"/>.</summary>
    public static (double Words, double Value) Share(double room, double words, double value)
    {
        room = Math.Max(0, room);
        words = Math.Max(0, words);
        value = Math.Max(0, value);
        if (words + value <= room)
        {
            return (words, value);
        }

        var given = value <= room * ValueShare ? value
            : words <= room * (1 - ValueShare) ? room - words
            : room * ValueShare;
        given = Math.Floor(given);
        return (room - given, given);
    }
}
