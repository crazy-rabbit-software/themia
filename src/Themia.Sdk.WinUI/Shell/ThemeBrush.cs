using Microsoft.UI.Xaml.Media;
using Color = Windows.UI.Color;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-822] The brush of a Fluent theme resource in Themia's own theme: what every code-made part
/// draws its text, fills and strokes with (design tokens, never a literal colour). The host owns the
/// design engine (the user's theme and accent, recoloured in place) and installs it here once, first
/// thing; a widget or an SDK part asks this, so it never reaches the host's design state.
/// </summary>
public static class ThemeBrush
{
    private static Func<string, Color, Brush>? _resolve;

    /// <summary>
    /// The shared brush for <paramref name="key"/> (a <c>…Brush</c> resource name such as
    /// <c>TextFillColorSecondaryBrush</c>), or <paramref name="fallback"/> where the theme has none.
    /// The same object for the life of the process: it is recoloured when the theme changes.
    /// </summary>
    public static Brush Of(string key, Color fallback)
        => (Volatile.Read(ref _resolve) ?? throw new InvalidOperationException("no theme brushes installed: the host installs them before any part is drawn"))(key, fallback);

    /// <summary>The host's design engine. Once per process.</summary>
    internal static void Install(Func<string, Color, Brush> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        if (Interlocked.CompareExchange(ref _resolve, resolve, null) is not null)
        {
            throw new InvalidOperationException("the theme brushes are installed once per process");
        }
    }
}
