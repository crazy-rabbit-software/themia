using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Widget.Mixer;

namespace Themia.App.Shell.Config;

/// <summary>
/// Settings rows for the mixer.
/// <para>
/// Same shape as the rest of <see cref="WidgetSettings"/>: each control writes straight to the live
/// config and saves, because the widget is on the desktop behind the window and watching it change
/// is the point. Which device first, then what the list holds, then what the rows show.
/// </para>
/// </summary>
internal static class MixerSettings
{
    public static void Rows(List<FrameworkElement> rows, MixerConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(MixerStr.WidgetsB_Settings_Mixer_Header.Text()));

        // [S-1222] Which device's apps; the default follows Windows.
        rows.Add(AudioDeviceRow.Create(config.DeviceId, config.DeviceName, (id, name) =>
        {
            config.DeviceId = id;
            config.DeviceName = name;
            save();
        }));

        // [S-442] The original request: one slider per app, not one per tab.
        rows.Add(FluentLayout.Row(Glyphs.Group, MixerStr.WidgetsB_Settings_Mixer_GroupByApp_Label.Text(),
            MixerStr.WidgetsB_Settings_Mixer_GroupByApp_Desc.Text(),
            FluentLayout.Toggle(config.GroupByApp, value => { config.GroupByApp = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Play, MixerStr.Mixer_Cfg_ActiveLabel.Text(),
            MixerStr.WidgetsB_Settings_Mixer_OnlyActive_Desc.Text(),
            FluentLayout.Toggle(config.OnlyActive, value => { config.OnlyActive = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Bell, MixerStr.Mixer_Cfg_SystemLabel.Text(), MixerStr.WidgetsB_Settings_Mixer_SystemSounds_Desc.Text(),
            FluentLayout.Toggle(config.ShowSystemSounds, value => { config.ShowSystemSounds = value; save(); })));

        rows.Add(FluentLayout.Header(MixerStr.WidgetsB_Settings_Mixer_Rows.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Apps, MixerStr.Mixer_Cfg_IconsLabel.Text(), MixerStr.WidgetsB_Settings_Mixer_Icons_Desc.Text(),
            FluentLayout.Toggle(config.ShowIcons, value => { config.ShowIcons = value; save(); })));

        rows.Add(FluentLayout.Note(MixerStr.WidgetsB_Settings_Mixer_Note.Text()));
    }
}
