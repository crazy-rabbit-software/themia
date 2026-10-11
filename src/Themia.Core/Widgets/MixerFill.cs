using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1624] The extra-large mixer's list when every app already fits. A short list on the left of
/// an 800 × 560 card used to stop a third of the way down; instead the rows share the height, up to
/// <see cref="MaxRowHeight"/>, and a row tall enough for a second line says what the app is doing.
/// Windows 11's own volume mixer gives each app a two-line row the same way.
/// </summary>
public static class MixerFill
{
    /// <summary>The tallest a shared-out row gets: past this a list of three reads as three banners.</summary>
    public const double MaxRowHeight = 64;

    /// <summary>From this row height the name has a caption line under it.</summary>
    public const double DetailRowHeight = 56;

    /// <summary>
    /// The row height when <paramref name="count"/> rows of <paramref name="baseHeight"/> all fit
    /// in <paramref name="height"/>: the height shared out, never below the base and never above
    /// <see cref="MaxRowHeight"/>. Rows that do not all fit keep their base height (they scroll).
    /// </summary>
    public static double RowHeight(double height, int count, double baseHeight)
    {
        if (count <= 0 || !double.IsFinite(height) || height <= 0 || baseHeight <= 0 || count * baseHeight > height + Disclosure.Tolerance)
        {
            return baseHeight;
        }

        return Math.Max(baseHeight, Math.Min(MaxRowHeight, Math.Floor(height / count)));
    }

    /// <summary>
    /// The caption under an app's name: "Playing", "Silent", "Muted"; the system-sounds row says
    /// what it is. The session count is the pill beside the name already.
    /// </summary>
    public static string Detail(MixerRow row) => Detail(row, Strings.Current);

    /// <inheritdoc cref="Detail(MixerRow)"/>
    public static string Detail(MixerRow row, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(row);

        var id = row.Muted ? Str.WidgetsB_Mixer_Detail_Muted
            : row.IsSystem ? Str.WidgetsB_Mixer_Detail_System
            : row.Active ? Str.WidgetsB_Mixer_Detail_Playing
            : Str.WidgetsB_Mixer_Detail_Silent;

        return Strings.Get(locale, id);
    }
}
