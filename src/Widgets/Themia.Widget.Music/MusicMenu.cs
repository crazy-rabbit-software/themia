using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Themia.Widget.Music;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1479] Now playing's options, all on its properties menu ([S-3719]): Appearance ([S-3723]),
/// then the transport and the progress switches and, behind a divider, when it hides ([S-1464]). Nothing about players
/// or accounts: it reads Windows' own media session. It has no settings page.
/// </summary>
internal static class MusicMenu
{
    public static void Lines(WidgetMenuLines menu, MusicConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.Lines.Add(menu.Toggle(Glyphs.Play, MusicStr.WidgetsB_Settings_Music_Transport_Label.Text(), config.ShowTransport, on => config.ShowTransport = on));
        menu.Lines.Add(menu.Toggle(Glyphs.Motion, MusicStr.WidgetsB_Settings_Music_Progress_Label.Text(), config.ShowProgress, on => config.ShowProgress = on));

        // [S-1464] When it shows at all, apart from what it shows. Edit mode always shows a hidden widget, so it can still be found and changed.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(menu.Choice(Glyphs.Hide, MusicStr.Music_HideWhenIdle_Label.Text(),
            [
                (MusicHideWhenIdle.Never, MusicStr.Music_HideWhenIdle_Never.Text()),
                (MusicHideWhenIdle.NoSession, MusicStr.WidgetsB_Settings_Music_HideWhenIdle_NoSession.Text()),
                (MusicHideWhenIdle.NotPlaying, MusicStr.WidgetsB_Settings_Music_HideWhenIdle_NotPlaying.Text()),
            ],
            config.HideWhenIdle, value => config.HideWhenIdle = value));
    }
}
