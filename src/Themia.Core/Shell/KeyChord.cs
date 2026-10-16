using Themia.Core.Localization;

namespace Themia.Core.Shell;

/// <summary>The modifiers of a <see cref="KeyChord"/>. Written in this order, as Windows writes them.</summary>
[Flags]
public enum ChordModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
}

/// <summary>The key of a <see cref="KeyChord"/>: a letter, a function key, or one of the named keys a menu shows.</summary>
public enum ChordKey
{
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    Enter,
    Delete,
}

/// <summary>
/// [S-1753] A keyboard shortcut as a menu shows it beside a command ("Ctrl+Shift+C"). Menus hold
/// the chord, never a typed string, so every shortcut in the app is written by <see cref="KeyChordText"/>.
/// </summary>
public readonly record struct KeyChord(ChordModifiers Modifiers, ChordKey Key)
{
    public static KeyChord Plain(ChordKey key) => new(ChordModifiers.None, key);

    public static KeyChord Ctrl(ChordKey key) => new(ChordModifiers.Ctrl, key);

    public static KeyChord Shift(ChordKey key) => new(ChordModifiers.Shift, key);

    public static KeyChord Alt(ChordKey key) => new(ChordModifiers.Alt, key);

    public static KeyChord CtrlShift(ChordKey key) => new(ChordModifiers.Ctrl | ChordModifiers.Shift, key);
}

/// <summary>
/// [S-1753] The one formatter of shortcut text, written as Windows 11 writes it in File Explorer's
/// menu and every WinUI menu: modifiers in the order Ctrl, Alt, Shift, then the key, joined by "+"
/// ("Ctrl+Shift+C", "Alt+Enter", "Shift+F10", "Delete"). The modifier and named-key words come
/// from the language layer, as Windows localizes them ("Strg+Umschalt+C", "Entf"); letters and
/// function keys are written as they are on the key.
/// </summary>
public static class KeyChordText
{
    /// <summary>The chord in the current language.</summary>
    public static string Format(KeyChord chord) => Format(chord, Strings.Current);

    /// <summary>The chord in <paramref name="locale"/>.</summary>
    public static string Format(KeyChord chord, Locale locale)
    {
        if (!Enum.IsDefined(chord.Key) || ((int)chord.Modifiers & ~(int)(ChordModifiers.Ctrl | ChordModifiers.Alt | ChordModifiers.Shift)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chord), chord, "not a chord a menu shows");
        }

        var parts = new List<string>(4);

        if (chord.Modifiers.HasFlag(ChordModifiers.Ctrl))
        {
            parts.Add(Strings.Get(locale, Str.Shell_Key_Ctrl));
        }

        if (chord.Modifiers.HasFlag(ChordModifiers.Alt))
        {
            parts.Add(Strings.Get(locale, Str.Shell_Key_Alt));
        }

        if (chord.Modifiers.HasFlag(ChordModifiers.Shift))
        {
            parts.Add(Strings.Get(locale, Str.Shell_Key_Shift));
        }

        parts.Add(chord.Key switch
        {
            ChordKey.Enter => Strings.Get(locale, Str.Shell_Key_Enter),
            ChordKey.Delete => Strings.Get(locale, Str.Shell_Key_Delete),

            // Letters and F1..F12 read the same in every language: they are what the key says.
            >= ChordKey.F1 => "F" + ((chord.Key - ChordKey.F1) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => ((char)('A' + (int)chord.Key)).ToString(),
        });

        return string.Join('+', parts);
    }
}
