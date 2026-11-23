using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Sdk.WinUI;
using Themia.Widget.Music;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1479] [S-16501] Now playing's options, all on its page ([S-3719]): under the Style picker what its face shows, every
/// element on its own (owner, 2026-11-22: "hide almost every element separately, e. g. the pause play, next previous, artist,
/// cover, etc."), in three cards: the track (the cover, where the style draws one, the title, the artist, the album), the
/// progress (the elapsed time, the bar, the length) and the controls (previous, play or pause, next); then when it hides
/// ([S-1464]). Nothing about players or accounts: it reads Windows' own media session. It has no settings page.
/// </summary>
internal static class MusicMenu
{
    public static void Lines(WidgetMenuLines menu, MusicConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        SettingsLine Element(MusicElement element)
            => menu.Toggle(null, Name(element), config.Has(element), on => config.Shows = MusicElements.Toggle(config.Shows, element, on));

        // [S-16506] The strip draws no cover: its page offers none.
        MusicElement[] track = config.Style == MusicStyle.Strip
            ? [MusicElement.Title, MusicElement.Artist, MusicElement.Album]
            : [MusicElement.Cover, MusicElement.Title, MusicElement.Artist, MusicElement.Album];
        menu.StyleLines.Add(WidgetMenuLines.Submenu(Glyphs.MusicAlbum, MusicStr.Music_Shows_Track.Text(), [.. track.Select(Element)]));
        menu.StyleLines.Add(WidgetMenuLines.Submenu(Glyphs.ProgressBar, MusicStr.Music_Shows_Progress.Text(),
            [.. new[] { MusicElement.Elapsed, MusicElement.Bar, MusicElement.Length }.Select(Element)]));
        menu.StyleLines.Add(WidgetMenuLines.Submenu(Glyphs.Play, MusicStr.Music_Shows_Controls.Text(),
            [.. new[] { MusicElement.Previous, MusicElement.PlayPause, MusicElement.Next }.Select(Element)]));

        // [S-1464] When it shows at all. Edit mode always shows a hidden widget, so it can still be found and changed.
        menu.Lines.Add(WidgetMenuLines.Shared(SharedSettings.HideWhenIdle, menu.Choice(Glyphs.Hide, MusicStr.Music_HideWhenIdle_Label.Text(),
            [
                (MusicHideWhenIdle.Never, MusicStr.Music_HideWhenIdle_Never.Text()),
                (MusicHideWhenIdle.NoSession, MusicStr.WidgetsB_Settings_Music_HideWhenIdle_NoSession.Text()),
                (MusicHideWhenIdle.NotPlaying, MusicStr.WidgetsB_Settings_Music_HideWhenIdle_NotPlaying.Text()),
            ],
            config.HideWhenIdle, value => config.HideWhenIdle = value)));
    }

    /// <summary>[S-16501] An element's name on the page.</summary>
    internal static string Name(MusicElement element) => element switch
    {
        MusicElement.Cover => MusicStr.Music_Element_Cover.Text(),
        MusicElement.Title => MusicStr.Music_Element_Title.Text(),
        MusicElement.Artist => MusicStr.Music_Element_Artist.Text(),
        MusicElement.Album => MusicStr.Music_Element_Album.Text(),
        MusicElement.Elapsed => MusicStr.Music_Element_Elapsed.Text(),
        MusicElement.Bar => MusicStr.Music_Element_Bar.Text(),
        MusicElement.Length => MusicStr.Music_Element_Length.Text(),
        MusicElement.Previous => MusicStr.WidgetsB_Music_Previous.Text(),
        MusicElement.PlayPause => MusicStr.WidgetsB_Music_PlayPause.Text(),
        _ => MusicStr.WidgetsB_Music_Next.Text(),
    };
}
