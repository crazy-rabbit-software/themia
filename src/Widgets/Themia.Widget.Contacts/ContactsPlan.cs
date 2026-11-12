using Themia.Sdk;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Contacts;

/// <summary>[S-11406] What the contacts widget draws, from its config alone (<see cref="ContactsPlan.Of"/>).</summary>
/// <param name="Layout">The layout drawn: the chosen one, at every size.</param>
/// <param name="Columns">List and compact rows: one column; the grid: cells per line.</param>
/// <param name="ColumnWidth">A column's width (the grid: a cell's).</param>
/// <param name="Lines">[S-6712] The lines every row shows (1 to 3).</param>
/// <param name="Avatar">The list's rows lead with the person's 32 monogram.</param>
/// <param name="Aside">Compact rows carry the one field as their value.</param>
/// <param name="Icon">The grid: a cell's monogram (48).</param>
/// <param name="Pitch">A row's (a grid line's) height.</param>
/// <param name="Rows">How many rows (grid lines) show; the rest is a scroll away.</param>
public readonly record struct ContactsLayoutPlan(ContactsLayout Layout, int Columns, double ColumnWidth, int Lines, bool Avatar, bool Aside, double Icon, double Pitch, int Rows);

/// <summary>
/// [S-11406] The contacts widget's plan and sizes on the family's parts ([S-6711]..[S-6713]), each
/// layout its own: list rows of the chosen lines (the name; then the role, else a way to reach the
/// person; then the address, else the number) led by a 32 monogram; compact rows of one line with the
/// one field as their value; the details table of one line under its column titles; the grid's cells
/// (<c>Row.Cell</c>: a 48 monogram over the name), the chosen cells a line and lines. The search box
/// over them where the config has it. No size decides any of it.
/// </summary>
public static class ContactsPlan
{
    /// <summary>[S-1275] The search box: one control of 28 and the gap under it.</summary>
    public const double SearchHeight = FamilyShape.Button + FamilySpace.S2;

    /// <summary>[S-582] The details table's column titles: one Caption line and the gap under it.</summary>
    public static double HeaderHeight => FamilyType.Caption.LineHeight + FamilySpace.S2;

    /// <summary>[S-11406] The grid's monogram: one size, the cells' line and the card's width follow it.</summary>
    public const double CellIcon = FamilyShape.IconHuge;

    /// <summary>[S-11406] A grid cell's width: its monogram and 24 either side for the name.</summary>
    public const double CellWidth = CellIcon + (2 * FamilySpace.S6);

    /// <summary>[S-11406] A grid line's height: the monogram, 4, the name's Caption line.</summary>
    public static double CellHeight => RowRules.CellHeight(CellIcon, named: true);

    /// <summary>[S-582] The narrowest a details column says anything in.</summary>
    public const double DetailColumn = 88;

    public static ContactsLayoutPlan Of(WidgetArea area, ContactsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var width = Math.Max(0, area.Width);
        var rows = KitSizing.ClampRows(config.Rows);

        return config.EffectiveLayout switch
        {
            ContactsLayout.Grid => new ContactsLayoutPlan(ContactsLayout.Grid, Math.Clamp(config.GridColumns, 2, ContactsConfig.MaxGrid), CellWidth, 1, false, false, CellIcon, CellHeight + FamilySpace.ColumnGap, Math.Clamp(config.GridRows, 1, ContactsConfig.MaxGrid)),
            ContactsLayout.Details => new ContactsLayoutPlan(ContactsLayout.Details, 1, width, 1, false, false, 0, FamilyShape.Row1, rows),
            ContactsLayout.Compact => new ContactsLayoutPlan(ContactsLayout.Compact, 1, width, 1, false, true, 0, FamilyShape.Row1, rows),
            _ => new ContactsLayoutPlan(ContactsLayout.List, 1, width, KitSizing.ClampLines(config.Lines, KitSizing.MaxLines), true, false, 0, FamilyShape.RowHeight(KitSizing.ClampLines(config.Lines, KitSizing.MaxLines)), rows),
        };
    }

    /// <summary>
    /// [S-11406] Each layout's own sizes, with the search box where it is on: List and Compact the
    /// kit's widths (220..520, a new one 300), their height the rows; Details as wide as its columns
    /// need (88 a column) to 720, its height the column titles and the rows; the Grid one size, its
    /// cells and lines (no handle).
    /// </summary>
    public static WidgetSizing Sizing(ContactsConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var top = FamilySpace.ContentTop + (config.SearchBox ? SearchHeight : 0);

        if (config.EffectiveLayout == ContactsLayout.Grid)
        {
            var columns = Math.Clamp(config.GridColumns, 2, ContactsConfig.MaxGrid);
            var lines = Math.Clamp(config.GridRows, 1, ContactsConfig.MaxGrid);
            return WidgetSizing.Fixed(new(
                (2 * FamilySpace.Inset) + (columns * CellWidth) + ((columns - 1) * FamilySpace.ColumnGap),
                top + (lines * CellHeight) + ((lines - 1) * FamilySpace.ColumnGap) + FamilySpace.Inset));
        }

        var widths = config.EffectiveLayout == ContactsLayout.Details ? DetailWidths(config.EffectiveColumns.Count) : new KitWidths(KitSizing.MinWidth, KitSizing.NaturalWidth, KitSizing.MaxWidth);
        return WidgetSizing.Range(new(widths.Min, 1), new(widths.Natural, 1), new(widths.Max, 1)).WithHeightSteps(Steps(config, top));
    }

    /// <summary>[S-11406] The rows a height means (List, Compact, Details): a copy when they change; the Grid has one size.</summary>
    public static WidgetConfig SizeSets(WidgetConfig config, SizeDip size)
    {
        if (config is not ContactsConfig contacts || contacts.EffectiveLayout == ContactsLayout.Grid)
        {
            return config;
        }

        var wanted = Steps(contacts, FamilySpace.ContentTop + (contacts.SearchBox ? SearchHeight : 0)).Nearest(size.Height);
        if (wanted == contacts.Rows)
        {
            return config;
        }

        var copy = (ContactsConfig)contacts.Clone();
        copy.Rows = wanted;
        return copy;
    }

    /// <summary>[S-582] A details table reads from two columns of 88 (or its own count) to 720.</summary>
    public static KitWidths DetailWidths(int columns)
    {
        var min = (2 * FamilySpace.Inset) + (Math.Max(2, columns) * DetailColumn);
        var natural = Math.Max(min, 420);
        return new KitWidths(min, natural, Math.Max(natural, 720));
    }

    private static SizeSteps Steps(ContactsConfig config, double top)
    {
        var layout = config.EffectiveLayout;
        var row = layout == ContactsLayout.List ? FamilyShape.RowHeight(KitSizing.ClampLines(config.Lines, KitSizing.MaxLines)) : FamilyShape.Row1;
        var head = layout == ContactsLayout.Details ? HeaderHeight : 0;
        return new SizeSteps(top + head + FamilySpace.Inset, row, KitSizing.MinRows, KitSizing.MaxRows, KitSizing.ClampRows(config.Rows));
    }

    /// <summary>
    /// [S-1270] A list row's lines under the name: the role, else a way to reach the person; then the
    /// address, else the number (each only once). As many as the plan's lines leave room for.
    /// </summary>
    public static (string? Second, string? Third) Lines(Contact contact, int lines)
    {
        ArgumentNullException.ThrowIfNull(contact);

        var role = ContactRules.Role(contact);
        var email = contact.Email?.Trim() is { Length: > 0 } e ? e : null;
        var phone = contact.Phone?.Trim() is { Length: > 0 } p ? p : null;
        var reach = email ?? phone;
        string?[] all = role.Length > 0 ? [role, reach] : [reach, email is not null ? phone : null];
        var shown = all.Where(s => s is not null).ToList();
        return (lines >= 2 && shown.Count > 0 ? shown[0] : null, lines >= 3 && shown.Count > 1 ? shown[1] : null);
    }
}
