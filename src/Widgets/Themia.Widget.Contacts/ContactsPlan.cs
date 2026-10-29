using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Contacts;

/// <summary>[S-1270]..[S-1275] What one size of the contacts widget draws, chosen by <see cref="ContactsPlan.Of"/>.</summary>
/// <param name="Layout">The layout drawn (the wanted one, or its fallback on a tile too narrow for it, [S-581]).</param>
/// <param name="Columns">List and compact rows: columns of rows, read left to right; the grid: cells per row.</param>
/// <param name="ColumnWidth">A column's width (the grid: a cell's).</param>
/// <param name="Lines">[S-6712] The lines every row shows (1 to 3).</param>
/// <param name="Avatar">The list's rows lead with the person's 32 monogram.</param>
/// <param name="Aside">Compact rows carry the one field as their value.</param>
/// <param name="Icon">The grid: a cell's monogram (32, 48 on an expanded width).</param>
/// <param name="Pitch">A row's (a grid line's) height.</param>
/// <param name="Rows">How many whole rows (grid lines) fit; the rest is a scroll away.</param>
public readonly record struct ContactsLayoutPlan(ContactsLayout Layout, int Columns, double ColumnWidth, int Lines, bool Avatar, bool Aside, double Icon, double Pitch, int Rows);

/// <summary>
/// [S-1270]..[S-1275] The contacts widget's ladder on the family's parts ([S-6711]..[S-6713]): list
/// rows of one, two or three lines by the tier (the name; then the role, else a way to reach the
/// person; then the address, else the number), leading with a 32 monogram from a column of 180;
/// compact rows of one line with the one field as their value from a column of 240; the details
/// table of one line under its column titles; the grid's cells (<c>Row.Cell</c>: the monogram over the
/// name). Columns of 240, at most four. Whole rows only, in the view's area less the card's bottom
/// inset and the search box.
/// </summary>
public static class ContactsPlan
{
    /// <summary>[S-1275] The search box: one control of 32 and the gap under it.</summary>
    public const double SearchHeight = FamilyShape.Button + FamilySpace.S2;

    /// <summary>[S-582] The details table's column titles: one Caption line and the gap under it.</summary>
    public static double HeaderHeight => FamilyType.Caption.LineHeight + FamilySpace.S2;

    public static ContactsLayoutPlan Of(WidgetArea area, ContactsLayout wanted, int detailColumns, bool search)
    {
        var layout = ContactRules.Effective(wanted, area.Width, detailColumns);
        var list = Math.Max(0, area.Height - FamilySpace.Inset - (search ? SearchHeight : 0));
        var columns = ContactRules.ListColumns(area.Width);
        var column = area.Width > 0 ? area.Width / columns : 0;

        switch (layout)
        {
            case ContactsLayout.Grid:
            {
                var icon = RowRules.CellIcon(area);
                var cell = icon + (2 * FamilySpace.S6);
                var height = RowRules.CellHeight(icon, named: true);
                var inner = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
                var perRow = Math.Max(1, (int)Math.Floor((inner + FamilySpace.ColumnGap) / (cell + FamilySpace.ColumnGap)));
                var rows = (int)Math.Floor((list + FamilySpace.ColumnGap + 0.01) / (height + FamilySpace.ColumnGap));
                return new ContactsLayoutPlan(layout, perRow, cell, 1, false, false, icon, height + FamilySpace.ColumnGap, rows);
            }

            case ContactsLayout.Details:
                return new ContactsLayoutPlan(layout, 1, area.Width, 1, false, false, 0, FamilyShape.Row1, RowRules.Fit(list - HeaderHeight, 1));

            case ContactsLayout.Compact:
                return new ContactsLayoutPlan(layout, columns, column, 1, false, ContactRules.CompactAside(column), 0, FamilyShape.Row1, RowRules.Fit(list, 1));

            default:
            {
                var lines = area.IsMeasured ? RowRules.Lines(area, 3) : 1;
                return new ContactsLayoutPlan(layout, columns, column, lines, ContactRules.ShowsAvatar(column), false, 0, FamilyShape.RowHeight(lines), RowRules.Fit(list, lines));
            }
        }
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
