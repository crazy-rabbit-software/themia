using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Themia.Widget.Volume;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-2734] The volume tile's options, all on its properties menu ([S-3719]): Appearance
/// ([S-3723]), the output device ([S-1222]), whether it may change the volume, and how the keys
/// work. Nothing to show or hide: the tile is the slider ([S-2730]). It has no settings page.
/// </summary>
internal static class VolumeMenu
{
    public static void Lines(WidgetMenuLines menu, VolumeConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.PlaceCard();
        var lines = menu.Lines;
        lines.Add(AudioDeviceLine.Line(menu, config.DeviceId, config.DeviceName, (id, name) =>
        {
            config.DeviceId = id;
            config.DeviceName = name;
        }));

        // Named for what it does rather than "read-only": a desktop widget sits under the path of
        // every pointer on the screen, and somebody who wants a gauge and not a control should be
        // able to say so without losing the gauge.
        lines.Add(menu.Toggle(Glyphs.Edit, VolumeStr.WidgetsC_Volume_Settings_Allow_Label.Text(), config.AllowControl, on => config.AllowControl = on));

        lines.Add(ItemMenuEntry.Separator);
        lines.Add(WidgetMenuLines.Note(VolumeStr.WidgetsC_Volume_Settings_KeysNote.Text()));
    }
}
