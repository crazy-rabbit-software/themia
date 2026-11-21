using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Themia.Widget.Music;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1479] The now-playing parts' options, all on their page ([S-3719]): Appearance ([S-3723]),
/// then when it hides ([S-1464]); what each shows is its kind ([S-14920]). Nothing about players
/// or accounts: it reads Windows' own media session. It has no settings page.
/// </summary>
internal static class MusicMenu
{
    public static void Lines(WidgetMenuLines menu, MusicPartConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        // [S-1464] When it shows at all. Edit mode always shows a hidden widget, so it can still be found and changed.
        menu.Lines.Add(WidgetMenuLines.Shared(SharedSettings.HideWhenIdle, menu.Choice(Glyphs.Hide, MusicStr.Music_HideWhenIdle_Label.Text(),
            [
                (MusicHideWhenIdle.Never, MusicStr.Music_HideWhenIdle_Never.Text()),
                (MusicHideWhenIdle.NoSession, MusicStr.WidgetsB_Settings_Music_HideWhenIdle_NoSession.Text()),
                (MusicHideWhenIdle.NotPlaying, MusicStr.WidgetsB_Settings_Music_HideWhenIdle_NotPlaying.Text()),
            ],
            config.HideWhenIdle, value => config.HideWhenIdle = value)));
    }
}
