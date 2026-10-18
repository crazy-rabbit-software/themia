using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Shell.Config;

/// <summary>
/// Settings rows for the volume tile (the output-device row it shares with the mixer is the SDK's <see cref="AudioDeviceRow"/>).
/// <para>
/// Same shape as the rest of <see cref="WidgetSettings"/>: each control writes straight to the live
/// config and saves, because the widget is on the desktop behind the window and watching it change
/// is the point.
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    public static void Volume(List<FrameworkElement> rows, VolumeConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.Widget_Volume_Label.Text()));

        rows.Add(AudioDeviceRow.Create(config.DeviceId, config.DeviceName, (id, name) =>
        {
            config.DeviceId = id;
            config.DeviceName = name;
            save();
        }));

        // [S-2730] Nothing to show or hide: the tile is the slider.
        rows.Add(FluentLayout.Header(Str.WidgetsC_Volume_Settings_Behaviour.Text()));

        // Named for what it does rather than "read-only": a desktop widget sits under the path of
        // every pointer on the screen, and somebody who wants a gauge and not a control should be
        // able to say so without losing the gauge.
        rows.Add(FluentLayout.Row(Glyphs.Edit, Str.WidgetsC_Volume_Settings_Allow_Label.Text(),
            Str.WidgetsC_Volume_Settings_Allow_Desc.Text(),
            FluentLayout.Toggle(config.AllowControl, value => { config.AllowControl = value; save(); })));

        rows.Add(FluentLayout.Note(Str.WidgetsC_Volume_Settings_KeysNote.Text()));
    }
}
