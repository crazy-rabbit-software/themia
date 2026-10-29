namespace Themia.Sdk.Kit;

/// <summary>
/// [S-6705] The colours a widget may draw with, by what they mean, never by value: the theme's own
/// brushes, recoloured with the theme and the accent. Data colours (a calendar's, an app's, a pen,
/// a photo) are not roles: they come from the data through the one entry point
/// (<c>Family.Data</c>), and only as a 2 px bar, a dot or a series.
/// </summary>
public enum ColourRole
{
    /// <summary>What is read: a list row's name, a fact's value, a readout.</summary>
    TextPrimary,

    /// <summary>What labels it: a fact's name, a list row's value, the header's name, a unit.</summary>
    TextSecondary,

    /// <summary>A third line, an axis label, a status glyph, a markup mark.</summary>
    TextTertiary,

    /// <summary>A part greyed because what it shows is missing on this PC ([S-1940]).</summary>
    TextDisabled,

    /// <summary>
    /// Only for on or selected (a toggle that is on, today's circle, a checked box), the one main action
    /// (Set up, Join, Start, Play) and charts and meters (line, wash, bar, ring, slider value). Never for
    /// text on the card ([S-6706]).
    /// </summary>
    Accent,

    /// <summary>Text and glyphs on the accent.</summary>
    TextOnAccent,

    TextOnAccentSecondary,

    /// <summary>A reading over its threshold, a low battery, a full drive, falling prices, a failed fetch's glyph.</summary>
    Caution,

    /// <summary>The worse state of the same: past the critical threshold, a failure that lost something.</summary>
    Critical,

    /// <summary>A meter's or slider's rail, a ring's track (the theme's divider stroke).</summary>
    Track,

    /// <summary>A pill over an image (a cell's badge).</summary>
    Solid,

    /// <summary>A count pill's fill, a header button's hover ([S-6706]: neutral, never the accent).</summary>
    Subtle,

    /// <summary>A pressed subtle control.</summary>
    SubtlePressed,

    /// <summary>Clear, but still taking the pointer.</summary>
    Clear,

    /// <summary>A standard button's fill and its stroke.</summary>
    Control,

    ControlStroke,
}

/// <summary>[S-6705] The theme resource each role is drawn with: the one table, for the parts and the guards.</summary>
public static class ColourRoles
{
    private static readonly Dictionary<ColourRole, string> Keys = new()
    {
        [ColourRole.TextPrimary] = "TextFillColorPrimaryBrush",
        [ColourRole.TextSecondary] = "TextFillColorSecondaryBrush",
        [ColourRole.TextTertiary] = "TextFillColorTertiaryBrush",
        [ColourRole.TextDisabled] = "TextFillColorDisabledBrush",
        [ColourRole.Accent] = "AccentFillColorDefaultBrush",
        [ColourRole.TextOnAccent] = "TextOnAccentFillColorPrimaryBrush",
        [ColourRole.TextOnAccentSecondary] = "TextOnAccentFillColorSecondaryBrush",
        [ColourRole.Caution] = "SystemFillColorCautionBrush",
        [ColourRole.Critical] = "SystemFillColorCriticalBrush",
        [ColourRole.Track] = "DividerStrokeColorDefaultBrush",
        [ColourRole.Solid] = "ControlSolidFillColorDefaultBrush",
        [ColourRole.Subtle] = "SubtleFillColorSecondaryBrush",
        [ColourRole.SubtlePressed] = "SubtleFillColorTertiaryBrush",
        [ColourRole.Clear] = "SubtleFillColorTransparentBrush",
        [ColourRole.Control] = "ControlFillColorDefaultBrush",
        [ColourRole.ControlStroke] = "ControlStrokeColorDefaultBrush",
    };

    /// <summary>
    /// The theme brushes the card itself and the item layer are drawn with: not a widget's to name,
    /// but a render walk meets them on every card ([S-6731]).
    /// </summary>
    public static IReadOnlyList<string> CardKeys { get; } =
    [
        "CardBackgroundFillColorDefaultBrush", "CardStrokeColorDefaultBrush", "SolidBackgroundFillColorBaseBrush",
        "SolidBackgroundFillColorSecondaryBrush", "AccentFillColorDisabledBrush", "TextOnAccentFillColorDisabledBrush",
    ];

    /// <summary>The resource key of <paramref name="role"/> (a <c>…Brush</c> name).</summary>
    public static string Key(ColourRole role) => Keys.TryGetValue(role, out var key)
        ? key
        : throw new ArgumentOutOfRangeException(nameof(role), role, "not a colour role");

    /// <summary>Every role, in declaration order.</summary>
    public static IReadOnlyList<ColourRole> All { get; } = Enum.GetValues<ColourRole>();

    /// <summary>Whether <paramref name="key"/> is a role's resource, the one list a widget may name a brush from.</summary>
    public static bool IsRoleKey(string key) => Keys.ContainsValue(key);

    /// <summary>The role drawn with <paramref name="key"/>, if it is one.</summary>
    public static ColourRole? Of(string key)
    {
        foreach (var (role, name) in Keys)
        {
            if (string.Equals(name, key, StringComparison.Ordinal))
            {
                return role;
            }
        }

        return null;
    }
}
