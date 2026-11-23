using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;

namespace Themia.Widget.Contacts;

/// <summary>
/// [S-16972] [S-16973] The people on the item base (owner, 2026-11-23: "same should be done for email and contacts too"): a person
/// as an item, their monogram the lead ([S-586]), their name, their role under it, their address the note, their number at
/// the end; and the search band's one size. Pure.
/// </summary>
public static class ContactItems
{
    /// <summary>[S-16972] The parts a person has, in their own order.</summary>
    public static IReadOnlyList<ItemPart> Parts { get; } = [ItemPart.Lead, ItemPart.Title, ItemPart.Subtitle, ItemPart.Note, ItemPart.End];

    /// <summary>[S-584] The search field: the family's control of 28.</summary>
    public const double SearchHeight = FamilyShape.Button;

    /// <summary>[S-15775] The field's band over the people: the control in whole grid cells (30 on the default grid).</summary>
    public static double Band(ItemGrid grid) => ItemSize.Optimal(SearchHeight, grid);

    /// <summary>
    /// [S-16972] A person as an item: their monogram (the people glyph where they have no name), their name, their role ("Engineer
    /// · Contoso") under it, their address the note, their number at the end, everything they have in the tooltip.
    /// </summary>
    public static Item Item(Contact contact)
    {
        ArgumentNullException.ThrowIfNull(contact);
        var role = ContactRules.Role(contact);
        var tooltip = string.Join(Environment.NewLine, new[] { contact.Name, role, contact.Email, contact.Phone }.Where(t => !string.IsNullOrWhiteSpace(t)));
        return new Item(
            contact.Id,
            contact.Name,
            new ItemLead(Glyphs.Contact, Monogram: Formatting.Monogram(contact.Name)),
            role,
            string.IsNullOrWhiteSpace(contact.Phone) ? null : ItemEnd.Text(contact.Phone.Trim()),
            Note: contact.Email?.Trim() ?? string.Empty,
            Tooltip: tooltip);
    }

    /// <summary>[S-16973] A Contact with no one picked, or the one picked no longer there: "--" and why (WR-10).</summary>
    public static Item Nobody(string why) => new(string.Empty, "--", new ItemLead(Glyphs.Contact), why, Tooltip: why, Muted: true);

    /// <summary>[S-16972] A person's words for their parts: Name, Role, Email, Phone.</summary>
    public static string PartName(ItemPart part, Locale locale) => part switch
    {
        ItemPart.Title => Strings.Get(locale, Str.Contacts_Col_Name),
        ItemPart.Subtitle => ContactsStr.Contacts_Part_Role.Text(locale),
        ItemPart.Note => Strings.Get(locale, Str.Contacts_Col_Email),
        ItemPart.End => Strings.Get(locale, Str.Contacts_Col_Phone),
        _ => Strings.Get(locale, Str.Items_Part_Lead),
    };
}

/// <summary>[S-16973] The people the contacts views read last (none in a render): what a Contact's page offers to pick from.</summary>
internal static class ContactsSeen
{
    private static IReadOnlyList<Contact> _last = [];

    public static IReadOnlyList<Contact> Last
    {
        get => Volatile.Read(ref _last);
        set => Volatile.Write(ref _last, value ?? []);
    }
}
