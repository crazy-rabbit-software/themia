using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Mixer;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1235] The mixer's options, all on its properties menu ([S-3719]): Appearance ([S-3723]), the
/// output device ([S-1222], only where there is a choice, [S-1236]), which rows the list holds, what
/// a row shows, and how the keys work, each group behind a divider. It has no settings page.
/// </summary>
internal static class MixerMenu
{
    /// <param name="outputs">The output devices connected (<c>AudioDeviceLine.Outputs(menu)</c>).</param>
    public static void Lines(WidgetMenuLines menu, MixerConfig config, MenuLoaded<IReadOnlyList<AudioDevice>> outputs)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        var lines = menu.Lines;

        // [S-1222] Which device's apps; the default follows Windows.
        if (AudioDeviceLine.Line(menu, outputs, config.DeviceId, config.DeviceName, (id, name) =>
        {
            config.DeviceId = id;
            config.DeviceName = name;
        }) is { } device)
        {
            lines.Add(device);
            lines.Add(ItemMenuEntry.Separator);
        }

        // [S-442] The original request: one slider per app, not one per tab. [S-445] Which apps.
        lines.Add(menu.Toggle(Glyphs.Group, MixerStr.WidgetsB_Settings_Mixer_GroupByApp_Label.Text(), config.GroupByApp, on => config.GroupByApp = on));
        lines.Add(menu.Toggle(Glyphs.Play, MixerStr.Mixer_Cfg_ActiveLabel.Text(), config.OnlyActive, on => config.OnlyActive = on));
        lines.Add(menu.Toggle(Glyphs.Bell, MixerStr.Mixer_Cfg_SystemLabel.Text(), config.ShowSystemSounds, on => config.ShowSystemSounds = on));

        // [S-446] What a row shows.
        lines.Add(ItemMenuEntry.Separator);
        lines.Add(menu.Toggle(Glyphs.Apps, MixerStr.Mixer_Cfg_IconsLabel.Text(), config.ShowIcons, on => config.ShowIcons = on));

        // [S-1225] The keys are found nowhere else.
        lines.Add(ItemMenuEntry.Separator);
        lines.Add(WidgetMenuLines.Note(MixerStr.WidgetsB_Settings_Mixer_Note.Text()));
    }
}
