using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Widget.Music;

namespace Themia.App.Shell.Config;

/// <summary>
/// Settings rows for the now-playing tile.
/// <para>
/// Nothing about accounts or players: the widget reads Windows' own media session, so there is
/// nothing to sign in to and nothing to choose between.
/// </para>
/// </summary>
internal static class MusicSettings
{
    public static void Rows(List<FrameworkElement> rows, MusicConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(MusicStr.WidgetsB_Settings_Music_Header.Text()));

        // [S-6406] Whether the art shows, and how, is the widget's style, on the properties panel.

        rows.Add(FluentLayout.Row(Glyphs.Play, MusicStr.WidgetsB_Settings_Music_Transport_Label.Text(),
            MusicStr.WidgetsB_Settings_Music_Transport_Desc.Text(),
            FluentLayout.Toggle(config.ShowTransport, value => { config.ShowTransport = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Motion, MusicStr.WidgetsB_Settings_Music_Progress_Label.Text(),
            MusicStr.WidgetsB_Settings_Music_Progress_Desc.Text(),
            FluentLayout.Toggle(config.ShowProgress, value => { config.ShowProgress = value; save(); })));

        // [S-1464] Edit mode always shows a hidden widget, so it can still be found and changed.
        rows.Add(FluentLayout.Row(Glyphs.Hide, MusicStr.Music_HideWhenIdle_Label.Text(),
            MusicStr.WidgetsB_Settings_Music_HideWhenIdle_Desc.Text(),
            FluentLayout.Choice<MusicHideWhenIdle>(
                [
                    (MusicHideWhenIdle.Never, MusicStr.Music_HideWhenIdle_Never.Text()),
                    (MusicHideWhenIdle.NoSession, MusicStr.WidgetsB_Settings_Music_HideWhenIdle_NoSession.Text()),
                    (MusicHideWhenIdle.NotPlaying, MusicStr.WidgetsB_Settings_Music_HideWhenIdle_NotPlaying.Text()),
                ],
                config.HideWhenIdle,
                value => { config.HideWhenIdle = value; save(); })));
    }
}
