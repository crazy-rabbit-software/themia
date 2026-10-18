using Microsoft.UI.Xaml.Controls;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Shell.Config;

/// <summary>The output-device row the volume tile's and the mixer's pages share.</summary>
public static class AudioDeviceRow
{
    /// <summary>
    /// [S-1222] The output device an audio widget is bound to: "Default output" (which follows
    /// Windows) or one connected device. A chosen device that is not connected stays listed by the
    /// name it was chosen under, so the choice is never silently lost.
    /// </summary>
    public static Border Create(string? chosenId, string? chosenName, Action<string?, string?> pick)
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
                var devices = await AudioDevices.OutputsAsync().ConfigureAwait(true);

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
            null,
            box, "speaker", "headphones", "sound");
    }
}
