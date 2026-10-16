using Themia.Core.Widgets;
using Themia.Core.Localization;

namespace Themia.Core.Model;

/// <summary>A design preset's id. Closed set: it is what the picker stores and compares.</summary>
public enum DesignPresetId
{
    Default = 0,
    Midnight = 1,
    Daylight = 2,
    Glass = 3,
    Terminal = 4,
    Paper = 5,
}

/// <summary>
/// The look a preset sets: the thirteen appearance fields it owns, and nothing else. [S-830]
/// </summary>
public sealed record DesignPresetValues(
    AppTheme Theme,
    string PrimaryColor,
    string FontFamily,
    string WidgetBackgroundColor,
    double SurfaceOpacity,
    string HeaderBackgroundColor,
    double HeaderOpacity,
    WidgetBorderStyle BorderStyle,
    double BorderWidthPx,
    string BorderColor,
    double BorderOpacity,
    double CornerRadiusPx,
    double WidgetGapPx);

/// <summary>One named look.</summary>
public sealed record DesignPreset(DesignPresetId Id, Str LabelId, DesignPresetValues Values)
{
    /// <summary>The preset's name in the current language (the list is static, so it is read when shown).</summary>
    public string Label => LabelId.Text();
}

/// <summary>
/// Named bundles of appearance settings, applied in one write — the original's six
/// (<c>src/designPresets.ts</c>). [S-830]..[S-833]
/// <para>
/// A preset owns the <b>look</b>: theme, accent, font family, widget and header fill, border, shape
/// and gap. It never owns the widget scale, the text size or any animation setting: those are the
/// user's accessibility and motion choices, and applying a colour scheme must not reset someone's
/// 150% text to 100% or switch motion back on.
/// </para>
/// </summary>
public static class DesignPresets
{
    /// <summary>
    /// [S-831] Default is whatever a fresh <see cref="DesignState"/> holds, read from it rather than
    /// copied, so a fresh install reads as "Default" and not "Custom" even after a default changes.
    /// </summary>
    public static IReadOnlyList<DesignPreset> All { get; } =
    [
        new(DesignPresetId.Default, Str.Settings_Style_Preset_Name_Default, ValuesOf(new DesignState())),

        // Near-black, tight, high contrast.
        new(DesignPresetId.Midnight, Str.Settings_Style_Preset_Name_Midnight, new(
            AppTheme.Dark, "#7C8CFF", DesignLook.SystemFont, "#0B0B0F", 0.96, "#131320", 0.96,
            WidgetBorderStyle.Solid, 1, "#7C8CFF", 0.18, 6, 10)),

        // Light theme, soft and airy.
        new(DesignPresetId.Daylight, Str.Settings_Style_Preset_Name_Daylight, new(
            AppTheme.Light, "#0078D4", DesignLook.SystemFont, "#FFFFFF", 0.86, "#F3F4F8", 0.92,
            WidgetBorderStyle.Solid, 1, "#000000", 0.1, 12, 14)),

        // Heavy glass: very translucent, borderless, generously rounded.
        new(DesignPresetId.Glass, Str.Settings_Style_Preset_Name_Glass, new(
            AppTheme.Dark, "#8AD8FF", DesignLook.SystemFont, "#1B2030", 0.55, "#1B2030", 0.35,
            WidgetBorderStyle.None, 0, "#FFFFFF", 0.25, 18, 16)),

        // Monospace, square, phosphor green.
        new(DesignPresetId.Terminal, Str.Settings_Style_Preset_Name_Terminal, new(
            AppTheme.Dark, "#3DDC84", DesignLook.MonoFont, "#050D07", 0.97, "#050D07", 0.97,
            WidgetBorderStyle.Solid, 1, "#3DDC84", 0.45, 0, 8)),

        // Light, serif, printed-page feel: opaque cards, hairline rules.
        new(DesignPresetId.Paper, Str.Settings_Style_Preset_Name_Paper, new(
            AppTheme.Light, "#8A5A2B", DesignLook.SerifFont, "#FAF7F0", 1, "#F0E9DC", 1,
            WidgetBorderStyle.Solid, 1, "#8A5A2B", 0.22, 4, 12)),
    ];

    /// <summary>The preset with this id. Every id has one; an undefined value is Default.</summary>
    public static DesignPreset Get(DesignPresetId id)
    {
        foreach (var preset in All)
        {
            if (preset.Id == id)
            {
                return preset;
            }
        }

        return All[0];
    }

    /// <summary>The thirteen owned fields of a design.</summary>
    public static DesignPresetValues ValuesOf(DesignState design)
    {
        ArgumentNullException.ThrowIfNull(design);

        return new(
            design.Theme,
            design.PrimaryColor,
            design.FontFamily,
            design.WidgetBackgroundColor,
            design.SurfaceOpacity,
            design.HeaderBackgroundColor,
            design.HeaderOpacity,
            design.BorderStyle,
            design.BorderWidthPx,
            design.BorderColor,
            design.BorderOpacity,
            design.CornerRadiusPx,
            design.WidgetGapPx);
    }

    /// <summary>
    /// [S-832] Write every field the preset owns into <paramref name="design"/>, in one call (one
    /// mutation, one save, one apply). Fields it does not own are left exactly as they were.
    /// </summary>
    public static void Apply(DesignState design, DesignPresetId id)
    {
        ArgumentNullException.ThrowIfNull(design);
        var v = Get(id).Values;

        design.Theme = v.Theme;
        design.PrimaryColor = v.PrimaryColor;
        design.FontFamily = v.FontFamily;
        design.WidgetBackgroundColor = v.WidgetBackgroundColor;
        design.SurfaceOpacity = v.SurfaceOpacity;
        design.HeaderBackgroundColor = v.HeaderBackgroundColor;
        design.HeaderOpacity = v.HeaderOpacity;
        design.BorderStyle = v.BorderStyle;
        design.BorderWidthPx = v.BorderWidthPx;
        design.BorderColor = v.BorderColor;
        design.BorderOpacity = v.BorderOpacity;
        design.CornerRadiusPx = v.CornerRadiusPx;
        design.WidgetGapPx = v.WidgetGapPx;
    }

    /// <summary>
    /// [S-833] The preset this design is, or null for "Custom". Only the owned fields are compared,
    /// so changing the text size or the widget scale after picking a preset still reads as that
    /// preset. Colours compare case-insensitively (the picker stores upper case, a file may not),
    /// numbers to within a rounding error a slider can leave.
    /// </summary>
    public static DesignPresetId? Match(DesignState design)
    {
        ArgumentNullException.ThrowIfNull(design);
        var current = ValuesOf(design);

        foreach (var preset in All)
        {
            if (Same(current, preset.Values))
            {
                return preset.Id;
            }
        }

        return null;
    }

    /// <summary>
    /// [S-6483] Whether picking <paramref name="target"/> would throw away a look the user made: the
    /// design matches no preset (it reads as Custom), so its owned fields exist nowhere else and a
    /// preset overwrites them for good. A design that is a preset loses nothing — that preset can be
    /// picked again — and fields no preset owns (text size, widget scale, motion) never count.
    /// </summary>
    public static bool ReplacesCustomLook(DesignState design, DesignPresetId target)
    {
        ArgumentNullException.ThrowIfNull(design);
        return Match(design) is null && !Same(ValuesOf(design), Get(target).Values);
    }

    private static bool Same(DesignPresetValues a, DesignPresetValues b)
        => a.Theme == b.Theme
            && SameText(a.PrimaryColor, b.PrimaryColor)
            && SameText(a.FontFamily, b.FontFamily)
            && SameText(a.WidgetBackgroundColor, b.WidgetBackgroundColor)
            && Near(a.SurfaceOpacity, b.SurfaceOpacity)
            && SameText(a.HeaderBackgroundColor, b.HeaderBackgroundColor)
            && Near(a.HeaderOpacity, b.HeaderOpacity)
            && a.BorderStyle == b.BorderStyle
            && Near(a.BorderWidthPx, b.BorderWidthPx)
            && SameText(a.BorderColor, b.BorderColor)
            && Near(a.BorderOpacity, b.BorderOpacity)
            && Near(a.CornerRadiusPx, b.CornerRadiusPx)
            && Near(a.WidgetGapPx, b.WidgetGapPx);

    private static bool SameText(string? a, string? b)
        => string.Equals(a?.Trim() ?? string.Empty, b?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private static bool Near(double a, double b) => Math.Abs(a - b) < 0.001;
}
