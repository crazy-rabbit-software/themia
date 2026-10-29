using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Core.Office;

/// <summary>[S-580] The contacts widget's list logic: merge, search, cap, avatar and secondary lines.</summary>
public static class ContactRules
{
    /// <summary>
    /// [S-583] How many contacts to ask each account for: at least 500, so a search spans a whole
    /// address book even on the small default list, growing with the configured count, and never
    /// past the 1000 a Graph page or a People page will hold.
    /// </summary>
    public static int FetchSize(int count) => Math.Clamp(count, 500, 1000);

    /// <summary>
    /// [S-580] Every account's contacts as one list, by name (case-insensitive, then exact so the
    /// order is total), with the same person listed once when two accounts both hold them.
    /// </summary>
    public static IReadOnlyList<Contact> Merge(IEnumerable<IEnumerable<Contact>> lists)
    {
        var seen = new HashSet<(string, string)>();
        var merged = new List<Contact>();

        foreach (var contact in lists.SelectMany(l => l))
        {
            if (string.IsNullOrWhiteSpace(contact.Name))
            {
                continue;
            }

            if (seen.Add((contact.Name.Trim().ToUpperInvariant(), (contact.Email ?? contact.Phone ?? string.Empty).Trim().ToUpperInvariant())))
            {
                merged.Add(contact);
            }
        }

        return merged
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>[S-585] Case-insensitive substring match over name, email, company and job title.</summary>
    public static IReadOnlyList<Contact> Filter(IReadOnlyList<Contact> contacts, string? query)
    {
        var q = query?.Trim();
        if (string.IsNullOrEmpty(q))
        {
            return contacts;
        }

        return contacts.Where(c =>
            Has(c.Name, q) || Has(c.Email, q) || Has(c.Company, q) || Has(c.JobTitle, q)).ToList();

        static bool Has(string? field, string q) => field?.Contains(q, StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>[S-583] Filter first, so a search spans everything fetched, then cap.</summary>
    public static IReadOnlyList<Contact> Visible(IReadOnlyList<Contact> contacts, string? query, int count)
    {
        var filtered = Filter(contacts, query);
        var cap = Math.Max(0, count);
        return filtered.Count > cap ? filtered.Take(cap).ToList() : filtered;
    }

    /// <summary>"Engineer · Contoso", "Engineer", "Contoso" or empty.</summary>
    public static string Role(Contact contact)
    {
        var parts = new[] { contact.JobTitle, contact.Company }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim());
        return string.Join(" · ", parts);
    }

    /// <summary>[S-581] The one secondary field of a compact row: email, else company, else phone.</summary>
    public static string CompactDetail(Contact contact)
        => FirstOf(contact.Email, contact.Company, contact.Phone);

    /// <summary>One cell of the details table.</summary>
    public static string Cell(Contact contact, ContactColumn column) => column switch
    {
        ContactColumn.Name => contact.Name,
        ContactColumn.Email => contact.Email ?? string.Empty,
        ContactColumn.Phone => contact.Phone ?? string.Empty,
        ContactColumn.Company => contact.Company ?? string.Empty,
        ContactColumn.Title => contact.JobTitle ?? string.Empty,
        _ => string.Empty,
    };

    /// <summary>A column's heading in <paramref name="locale"/>.</summary>
    public static string ColumnTitle(ContactColumn column, Locale locale) => column switch
    {
        ContactColumn.Name => Strings.Get(locale, Str.Contacts_Col_Name),
        ContactColumn.Email => Strings.Get(locale, Str.Contacts_Col_Email),
        ContactColumn.Phone => Strings.Get(locale, Str.Contacts_Col_Phone),
        ContactColumn.Company => Strings.Get(locale, Str.Contacts_Col_Company),
        ContactColumn.Title => Strings.Get(locale, Str.Contacts_Col_Title),
        _ => string.Empty,
    };

    /// <summary>
    /// [S-582] The columns after toggling one: canonical order, and the last one cannot be turned
    /// off (the table would have nothing in it).
    /// </summary>
    public static List<ContactColumn> ToggleColumn(IReadOnlyList<ContactColumn> current, ContactColumn column)
    {
        var next = current.Contains(column) ? current.Where(c => c != column).ToList() : [.. current, column];
        return next.Count == 0
            ? [.. current]
            : ContactColumns.All.Where(next.Contains).ToList();
    }

    /// <summary>
    /// [S-581] What a layout becomes at a size: a details table too narrow for its columns falls back
    /// to the list, and a grid too narrow for two cards to compact rows.
    /// </summary>
    public static ContactsLayout Effective(ContactsLayout wanted, double width, int columns)
        => wanted switch
        {
            ContactsLayout.Details when width > 0 && width < Math.Max(2, columns) * 88 => ContactsLayout.List,
            ContactsLayout.Grid when width > 0 && width < 2 * 96 => ContactsLayout.Compact,
            _ => wanted,
        };

    private static string FirstOf(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    // ---- the disclosure ladder ([S-1270]..[S-1275]) -----------------------------------------------

    /// <summary>[S-1270] One list column's width: an avatar, a name and an address without trimming.</summary>
    public const double ListColumnWidth = 240;

    /// <summary>[S-1270] Below this column width a row gives the avatar's room to the name.</summary>
    public const double AvatarFrom = 180;

    /// <summary>[S-1275] From this height the search box is always there.</summary>
    public const double SearchFrom = 220;

    /// <summary>[S-1275] Below this height there is no search at all: two names are the whole tile.</summary>
    public const double SearchOnDemandFrom = 128;

    /// <summary>[S-1270] How many columns the list and compact layouts flow their rows into: one per 240 px, up to four.</summary>
    public static int ListColumns(double width)
        => width > 0 ? Math.Clamp((int)Math.Floor(width / ListColumnWidth), 1, 4) : 1;

    /// <summary>[S-1271] A compact row keeps its right-aligned field only in a column this wide; else the name has the row.</summary>
    public static bool CompactAside(double columnWidth) => !(columnWidth > 0) || columnWidth >= ListColumnWidth;

    /// <summary>[S-1270] Whether a row of this column width carries the avatar.</summary>
    public static bool ShowsAvatar(double columnWidth) => !(columnWidth > 0) || columnWidth >= AvatarFrom;

    /// <summary>[S-1275] Where the search box is at a height: always, behind a header button, or not at all.</summary>
    public static ContactSearch Search(double height)
        => !(height > 0) || height >= SearchFrom ? ContactSearch.Shown
            : height >= SearchOnDemandFrom ? ContactSearch.OnDemand
            : ContactSearch.Hidden;

    // ---- keys ([S-1277]) -------------------------------------------------------------------------

    /// <summary>
    /// [S-1277] The selection after a key, over the people on screen in order (the classic S-31):
    /// Up / Down move by one (from nothing, Down picks the first and Up the last), Home / End jump,
    /// Escape clears. Other keys leave it; a selection no longer on screen starts over.
    /// [S-1983] Page Up / Page Down jump <see cref="ListKeys.PageStep"/> people (from nothing, the first).
    /// </summary>
    public static string? MoveSelection(IReadOnlyList<string> ids, string? current, WidgetKey key)
    {
        if (key == WidgetKey.Escape)
        {
            return null;
        }

        if (ids.Count == 0)
        {
            return current;
        }

        var index = current is null ? -1 : ids.ToList().IndexOf(current);

        return key switch
        {
            WidgetKey.Down => ids[index < 0 ? 0 : Math.Min(ids.Count - 1, index + 1)],
            WidgetKey.Up => ids[index < 0 ? ids.Count - 1 : Math.Max(0, index - 1)],
            WidgetKey.PageDown => ids[index < 0 ? 0 : Math.Min(ids.Count - 1, index + ListKeys.PageStep)],
            WidgetKey.PageUp => ids[index < 0 ? 0 : Math.Max(0, index - ListKeys.PageStep)],
            WidgetKey.Home => ids[0],
            WidgetKey.End => ids[^1],
            _ => current,
        };
    }

    /// <summary>
    /// [S-1277] What Enter does to a selected person (the classic S-32): write to them when they have
    /// a plain address, else call them, else nothing.
    /// </summary>
    public static (ContactLinkKind Kind, string Value)? EnterLink(Contact contact)
    {
        if (ContactLinks.Build(ContactLinkKind.Email, contact.Email) is not null)
        {
            return (ContactLinkKind.Email, contact.Email!);
        }

        return ContactLinks.Build(ContactLinkKind.Call, contact.Phone) is not null ? (ContactLinkKind.Call, contact.Phone!) : null;
    }
}

/// <summary>[S-1275] Where the contacts search box is.</summary>
public enum ContactSearch
{
    Hidden,
    OnDemand,
    Shown,
}

/// <summary>
/// [S-587] <c>mailto:</c>, <c>tel:</c> and <c>sms:</c> links built from a contact's own fields — the
/// only non-web schemes this app hands to the shell, and only through here.
/// <para>
/// The address comes from a provider, so it is untrusted: an email must be one plain
/// <c>local@domain</c> with nothing a URI or a shell could read as syntax, and a number only digits
/// and the usual separators. Anything else is refused (null), never cleaned up into something the
/// contact did not say.
/// </para>
/// </summary>
public static class ContactLinks
{
    public static string? Build(ContactLinkKind kind, string? value)
    {
        var s = value?.Trim();
        if (string.IsNullOrEmpty(s) || s.Length > 254)
        {
            return null;
        }

        return kind switch
        {
            ContactLinkKind.Email => IsPlainEmail(s) ? "mailto:" + s : null,
            ContactLinkKind.Call => Number(s) is { } n ? "tel:" + n : null,
            ContactLinkKind.Message => Number(s) is { } n ? "sms:" + n : null,
            _ => null,
        };
    }

    private static bool IsPlainEmail(string s)
    {
        var at = s.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || at != s.LastIndexOf('@') || at == s.Length - 1 || !s[(at + 1)..].Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var c in s)
        {
            var ok = char.IsAsciiLetterOrDigit(c) || c is '@' or '.' or '-' or '_' or '+' or '\'';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The number with its visual separators dropped, or null when it is not a number.</summary>
    private static string? Number(string s)
    {
        var digits = 0;
        var result = new System.Text.StringBuilder(s.Length);

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsAsciiDigit(c))
            {
                digits++;
                result.Append(c);
            }
            else if (c == '+' && result.Length == 0)
            {
                result.Append(c);
            }
            else if (c is not (' ' or '-' or '.' or '(' or ')' or '/'))
            {
                return null;
            }
        }

        return digits is >= 3 and <= 20 ? result.ToString() : null;
    }
}
