using System.Collections.ObjectModel;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Sdk.WinUI.Devices;

/// <summary>[S-16655] One device a list's lines name: its id, its name, its glyph.</summary>
public readonly record struct DeviceChoice(string Id, string Name, string Glyph);

/// <summary>
/// [S-16655] A device list's lines, the same for every kind ([S-16636]): the devices of its kind as a checklist (a switch
/// each, on while it is shown), the shown ones' order (a list dragged to reorder; the checklist hides, so nothing is
/// removed there), the gap between them and how many rows a new card stands.
/// </summary>
public static class DeviceMenuLines
{
    /// <param name="devices">Every device of the list's kind there now, hidden ones too, in the kind's own order.</param>
    public static void List<TConfig>(WidgetMenuLines menu, TConfig config, IReadOnlyList<DeviceChoice> devices)
        where TConfig : IDeviceListConfig, IKitRows
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(devices);

        // [S-16651] The checklist: every device of the kind, in the list's order.
        var ordered = DeviceOrder.Ordered(devices, d => d.Id, config.Order);
        var checklist = new List<SettingsLine>(ordered.Count);
        foreach (var device in ordered)
        {
            var id = device.Id;
            var shown = !config.Hidden.Contains(id, StringComparer.OrdinalIgnoreCase);
            checklist.Add(menu.Toggle(device.Glyph, device.Name, shown, on =>
            {
                config.Hidden.RemoveAll(h => string.Equals(h, id, StringComparison.OrdinalIgnoreCase));
                if (!on)
                {
                    config.Hidden.Add(id);
                }
            }));
        }

        if (checklist.Count == 0)
        {
            checklist.Add(WidgetMenuLines.Note(Str.Devices_None.Text()));
        }

        menu.Lines.Add(WidgetMenuLines.Submenu(Glyphs.View, Str.Devices_Shown.Text(), checklist));

        // [S-16652] The shown ones' order, dragged up and down.
        var shownDevices = DeviceOrder.Arrange(devices, d => d.Id, config.Hidden, config.Order);
        var order = new OrderedIds(config, [.. shownDevices.Select(d => d.Id)]);
        menu.Lines.Add(menu.List(Glyphs.MoveDown, Str.Devices_Order.Text(), order, new MenuList<string>(
            id => shownDevices.FirstOrDefault(d => d.Id == id) is { Name: { } name } ? name : id,
            Str.Devices_None.Text())
        {
            Glyph = id => shownDevices.FirstOrDefault(d => d.Id == id) is { Glyph: { } glyph } ? glyph : null,
            Min = int.MaxValue,
        }));

        // [S-16653] The block gap, then [S-16654] how many rows a new card stands.
        menu.Lines.Add(SettingsLine.Separator);
        menu.Lines.Add(menu.Toggle(Glyphs.Grid, Str.Devices_Gap.Text(), config.Gap, on => config.Gap = on));
        menu.Lines.AddRange(KitMenuLines.Rows(menu, config));
    }

    /// <summary>
    /// [S-16652] The shown devices' ids as the order list edits them: every change is written back to the config's order
    /// (<see cref="DeviceOrder.Reordered"/>), so a device not there now keeps its place.
    /// </summary>
    private sealed class OrderedIds(IDeviceListConfig config, List<string> ids) : Collection<string>(ids)
    {
        protected override void InsertItem(int index, string item)
        {
            base.InsertItem(index, item);
            config.Order = DeviceOrder.Reordered(config.Order, this);
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            config.Order = DeviceOrder.Reordered(config.Order, this);
        }

        protected override void SetItem(int index, string item)
        {
            base.SetItem(index, item);
            config.Order = DeviceOrder.Reordered(config.Order, this);
        }
    }
}
