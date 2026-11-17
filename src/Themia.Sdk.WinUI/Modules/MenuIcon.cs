namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-9602] A widget page's icon line: the one icon picker (every Segoe Fluent glyph, searched, [S-1803]) inline
/// in its card, as a view's icon is chosen. <see cref="Current"/> is the glyph chosen, null for none (the
/// picker's first cell, worded <see cref="NoneName"/>); <see cref="Set"/> keeps a pick and saves.
/// </summary>
public sealed class MenuIcon(string? current, string noneName, Action<string?> set)
{
    public string? Current { get; } = current;

    /// <summary>What the picker's first cell, no glyph, is called ("None").</summary>
    public string NoneName { get; } = noneName ?? throw new ArgumentNullException(nameof(noneName));

    public Action<string?> Set { get; } = set ?? throw new ArgumentNullException(nameof(set));
}
