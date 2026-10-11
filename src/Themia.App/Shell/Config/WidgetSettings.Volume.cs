using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>
/// Settings rows for the volume tile, and the output-device row it shares with the mixer.
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

        rows.Add(AudioDeviceRow(config.DeviceId, config.DeviceName, (id, name) =>
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

    /// <summary>
    /// [S-1222] The output device an audio widget is bound to: "Default output" (which follows
    /// Windows) or one connected device. A chosen device that is not connected stays listed by the
    /// name it was chosen under, so the choice is never silently lost.
    /// </summary>
    private static Border AudioDeviceRow(string? chosenId, string? chosenName, Action<string?, string?> pick)
    {
        var box = new ComboBox { MinWidth = 220, MaxWidth = 320 };
        box.Items.Add(new ComboBoxItem { Content = Str.WidgetsC_Volume_Settings_DefaultOutput.Text(), Tag = null });
        box.SelectedIndex = 0;

        if (chosenId is not null)
        {
            box.Items.Add(new ComboBoxItem { Content = chosenName ?? Str.WidgetsC_Volume_Settings_ChosenDevice.Text(), Tag = new AudioDevice(chosenId, chosenName ?? string.Empty) });
            box.SelectedIndex = 1;
        }

        var filling = true;

        box.SelectionChanged += (_, _) =>
        {
            if (filling || box.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            var device = item.Tag as AudioDevice;
            pick(device?.Id, device?.Name);
        };

        _ = FillAsync();

        async Task FillAsync()
        {
            try
            {
                var devices = await CoreAudio.OutputDevicesAsync().ConfigureAwait(true);

                filling = true;
                box.Items.Clear();
                box.Items.Add(new ComboBoxItem { Content = Str.WidgetsC_Volume_Settings_DefaultOutput.Text(), Tag = null });
                var selected = 0;

                foreach (var device in devices)
                {
                    box.Items.Add(new ComboBoxItem { Content = AudioRules.DeviceName(device.Id, devices, null), Tag = device });

                    if (string.Equals(device.Id, chosenId, StringComparison.OrdinalIgnoreCase))
                    {
                        selected = box.Items.Count - 1;
                    }
                }

                if (chosenId is not null && selected == 0)
                {
                    box.Items.Add(new ComboBoxItem
                    {
                        Content = Str.WidgetsC_Volume_Settings_DeviceNotConnected.Format(("device", AudioRules.DeviceName(chosenId, devices, chosenName))),
                        Tag = new AudioDevice(chosenId, chosenName ?? string.Empty),
                    });
                    selected = box.Items.Count - 1;
                }

                box.SelectedIndex = selected;
            }
            catch (Exception ex)
            {
                Log.Error("settings", "could not list the output devices", ex);
            }
            finally
            {
                filling = false;
            }
        }

        return FluentLayout.Row(Glyphs.Speakers, Str.WidgetsC_Volume_Settings_Device_Label.Text(),
            Str.WidgetsC_Volume_Settings_Device_Desc.Text(),
            box, "speaker", "headphones", "sound");
    }
}
