using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Sdk.WinUI.Items;

/// <summary>[S-16671] One item a list's checklist names: its id, its name, its glyph.</summary>
public readonly record struct ItemChoice(string Id, string Name, string Glyph);

/// <summary>
/// [S-16671]..[S-16673] [S-16950]..[S-16952] An item widget's lines, the same for every kind on the item base ([S-16636]; owner,
/// 2026-11-23: "order and which items are shown should be combined. settings of elements that are shown should be all
/// grouped together under a section. gap between items should be settable as blocks, from 0 to x blocks."; "Table
/// columns: You pick and order"): one reorderable checklist of the items where the user picks them (drives, devices), the
/// parts an item shows as one reorderable checklist in one section with the kind's own lines (a table's columns in that
/// order), the gap in grid blocks (not in a table), a table's column titles, and how many rows a new card stands.
/// </summary>
public static class ItemMenuLines
{
    /// <summary>The base's words for a part (the kind may say its own: a device's end is its "Percentage").</summary>
    public static string PartName(ItemPart part) => (part switch
    {
        ItemPart.Lead => Str.Items_Part_Lead,
        ItemPart.Title => Str.Items_Part_Title,
        ItemPart.Subtitle => Str.Items_Part_Subtitle,
        ItemPart.End => Str.Items_Part_End,
        ItemPart.Bar => Str.Items_Part_Bar,
        _ => Str.Items_Part_Note,
    }).Text();

    /// <summary>[S-16672] A device item's words for its parts: its end is its "Percentage".</summary>
    public static string DevicePartName(ItemPart part) => part == ItemPart.End ? Str.Items_Part_Reading.Text() : PartName(part);

    /// <param name="offered">The parts the kind's items have, in its own order.</param>
    /// <param name="choices">Every item of the kind there now, hidden ones too, in its own order; null where the items are data, not picked (mail, meetings).</param>
    /// <param name="own">The kind's own lines in the parts' section (a Bluetooth item's connection word, a meeting's place).</param>
    /// <param name="name">The kind's word for a part; null for the base's.</param>
    public static void List<TConfig>(WidgetMenuLines menu, TConfig config, IReadOnlyList<ItemPart> offered, IReadOnlyList<ItemChoice>? choices = null, IReadOnlyList<SettingsLine>? own = null, Func<ItemPart, string>? name = null)
        where TConfig : IItemList
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(offered);

        if (choices is not null && config is IItemChoice chosen)
        {
            menu.Lines.Add(Checklist(menu, chosen, choices));
        }

        menu.Lines.Add(Parts(menu, config, offered, own, name));
        menu.Lines.Add(SettingsLine.Separator);
        if (ItemLayout.TakesGap(config.Arrangement))
        {
            menu.Lines.Add(Gap(menu, config.Gap, value => config.Gap = value));
        }
        else
        {
            menu.Lines.Add(menu.Toggle(Glyphs.View, Str.Items_ColumnTitles.Text(), config.ColumnTitles, on => config.ColumnTitles = on));
        }

        menu.Lines.AddRange(KitMenuLines.Rows(menu, config));
    }

    /// <summary>
    /// [S-16671] The items of the kind as one list in the list's order: each its name with its box at its end (ticked: shown;
    /// empty: left out), dragged up and down (or Ctrl+Up/Down) to order them; an item not there keeps its place.
    /// </summary>
    public static SettingsLine Checklist(WidgetMenuLines menu, IItemChoice config, IReadOnlyList<ItemChoice> items)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(items);

        var ordered = ItemOrder.Ordered(items, d => d.Id, config.Order);
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
            config.Order = ItemOrder.Reordered(config.Order, ids);
        }

        var lines = new List<SettingsLine>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var item = ordered[i];
            var shown = !config.Hidden.Contains(item.Id, StringComparer.OrdinalIgnoreCase);
            var flip = menu.Command(null, item.Name, () =>
            {
                config.Hidden.RemoveAll(h => string.Equals(h, item.Id, StringComparison.OrdinalIgnoreCase));
                if (shown)
                {
                    config.Hidden.Add(item.Id);
                }
            });
            var moved = new Action<int, int>((from, to) => menu.Command(null, item.Name, () => Move(from, to)).Run());
            lines.Add(new SettingsItem(item.Id, item.Name, item.Glyph,
            [
                new SettingsTool("show", shown ? Glyphs.Checked : Glyphs.Unchecked, Str.Items_Show.Text(), SettingsToolKey.None) { Run = flip.Run },
            ])
            {
                Order = new SettingsOrder(i, ordered.Count, moved),
            });
        }

        if (lines.Count == 0)
        {
            lines.Add(WidgetMenuLines.Note(Str.Items_None.Text()));
        }

        return WidgetMenuLines.Submenu(Glyphs.View, Str.Items_Shown.Text(), lines);
    }

    /// <summary>
    /// [S-16672] [S-16950] The parts an item shows, together in one section: the kind's parts as one checklist in the parts' order
    /// (ticked shown; dragged or Ctrl+Up/Down to order them: a table's columns), then the kind's own lines
    /// (<paramref name="own"/>). A part left out takes no room and is not drawn.
    /// </summary>
    public static SettingsLine Parts(WidgetMenuLines menu, IItemParts config, IReadOnlyList<ItemPart> offered, IReadOnlyList<SettingsLine>? own = null, Func<ItemPart, string>? name = null)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(offered);
        name ??= PartName;

        var ordered = ItemOrder.Parts(offered, config.PartOrder).ToList();
        void Move(int from, int to)
        {
            if (from < 0 || from >= ordered.Count || to < 0 || to >= ordered.Count || from == to)
            {
                return;
            }

            var part = ordered[from];
            ordered.RemoveAt(from);
            ordered.Insert(to, part);
            config.PartOrder = [.. ordered];
        }

        var lines = new List<SettingsLine>(ordered.Count + (own?.Count ?? 0));
        for (var i = 0; i < ordered.Count; i++)
        {
            var part = ordered[i];
            var shown = !config.HiddenParts.Contains(part);
            var flip = menu.Command(null, name(part), () =>
            {
                config.HiddenParts.Remove(part);
                if (shown)
                {
                    config.HiddenParts.Add(part);
                    config.HiddenParts.Sort();
                }
            });
            var moved = new Action<int, int>((from, to) => menu.Command(null, name(part), () => Move(from, to)).Run());
            lines.Add(new SettingsItem($"part:{part}", name(part), null,
            [
                new SettingsTool("show", shown ? Glyphs.Checked : Glyphs.Unchecked, Str.Items_Show.Text(), SettingsToolKey.None) { Run = flip.Run },
            ])
            {
                Order = new SettingsOrder(i, ordered.Count, moved),
            });
        }

        lines.AddRange(own ?? []);
        return WidgetMenuLines.Submenu(Glyphs.Preview, Str.Items_Parts.Text(), lines);
    }

    /// <summary>[S-16673] The gap between items in grid blocks: none to <see cref="ItemLayout.MostGap"/>.</summary>
    public static SettingsLine Gap(WidgetMenuLines menu, int gap, Action<int> set)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(set);
        var options = Enumerable.Range(0, ItemLayout.MostGap + 1).Select(n => (n, n switch
        {
            0 => Str.Items_Gap_None.Text(),
            1 => Str.Items_Gap_One.Text(),
            _ => Str.Items_Gap_Blocks.Format(("count", n.ToString(System.Globalization.CultureInfo.InvariantCulture))),
        })).ToList();
        return menu.Choice(Glyphs.Grid, Str.Items_Gap.Text(), options, Math.Clamp(gap, 0, ItemLayout.MostGap), set);
    }
}
