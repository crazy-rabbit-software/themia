using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.Items;

/// <summary>
/// [S-16940] How an item list arranges its items: the item widgets' styles (owner, 2026-11-23: "be sure to implement this for
/// all the other widgets too: row list, column list, grid view, list view (current bluetooth)"; "there should also be a base
/// style where this data is shown as a table and not the list"). Every kind on the item base offers the ones that fit its
/// data; the default is the device list's.
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<ItemStyle>))]
public enum ItemStyle
{
    /// <summary>[S-16941] Each item its own block, stacked (the device list: icon, name over details, value, bar under).</summary>
    List = 0,

    /// <summary>[S-16942] The items side by side across, each a column of its parts.</summary>
    RowList = 1,

    /// <summary>[S-16943] The items stacked in one column of compact rows.</summary>
    ColumnList = 2,

    /// <summary>[S-16944] The items as cards in rows and columns.</summary>
    Grid = 3,

    /// <summary>[S-16945] The items as rows and their parts as aligned columns, with optional column titles.</summary>
    Table = 4,
}

/// <summary>
/// [S-16946] The parts of an item, each a slot that is drawn or not (owner, 2026-11-23: "if elements are not shown ... it
/// should also not show them in the design, not just grey them out"; "Table columns: You pick and order"): a part left out,
/// or one an item has none of, takes no room and the others close up. In the table each part shown is a column, in the
/// list's order of them.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ItemPart>))]
public enum ItemPart
{
    /// <summary>The icon, picture, monogram or glyph that leads the item.</summary>
    Lead = 0,

    /// <summary>The item's name.</summary>
    Title = 1,

    /// <summary>The line under the name: its figures in words, the sender, the time range.</summary>
    Subtitle = 2,

    /// <summary>The end slot: a value ("55 %"), an action ("Join") or a time.</summary>
    End = 3,

    /// <summary>The bar under the item (a reading's share, a day's range).</summary>
    Bar = 4,

    /// <summary>A small note: the rain of a day, the account of a message, the place of a meeting.</summary>
    Note = 5,
}

/// <summary>
/// [S-16947] What leads an item: a glyph in its role (a device's kind, a day's sky), a drive's own picture (<see cref="Drive"/>),
/// a person's monogram (<see cref="Monogram"/>), or a picture only its widget draws (<see cref="Key"/>: an app's icon, a
/// contact's photo), the glyph standing until it is in.
/// </summary>
/// <param name="Glyph">A Segoe Fluent code point.</param>
/// <param name="Role">The glyph's colour.</param>
/// <param name="Drive">The drive whose picture this is; null for any other lead.</param>
/// <param name="Monogram">A person's initials, drawn in a ring in place of the glyph; null for none.</param>
/// <param name="Key">What the widget's own lead draws (an app id, a contact id); null for none.</param>
public sealed record ItemLead(string Glyph, ColourRole Role = ColourRole.TextSecondary, DriveEntry? Drive = null, string? Monogram = null, string? Key = null);

/// <summary>[S-16948] What the end slot holds.</summary>
public enum ItemEndKind
{
    /// <summary>A reading: its figure and unit as the Readout draws them ("55" "%").</summary>
    Reading,

    /// <summary>A short text: a time, a temperature, a count.</summary>
    Text,

    /// <summary>A button: the item's one action ("Join").</summary>
    Action,
}

/// <summary>
/// [S-16948] The end slot (owner, 2026-11-23: "the meetings should get a join button on the right side for example instead of the
/// percent thingy"): a reading, a text in its role, or an action button with its glyph.
/// </summary>
/// <param name="Value">The figure, the text or the button's words.</param>
/// <param name="Unit">A reading's unit; empty otherwise.</param>
/// <param name="Role">A text's colour.</param>
/// <param name="Second">A second value after the first in the secondary role (a day's low after its high); empty for none.</param>
/// <param name="Glyph">An action's glyph; null for none.</param>
public sealed record ItemEnd(ItemEndKind Kind, string Value, string Unit = "", ColourRole Role = ColourRole.TextSecondary, string Second = "", string? Glyph = null)
{
    /// <summary>A reading split into its figure and unit.</summary>
    public static ItemEnd Reading(string value, string unit) => new(ItemEndKind.Reading, value, unit, ColourRole.TextPrimary);

    /// <summary>[S-16631] A 0..1 share as a whole percentage ("55" "%", Turkish "%55" all figure).</summary>
    public static ItemEnd Percent(Locale locale, double fraction)
    {
        var split = ReadoutRules.Split(Formatting.Percent(locale, fraction));
        return Reading(split.Value, split.Unit);
    }

    /// <summary>A text in <paramref name="role"/>.</summary>
    public static ItemEnd Text(string text, ColourRole role = ColourRole.TextSecondary, string second = "") => new(ItemEndKind.Text, text, Role: role, Second: second);

    /// <summary>A button of <paramref name="label"/>.</summary>
    public static ItemEnd Action(string label, string? glyph = null) => new(ItemEndKind.Action, label, Glyph: glyph);
}

/// <summary>
/// [S-16949] The bar under an item: the share from <see cref="From"/> to <see cref="To"/> of its track (0..1) in its role; an item
/// that has a bar but no reading now keeps its track (both 0).
/// </summary>
public sealed record ItemBar(double To, double From = 0, ColourRole Role = ColourRole.Accent)
{
    /// <summary>The empty track: a bar with nothing read (WR-10: the default values, never no bar).</summary>
    public static ItemBar Track { get; } = new(0);
}

/// <summary>
/// [S-16940] One item as the item base draws it (owner, 2026-11-23: "notifications and upcoming meetings should get the same
/// base widget as the bluetooth and drive devices for example. they have to share the base but they dont neccesarily need
/// to have an icon as well. it is just so that they look as similar as possible"): its lead, its title with its subtitle
/// under it, its end slot (a value, an action, a time), its bar under all and a small note. A slot it has none of is no
/// part. Pure: a widget maps its own data onto it.
/// </summary>
/// <param name="Id">Stable while the item is there: a drive's root, a device's id, a message's key, a day.</param>
/// <param name="Title">Its name; empty for none.</param>
/// <param name="Lead">What leads it; null for nothing.</param>
/// <param name="Subtitle">The line under the name; empty for none.</param>
/// <param name="End">The end slot; null for none.</param>
/// <param name="Bar">The bar under it; null for none.</param>
/// <param name="Note">A small note; empty for none.</param>
/// <param name="Tooltip">Everything it says, in words.</param>
/// <param name="Strong">The title in the strong weight (today, unread).</param>
/// <param name="Muted">The title in the secondary role (read mail, a device that is not there).</param>
public sealed record Item(
    string Id,
    string Title,
    ItemLead? Lead = null,
    string Subtitle = "",
    ItemEnd? End = null,
    ItemBar? Bar = null,
    string Note = "",
    string Tooltip = "",
    bool Strong = false,
    bool Muted = false)
{
    /// <summary>[S-16946] Whether the item has <paramref name="part"/> to draw.</summary>
    public bool Has(ItemPart part) => part switch
    {
        ItemPart.Lead => Lead is not null,
        ItemPart.Title => Title.Length > 0,
        ItemPart.Subtitle => Subtitle.Length > 0,
        ItemPart.End => End is not null,
        ItemPart.Bar => Bar is not null,
        _ => Note.Length > 0,
    };

    /// <summary>[S-16631] Whether it has no reading now (no percentage and no bar drawn, its line saying why).</summary>
    public bool Absent => End is null;
}

/// <summary>
/// [S-16946] [S-16950] An item config's parts (owner, 2026-11-23: "settings of elements that are shown should be all grouped
/// together under a section"; "Table columns: You pick and order"): which are left out and the order they stand in as
/// the table's columns.
/// </summary>
public interface IItemParts
{
    /// <summary>The parts left out; none by default.</summary>
    List<ItemPart> HiddenParts { get; set; }

    /// <summary>The parts' order, a table's columns; empty for the kind's own order.</summary>
    List<ItemPart> PartOrder { get; set; }
}

/// <summary>
/// [S-16951] An item list's shared shape: its style, the grid blocks between two items, its rows and a table's column titles.
/// </summary>
public interface IItemList : IItemParts, IKitRows
{
    /// <summary>The arrangement it draws now.</summary>
    ItemStyle Arrangement { get; }

    /// <summary>[S-16673] The grid blocks between two items, 0..<see cref="ItemLayout.MostGap"/>: each then a card of its own.</summary>
    int Gap { get; set; }

    /// <summary>[S-16952] A table's column titles over its rows.</summary>
    bool ColumnTitles { get; set; }
}

/// <summary>
/// [S-16636] Items the user picks one by one (drives, devices): the ones hidden and the user's order, by id.
/// </summary>
public interface IItemChoice
{
    /// <summary>[S-16651] The items hidden by their ids: the checklist's off ones.</summary>
    List<string> Hidden { get; set; }

    /// <summary>[S-16652] The user's order by ids: these first, as listed, every other after them in its kind's order.</summary>
    List<string> Order { get; set; }
}

/// <summary>
/// [S-16953] A kind's own measures of its items where the base's do not fit its data (the forecast's 60 columns and
/// 80 × 100 cells): a row list's item width and a grid's cell; null takes the base's from the parts drawn.
/// </summary>
public sealed record ItemCells(double? AcrossWidth = null, double? CellWidth = null, double? CellHeight = null)
{
    /// <summary>The base's own.</summary>
    public static ItemCells Base { get; } = new();
}
