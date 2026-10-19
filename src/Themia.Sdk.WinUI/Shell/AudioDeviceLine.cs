using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>The output-device line the volume tile's and the mixer's properties menus share.</summary>
public static class AudioDeviceLine
{
    private const string Scope = "audio-device";

    /// <summary>[S-1236] The output devices connected, as the menus last read them: one read per build, a different answer builds the menu again.</summary>
    private static readonly MenuLoads<IReadOnlyList<AudioDevice>> Loads = new((a, b) => a.Select(d => d.Id).SequenceEqual(b.Select(d => d.Id), StringComparer.OrdinalIgnoreCase));

    /// <summary>[S-1236] The output devices Windows lists, as held for the menu being built (a read starts; an answer that differs outdates it).</summary>
    public static MenuLoaded<IReadOnlyList<AudioDevice>> Outputs(WidgetMenuLines menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        return Loads.Read("outputs", menu, AudioDevices.OutputsAsync);
    }

    /// <summary>
    /// [S-1236] Whether the device line changes anything: with one output device (or none)
    /// "Default output" and that device are the same sound, so the line is offered only with two or
    /// more connected, or while a device is chosen (so the choice can always go back to the default),
    /// or when the devices could not be listed (the choice keeps working). Until the first answer it
    /// is not offered; the answer builds the menu again.
    /// </summary>
    public static bool Offers(MenuLoaded<IReadOnlyList<AudioDevice>> outputs, string? chosenId)
        => chosenId is not null || outputs.Failed || outputs.Value is { Count: >= 2 };

    /// <summary>
    /// [S-1222] The output device an audio widget is bound to, as a search on the properties menu
    /// ([S-3722]): Windows' devices are asked for as its submenu opens, so the list is the one
    /// connected now. "Default output" (which follows Windows) heads it, then each device; the one
    /// that holds has the dot, and a chosen device that is not connected stays listed by the name it
    /// was chosen under, so the choice is never silently lost. Null where it would change nothing
    /// (<see cref="Offers"/>, [S-1236]).
    /// </summary>
    public static ItemMenuEntry? Line(WidgetMenuLines menu, MenuLoaded<IReadOnlyList<AudioDevice>> outputs, string? chosenId, string? chosenName, Action<string?, string?> set)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(set);

        if (!Offers(outputs, chosenId))
        {
            return null;
        }

        void Pick(string? id, string? name)
        {
            set(id, name);
            menu.Save();
        }

        async Task<MenuSearchAnswer> Find(string typed, CancellationToken cancel)
        {
            IReadOnlyList<AudioDevice> devices;
            try
            {
                devices = await AudioDevices.OutputsAsync().ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The default and the chosen device are still offered: the choice keeps working.
                Log.Error(Scope, "could not list the output devices", ex);
                devices = [];
            }

            cancel.ThrowIfCancellationRequested();
            return Answer(devices, typed, chosenId, chosenName, Pick);
        }

        var value = chosenId is null
            ? Str.WidgetsC_Volume_Settings_DefaultOutput.Text()
            : chosenName is { Length: > 0 } name ? name : Str.WidgetsC_Volume_Settings_ChosenDevice.Text();

        return WidgetMenuLines.Search(Glyphs.Speakers, Str.WidgetsC_Volume_Settings_Device_Label.Text(), value,
            new MenuSearch(Str.AudioDevice_Search.Text(), Find, []));
    }

    /// <summary>
    /// [S-1222] The device lines for what is typed: Default output first whatever is typed, then the
    /// connected devices whose name holds it, then the chosen one where it is not connected. Picking
    /// the one that holds calls nothing.
    /// </summary>
    public static MenuSearchAnswer Answer(IReadOnlyList<AudioDevice> devices, string typed, string? chosenId, string? chosenName, Action<string?, string?> pick)
    {
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(pick);

        var group = Str.WidgetsC_Volume_Settings_Device_Label.Text();
        var query = (typed ?? string.Empty).Trim();

        ItemMenuEntry Line(string? id, string? name, string text)
        {
            var holds = string.Equals(id, chosenId, StringComparison.OrdinalIgnoreCase);
            return new(null, text, () =>
            {
                if (!holds)
                {
                    pick(id, name);
                }
            }, Checked: holds, Group: group);
        }

        var lines = new List<ItemMenuEntry> { Line(null, null, Str.WidgetsC_Volume_Settings_DefaultOutput.Text()) };
        var connected = false;
        foreach (var device in devices)
        {
            var name = AudioRules.DeviceName(device.Id, devices, null);
            connected |= string.Equals(device.Id, chosenId, StringComparison.OrdinalIgnoreCase);
            if (query.Length == 0 || name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                lines.Add(Line(device.Id, device.Name, name));
            }
        }

        if (chosenId is not null && !connected)
        {
            lines.Add(Line(chosenId, chosenName, Str.WidgetsC_Volume_Settings_DeviceNotConnected.Format(("device", AudioRules.DeviceName(chosenId, devices, chosenName)))));
        }

        return new MenuSearchAnswer(lines);
    }
}
