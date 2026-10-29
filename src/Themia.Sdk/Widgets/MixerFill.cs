using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1624] What a mixer row's second line says, where the tier gives its rows two lines ([S-449],
/// [S-6712]): what the app is doing. Windows 11's own volume mixer gives each app a two-line row the
/// same way.
/// </summary>
public static class MixerFill
{
    /// <summary>
    /// The line under an app's name: "Playing", "Silent", "Muted"; the system-sounds row says
    /// what it is.
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
