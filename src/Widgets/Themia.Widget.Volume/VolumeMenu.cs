using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Volume;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-2734] The volume tile's options, all on its properties menu ([S-3719]): Appearance
/// ([S-3723]), the output device ([S-1222], only where there is a choice, [S-1236]), then whether it
/// may change the volume and, while it may, how the keys work. Nothing to show or hide: the tile is
/// the slider ([S-2730]). It has no settings page.
/// </summary>
internal static class VolumeMenu
{
    /// <param name="outputs">The output devices connected (<c>AudioDeviceLine.Outputs(menu)</c>).</param>
    public static void Lines(WidgetMenuLines menu, VolumeConfig config, MenuLoaded<IReadOnlyList<AudioDevice>> outputs)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        var lines = menu.Lines;
        if (AudioDeviceLine.Line(menu, outputs, config.DeviceId, config.DeviceName, (id, name) =>
        {
            config.DeviceId = id;
            config.DeviceName = name;
        }) is { } device)
        {
            lines.Add(device);
            lines.Add(ItemMenuEntry.Separator);
        }

        // Named for what it does rather than "read-only": a desktop widget sits under the path of
        // every pointer on the screen, and somebody who wants a gauge and not a control should be
        // able to say so without losing the gauge.
        lines.Add(menu.Toggle(Glyphs.Edit, VolumeStr.WidgetsC_Volume_Settings_Allow_Label.Text(), config.AllowControl, on => config.AllowControl = on));

        // [S-1226] The keys are ignored while the tile may not change the volume: nothing to tell then.
        if (config.AllowControl)
        {
            lines.Add(WidgetMenuLines.Note(VolumeStr.WidgetsC_Volume_Settings_KeysNote.Text()));
        }
    }
}
