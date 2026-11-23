using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Sdk;
using Themia.Sdk.Items;
using Xunit;

namespace Themia.Widget.Contacts.Tests;

/// <summary>[S-16972] [S-16973] The people on the item base: a person as an item, the list's and the Contact's sizes.</summary>
public sealed class ContactItemsTests
{
    private static readonly Contact Ana = new() { Id = "1", Name = "Ana Silva", JobTitle = "Design lead", Company = "Lumen Labs", Email = "ana@lumen.example", Phone = "+41 44 555 01 14" };

    [Fact]
    public void S16972_a_person_is_their_monogram_name_role_address_and_number()
    {
        var item = ContactItems.Item(Ana);

        Assert.Equal(("1", "Ana Silva", "AS", "Design lead · Lumen Labs", "ana@lumen.example", "+41 44 555 01 14"), (item.Id, item.Title, item.Lead!.Monogram, item.Subtitle, item.Note, item.End!.Value));
        Assert.Null(ContactItems.Item(Ana with { Phone = " " }).End);
        Assert.True(ContactItems.Nobody("why").Muted);
    }

    [Fact]
    public void S16972_the_list_is_the_item_bases_with_the_search_band_over_it()
    {
        var config = new ContactsConfig();
        Assert.Equal([ItemPart.End], config.HiddenParts);
        var sizing = new ContactsModule().Descriptor.Sizing(config);

        // The 36 monogram line in the insets, 60 a person, five, under the field's band of 30.
        Assert.Equal(30 + (5 * 60), sizing.Natural.Height);
        Assert.Equal(5 * 60, new ContactsModule().Descriptor.Sizing(new ContactsConfig { SearchBox = false }).Natural.Height);

        var wild = new ContactsConfig { Count = 2, Rows = 0, AccountIds = ["a", "a"] };
        Assert.NotEmpty(wild.Repair());
        Assert.Equal((5, 1, 1), (wild.Count, wild.Rows, wild.AccountIds.Count));
    }

    [Fact]
    public void S16973_a_contact_is_one_item_tall_and_a_copy_keeps_its_person()
    {
        var face = new ContactModule().Descriptor.Sizing(new ContactConfig());
        Assert.Equal((60.0, 60.0), (face.Min.Height, face.Natural.Height));
        var copy = Assert.IsType<ContactConfig>(new ContactConfig { Person = "7", AccountIds = ["a"] }.Clone());
        Assert.Equal(("7", "a"), (copy.Person, copy.AccountIds.Single()));
    }
}
