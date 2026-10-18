using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Office;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// Contacts and birthdays: the classic <c>parse_vcards</c>, <c>contactsView</c> and birthday tests,
/// plus the Graph and Google shapes and the contact links.
/// </summary>
public sealed class ContactsAndBirthdaysTests
{
    // ---- [S-579] vCards ----------------------------------------------------------------------------

    [Fact]
    public void S579_ExtractsFieldsAndSkipsCardsWithNoName()
    {
        const string xml = "<D:multistatus xmlns:D=\"DAV:\" xmlns:C=\"urn:ietf:params:xml:ns:carddav\">"
            + "<D:response><D:href>/ab/1.vcf</D:href><D:propstat><D:prop><C:address-data>"
            + "BEGIN:VCARD\r\nVERSION:3.0\r\nUID:c-1\r\nFN:Jane Doe\r\nEMAIL;TYPE=INTERNET:jane@example.com\r\nTEL:+1-555-0100\r\n"
            + "ORG:Acme Inc;Sales\r\nTITLE:Engineer\r\nBDAY:1985-04-15\r\nEND:VCARD"
            + "</C:address-data></D:prop><D:status>HTTP/1.1 200 OK</D:status></D:propstat></D:response>"
            + "<D:response><D:href>/ab/2.vcf</D:href><D:propstat><D:prop><C:address-data>BEGIN:VCARD\r\nVERSION:3.0\r\nEND:VCARD</C:address-data></D:prop></D:propstat></D:response>"
            + "</D:multistatus>";

        var card = Assert.Single(VCards.Read(string.Join("\n", DavOffice.AddressData(xml))));
        Assert.Equal("c-1", card.Contact.Id);
        Assert.Equal("Jane Doe", card.Contact.Name);
        Assert.Equal("jane@example.com", card.Contact.Email);
        Assert.Equal("+1-555-0100", card.Contact.Phone);
        Assert.Equal("Acme Inc", card.Contact.Company);
        Assert.Equal("Engineer", card.Contact.JobTitle);
        Assert.Equal(new Birthday("Jane Doe", 4, 15, 1985), card.Birthday);
    }

    [Fact]
    public void S579_PreferredValuesStructuredNameAndUriValues()
    {
        var card = Assert.Single(VCards.Read(
            "BEGIN:VCARD\r\nVERSION:4.0\r\nN:Doe;John;;;\r\nEMAIL:old@example.com\r\nEMAIL;PREF=1:main@example.com\r\n"
            + "TEL;VALUE=uri;TYPE=\"voice,cell\":tel:+41-44-555-0100\r\nitem1.ORG:Contoso\\, Ltd;R&D\r\nBDAY:--0229\r\nEND:VCARD"));

        Assert.Equal("John Doe", card.Contact.Name);
        Assert.Equal("main@example.com", card.Contact.Email);
        Assert.Equal("+41-44-555-0100", card.Contact.Phone);
        Assert.Equal("Contoso, Ltd", card.Contact.Company);
        Assert.Equal(new Birthday("John Doe", 2, 29, null), card.Birthday);
    }

    // ---- [S-575] birthdays -------------------------------------------------------------------------

    [Fact]
    public void S575_NextOccurrenceTodayCountsAndLeapDaysLandOnThe28th()
    {
        var today = new DateOnly(2026, 10, 4);
        Assert.Equal(today, BirthdayRules.Next(new Birthday("A", 10, 4, null), today));
        Assert.Equal(new DateOnly(2027, 10, 3), BirthdayRules.Next(new Birthday("B", 10, 3, null), today));
        Assert.Equal(new DateOnly(2027, 2, 28), BirthdayRules.Next(new Birthday("C", 2, 29, null), today));
        Assert.Equal(new DateOnly(2028, 2, 29), BirthdayRules.OccurrenceIn(new Birthday("C", 2, 29, null), 2028));
        Assert.Null(BirthdayRules.OccurrenceIn(new Birthday("D", 4, 31, null), 2026));
        Assert.True(BirthdayRules.OccursOn(new Birthday("C", 2, 29, null), new DateOnly(2026, 2, 28)));
    }

    [Fact]
    public void S575_AgeIsAtTheOccurrenceAndOutlooksNoYearIsNoAge()
    {
        var b = new Birthday("Lena Fischer", 1, 2, 1990);
        Assert.Equal(37, BirthdayRules.AgeOn(b, new DateOnly(2027, 1, 2)));
        Assert.Equal("Lena Fischer (37)", BirthdayRules.Label(b, new DateOnly(2027, 1, 2)));
        Assert.Null(BirthdayRules.AgeOn(b with { Year = 1604 }, new DateOnly(2027, 1, 2)));
        Assert.Equal("Lena Fischer", BirthdayRules.Label(b with { Year = null }, new DateOnly(2027, 1, 2)));
        Assert.Equal("Lena", BirthdayRules.FirstName("  Lena Fischer "));
    }

    [Theory]
    [InlineData("1990-05-10T00:00:00Z", 5, 10, 1990)]
    [InlineData("19900510", 5, 10, 1990)]
    [InlineData("--0510", 5, 10, null)]
    [InlineData("--05-10", 5, 10, null)]
    [InlineData("1604-05-10T00:00:00Z", 5, 10, null)]
    public void S575_ProviderBirthdayShapes(string value, int month, int day, int? year)
        => Assert.Equal(new Birthday("X", month, day, year), BirthdayRules.Parse("X", value));

    [Theory]
    [InlineData("")]
    [InlineData("1990-13-10")]
    [InlineData("1990-02-30")]
    [InlineData("garbage")]
    public void S575_BadBirthdaysAreNone(string value) => Assert.Null(BirthdayRules.Parse("X", value));

    [Fact]
    public void S575_SamePersonInTwoAccountsIsOneBirthday()
    {
        var list = BirthdayRules.Distinct([new Birthday("Mia", 3, 1, null), new Birthday("MIA", 3, 1, 1990), new Birthday("Mia", 3, 2, null)]);
        Assert.Equal(2, list.Count);
    }

    // ---- [S-580] / [S-583] / [S-585] / [S-586] contacts --------------------------------------------

    private static Contact Person(string name, string? email = null, string? company = null, string? title = null, string? phone = null)
        => new() { Id = name, Name = name, Email = email, Company = company, JobTitle = title, Phone = phone };

    [Fact]
    public void S580_MergeSortsByNameAndListsAPersonOnce()
    {
        var merged = ContactRules.Merge(
        [
            [Person("zoe", "z@x.com"), Person("Adam", "a@x.com")],
            [Person("Bea"), Person("Adam", "A@X.COM"), Person("  ")],
        ]);

        Assert.Equal(["Adam", "Bea", "zoe"], merged.Select(c => c.Name));
    }

    [Fact]
    public void S585_SearchSpansNameEmailCompanyAndTitleBeforeTheCap()
    {
        Contact[] all =
        [
            Person("Ann", "ann@contoso.com"),
            Person("Ben", company: "Fabrikam"),
            Person("Cy", title: "Contoso liaison"),
            Person("Dee"),
        ];

        Assert.Equal(["Ann", "Cy"], ContactRules.Filter(all, "CONTOSO").Select(c => c.Name));
        Assert.Equal(4, ContactRules.Filter(all, "  ").Count);
        Assert.Equal(["Ann"], ContactRules.Visible(all, "contoso", 1).Select(c => c.Name));
        Assert.Equal(2, ContactRules.Visible(all, null, 2).Count);
    }

    [Theory]
    [InlineData(5, 500)]
    [InlineData(700, 700)]
    [InlineData(5000, 1000)]
    public void S583_FetchSizeIsClamped(int count, int expected) => Assert.Equal(expected, ContactRules.FetchSize(count));

    [Fact]
    public void S586_AvatarHueIsStableAndInRange()
    {
        Assert.Equal(ContactRules.AvatarHue("Jane Doe"), ContactRules.AvatarHue("Jane Doe"));
        Assert.InRange(ContactRules.AvatarHue("Jane Doe"), 0, 359);
        Assert.Equal(0, ContactRules.AvatarHue(null));
    }

    [Fact]
    public void S581_SecondaryLinesAndLayoutFallbacks()
    {
        var c = Person("Jane", "j@x.com", "Contoso", "Engineer", "+1 555");
        Assert.Equal("Engineer · Contoso", ContactRules.Role(c));
        Assert.Equal("j@x.com", ContactRules.CompactDetail(c));
        Assert.Equal("Contoso", ContactRules.CompactDetail(c with { Email = null }));
        Assert.Equal("Engineer", ContactRules.CardDetail(c with { Company = null }));

        Assert.Equal(ContactsLayout.List, ContactRules.Effective(ContactsLayout.Details, 300, 5));
        Assert.Equal(ContactsLayout.Details, ContactRules.Effective(ContactsLayout.Details, 560, 5));
        Assert.Equal(ContactsLayout.Compact, ContactRules.Effective(ContactsLayout.Grid, 120, 5));
    }

    [Fact]
    public void S582_ColumnsStayCanonicalAndNeverEmpty()
    {
        Assert.Equal([ContactColumn.Name, ContactColumn.Phone], ContactRules.ToggleColumn([ContactColumn.Phone], ContactColumn.Name));
        Assert.Equal([ContactColumn.Phone], ContactRules.ToggleColumn([ContactColumn.Phone], ContactColumn.Phone));
    }

    // ---- [S-587] links -----------------------------------------------------------------------------

    [Theory]
    [InlineData(ContactLinkKind.Email, "jane.doe+x@example.com", "mailto:jane.doe+x@example.com")]
    [InlineData(ContactLinkKind.Call, "+41 (44) 555-01.00", "tel:+41445550100")]
    [InlineData(ContactLinkKind.Message, "079 555 01 00", "sms:0795550100")]
    public void S587_LinksAreBuiltFromPlainValues(ContactLinkKind kind, string value, string expected)
        => Assert.Equal(expected, ContactLinks.Build(kind, value));

    [Theory]
    [InlineData(ContactLinkKind.Email, "a@b")]
    [InlineData(ContactLinkKind.Email, "x@y.com?subject=hi")]
    [InlineData(ContactLinkKind.Email, "a@b@c.com")]
    [InlineData(ContactLinkKind.Email, "\"x\" <x@y.com>")]
    [InlineData(ContactLinkKind.Call, "12")]
    [InlineData(ContactLinkKind.Call, "555-0100 & calc")]
    [InlineData(ContactLinkKind.Call, "")]
    public void S587_AnythingElseIsRefused(ContactLinkKind kind, string value) => Assert.Null(ContactLinks.Build(kind, value));

    // ---- [S-565] provider shapes -------------------------------------------------------------------

    [Fact]
    public void S565_GraphEventsAllDayAndCancelled()
    {
        var timed = Deserialize("""{"id":"1","subject":"Sync","start":{"dateTime":"2026-06-25T10:00:00.0000000","timeZone":"UTC"},"end":{"dateTime":"2026-06-25T10:30:00.0000000"},"isAllDay":false,"location":{"displayName":"Room 4"},"organizer":{"emailAddress":{"name":"Alice","address":"a@x.com"}},"webLink":"https://outlook.office.com/x"}""", OfficeJsonContext.Default.GraphEvent);
        var e = OfficeWire.FromGraph(timed)!;
        Assert.Equal(new DateTimeOffset(2026, 6, 25, 10, 0, 0, TimeSpan.Zero), e.Start);
        Assert.Equal("Room 4", e.Location);
        Assert.Equal("Alice", e.Organizer);

        var allDay = OfficeWire.FromGraph(Deserialize("""{"id":"2","isAllDay":true,"start":{"dateTime":"2026-06-05T00:00:00.0000000"},"end":{"dateTime":"2026-06-06T00:00:00.0000000"}}""", OfficeJsonContext.Default.GraphEvent))!;
        Assert.True(allDay.AllDay);
        Assert.Equal(new DateOnly(2026, 6, 5), allDay.FirstDay);
        Assert.Equal(new DateOnly(2026, 6, 6), allDay.EndDay);

        Assert.Null(OfficeWire.FromGraph(Deserialize("""{"id":"3","isCancelled":true,"start":{"dateTime":"2026-06-05T00:00:00"}}""", OfficeJsonContext.Default.GraphEvent)));
    }

    [Fact]
    public void S565_GoogleEventsKeepNegativeOffsetsAndDates()
    {
        var timed = OfficeWire.FromGoogle(Deserialize("""{"id":"g","summary":"Call","start":{"dateTime":"2026-06-25T10:00:00-04:00"},"end":{"dateTime":"2026-06-25T11:00:00-04:00"},"organizer":{"email":"o@x.com"},"htmlLink":"https://calendar.google.com/x"}""", OfficeJsonContext.Default.GoogleEvent))!;
        Assert.Equal(new DateTimeOffset(2026, 6, 25, 14, 0, 0, TimeSpan.Zero), timed.Start.ToUniversalTime());
        Assert.Equal("o@x.com", timed.Organizer);

        var allDay = OfficeWire.FromGoogle(Deserialize("""{"id":"h","start":{"date":"2026-06-05"},"end":{"date":"2026-06-08"}}""", OfficeJsonContext.Default.GoogleEvent))!;
        Assert.True(allDay.AllDay);
        Assert.Equal(new DateOnly(2026, 6, 8), allDay.EndDay);

        Assert.Null(OfficeWire.FromGoogle(Deserialize("""{"id":"i","status":"cancelled","start":{"date":"2026-06-05"}}""", OfficeJsonContext.Default.GoogleEvent)));
    }

    [Fact]
    public void S567_CalendarListsFromGraphAndGoogle()
    {
        var graph = OfficeWire.FromGraph(Deserialize("""{"id":"AAMk=","name":"Calendar","isDefaultCalendar":true,"hexColor":""}""", OfficeJsonContext.Default.GraphCalendar));
        Assert.Equal(new CalendarInfo("AAMk=", "Calendar", true, null), graph);

        var google = OfficeWire.FromGoogle(Deserialize("""{"id":"me@gmail.com","summary":"Me","summaryOverride":"Personal","primary":true,"backgroundColor":"#9FE1E7"}""", OfficeJsonContext.Default.GoogleCalendar));
        Assert.Equal(new CalendarInfo("me@gmail.com", "Personal", true, "#9fe1e7"), google);
    }

    [Fact]
    public void S580_ContactsAndBirthdaysFromGraphAndGoogle()
    {
        var graph = Deserialize("""{"id":"c1","displayName":"Ann Lee","emailAddresses":[{"address":""},{"address":"ann@x.com"}],"businessPhones":[],"mobilePhone":"+1 555","companyName":"Contoso","birthday":"1990-05-10T11:59:00Z"}""", OfficeJsonContext.Default.GraphContact);
        var c = OfficeWire.FromGraph(graph)!;
        Assert.Equal("ann@x.com", c.Email);
        Assert.Equal("+1 555", c.Phone);
        Assert.Equal(new Birthday("Ann Lee", 5, 10, 1990), OfficeWire.BirthdayFromGraph(graph));

        var person = Deserialize("""{"resourceName":"people/1","names":[{"displayName":"Bo Chen"}],"organizations":[{"name":"Fabrikam","title":"CTO"}],"birthdays":[{"date":{"month":0,"day":0}},{"date":{"month":7,"day":4}}]}""", OfficeJsonContext.Default.GooglePersonRecord);
        var g = OfficeWire.FromGoogle(person)!;
        Assert.Equal("Fabrikam", g.Company);
        Assert.Equal("CTO", g.JobTitle);
        Assert.Equal(new Birthday("Bo Chen", 7, 4, null), OfficeWire.BirthdayFromGoogle(person));

        Assert.Null(OfficeWire.FromGoogle(Deserialize("""{"resourceName":"people/2","names":[]}""", OfficeJsonContext.Default.GooglePersonRecord)));
    }

    private static T Deserialize<T>(string json, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> contract)
        => JsonSerializer.Deserialize(json, contract)!;
}
