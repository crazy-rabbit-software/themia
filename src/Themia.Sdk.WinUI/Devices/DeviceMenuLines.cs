using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Sdk.WinUI.Devices;

/// <summary>[S-16671] One device a list's lines name: its id, its name, its glyph.</summary>
public readonly record struct DeviceChoice(string Id, string Name, string Glyph);

/// <summary>
/// [S-16671]..[S-16673] A device widget's lines, the same for every kind ([S-16636]; owner, 2026-11-23: "order and which
/// items are shown should be combined. settings of elements that are shown should be all grouped together under a
/// section. gap between items should be settable as blocks, from 0 to x blocks."): one reorderable checklist of the
/// devices of its kind (its box shows or hides one, dragging or Ctrl+Up/Down orders them), the parts an item shows in one
/// section with the kind's own, the gap in grid blocks and how many rows a new card stands.
/// </summary>
public static class DeviceMenuLines
{
    /// <param name="devices">Every device of the list's kind there now, hidden ones too, in the kind's own order.</param>
    /// <param name="own">The kind's own parts' lines (a Bluetooth device's connection word, its signal), in the parts' section.</param>
    public static void List<TConfig>(WidgetMenuLines menu, TConfig config, IReadOnlyList<DeviceChoice> devices, IReadOnlyList<SettingsLine>? own = null)
        where TConfig : IDeviceListConfig, IDeviceParts, IKitRows
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(devices);

        menu.Lines.Add(Checklist(menu, config, devices));
        menu.Lines.Add(Parts(menu, config, own));
        menu.Lines.Add(SettingsLine.Separator);
        menu.Lines.Add(Gap(menu, config.Gap, value => config.Gap = value));
        menu.Lines.AddRange(KitMenuLines.Rows(menu, config));
    }

    /// <summary>
    /// [S-16671] The devices of the kind as one list in the list's order: each its name with its box at its end (ticked:
    /// shown; empty: left out), dragged up and down (or Ctrl+Up/Down) to order them; a device not there keeps its place.
    /// </summary>
    public static SettingsLine Checklist(WidgetMenuLines menu, IDeviceListConfig config, IReadOnlyList<DeviceChoice> devices)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(devices);

        var ordered = DeviceOrder.Ordered(devices, d => d.Id, config.Order);
        var ids = ordered.Select(d => d.Id).ToList();
        void Move(int from, int to)
        {
            if (from < 0 || from >= ids.Count || to < 0 || to >= ids.Count || from == to)
            {
                return;
            }

            var id = ids[from];
            ids.RemoveAt(from);
            ids.Insert(to, id);
            config.Order = DeviceOrder.Reordered(config.Order, ids);
        }

        var lines = new List<SettingsLine>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var device = ordered[i];
            var shown = !config.Hidden.Contains(device.Id, StringComparer.OrdinalIgnoreCase);
            var flip = menu.Command(null, device.Name, () =>
            {
                config.Hidden.RemoveAll(h => string.Equals(h, device.Id, StringComparison.OrdinalIgnoreCase));
                if (shown)
                {
                    config.Hidden.Add(device.Id);
                }
            });
            var moved = new Action<int, int>((from, to) => menu.Command(null, device.Name, () => Move(from, to)).Run());
            lines.Add(new SettingsItem(device.Id, device.Name, device.Glyph,
            [
                new SettingsTool("show", shown ? Glyphs.Checked : Glyphs.Unchecked, Str.Devices_Show.Text(), SettingsToolKey.None) { Run = flip.Run },
            ])
            {
                Order = new SettingsOrder(i, ordered.Count, moved),
            });
        }

        if (lines.Count == 0)
        {
            lines.Add(WidgetMenuLines.Note(Str.Devices_None.Text()));
        }

        return WidgetMenuLines.Submenu(Glyphs.View, Str.Devices_Shown.Text(), lines);
    }

    /// <summary>
    /// [S-16672] The parts an item shows, together in one section: its icon, its details, its percentage, its bar, then the
    /// kind's own (<paramref name="own"/>). A part left out takes no room and is not drawn.
    /// </summary>
    public static SettingsLine Parts(WidgetMenuLines menu, IDeviceParts config, IReadOnlyList<SettingsLine>? own = null)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        var lines = new List<SettingsLine>();
        foreach (var part in Enum.GetValues<DevicePart>())
        {
            lines.Add(menu.Toggle(null, PartName(part), !config.HiddenParts.Contains(part), on =>
            {
                config.HiddenParts.Remove(part);
                if (!on)
                {
                    config.HiddenParts.Add(part);
                    config.HiddenParts.Sort();
                }
            }));
        }

        lines.AddRange(own ?? []);
        return WidgetMenuLines.Submenu(Glyphs.Preview, Str.Devices_Parts.Text(), lines);
    }

    /// <summary>[S-16673] The gap between items in grid blocks: none to <see cref="DeviceLayout.MostGap"/>.</summary>
    public static SettingsLine Gap(WidgetMenuLines menu, int gap, Action<int> set)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(set);
        var options = Enumerable.Range(0, DeviceLayout.MostGap + 1).Select(n => (n, n switch
        {
            0 => Str.Devices_Gap_None.Text(),
            1 => Str.Devices_Gap_One.Text(),
            _ => Str.Devices_Gap_Blocks.Format(("count", n.ToString(System.Globalization.CultureInfo.InvariantCulture))),
        })).ToList();
        return menu.Choice(Glyphs.Grid, Str.Devices_Gap.Text(), options, Math.Clamp(gap, 0, DeviceLayout.MostGap), set);
    }

    /// <summary>[S-16672] A part in words.</summary>
    public static string PartName(DevicePart part) => (part switch
    {
        DevicePart.Icon => Str.Devices_Part_Icon,
        DevicePart.Details => Str.Devices_Part_Details,
        DevicePart.Reading => Str.Devices_Part_Reading,
        _ => Str.Devices_Part_Bar,
    }).Text();
}
