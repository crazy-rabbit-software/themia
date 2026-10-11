using Themia.Core.Office;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The contacts widget's disclosure ladder and key map ([S-1270]..[S-1277]), and vCard 2.1
/// quoted-printable values ([S-1278]), at the size tiers' slots.
/// </summary>
public sealed class ContactsFitTests
{
    private static readonly Contact Full = new()
    {
        Id = "1",
        Name = "Amara Okafor",
        Email = "amara@contoso.com",
        Phone = "+41 44 555 01 12",
        JobTitle = "Product Manager",
        Company = "Contoso",
    };

    // ---- [S-1270] lines and columns ----------------------------------------------------------------

    [Theory]
    [InlineData(120, 76, ContactDetail.Name)]          // XS
    [InlineData(200, 106, ContactDetail.Role)]         // S
    [InlineData(320, 188, ContactDetail.RoleAndReach)] // M: three rows of 60 still fit
    [InlineData(320, 150, ContactDetail.Role)]         // a short M: two would not
    [InlineData(480, 116, ContactDetail.Role)]         // Wide
    [InlineData(200, 356, ContactDetail.Everything)]   // Tall
    [InlineData(560, 376, ContactDetail.Everything)]   // L
    [InlineData(800, 516, ContactDetail.Everything)]   // XL
    public void S1270_RowsSayMoreAsTheTileGrows(double width, double height, ContactDetail expected)
        => Assert.Equal(expected, ContactRules.ListDetail(new WidgetArea(width, height)));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(200, 1)]
    [InlineData(480, 2)]
    [InlineData(560, 2)]
    [InlineData(800, 3)]
    [InlineData(5000, 4)]
    public void S1270_RowsFlowIntoColumnsOf240(double width, int columns)
        => Assert.Equal(columns, ContactRules.ListColumns(width));

    [Fact]
    public void S1270_LinesAreOnlyWhatThePersonHas()
    {
        Assert.Empty(ContactRules.Lines(Full, ContactDetail.Name));
        Assert.Equal([(ContactLine.Role, "Product Manager · Contoso")], ContactRules.Lines(Full, ContactDetail.Role));
        Assert.Equal(
            [(ContactLine.Role, "Product Manager · Contoso"), (ContactLine.Email, "amara@contoso.com")],
            ContactRules.Lines(Full, ContactDetail.RoleAndReach));
        Assert.Equal(3, ContactRules.Lines(Full, ContactDetail.Everything).Count);

        // No role: the short row's one line is the way to reach them; a phone-only person gets the phone.
        var phoneOnly = new Contact { Id = "2", Name = "Noah", Phone = "+41 79 555 90 12" };
        Assert.Equal([(ContactLine.Phone, "+41 79 555 90 12")], ContactRules.Lines(phoneOnly, ContactDetail.Role));
        Assert.Equal([(ContactLine.Phone, "+41 79 555 90 12")], ContactRules.Lines(phoneOnly, ContactDetail.Everything));
        Assert.Equal(3, ContactRules.MaxLines(ContactDetail.Everything));
    }

    [Fact]
    public void S1270_S1271_AvatarAndCompactAsideNeedTheirWidth()
    {
        Assert.False(ContactRules.ShowsAvatar(120));
        Assert.True(ContactRules.ShowsAvatar(200));
        Assert.True(ContactRules.ShowsAvatar(0));
        Assert.False(ContactRules.CompactAside(200));
        Assert.True(ContactRules.CompactAside(266));
    }

    // ---- [S-1273] / [S-1274] / [S-1275] -----------------------------------------------------------

    [Fact]
    public void S1273_CardsGrowOnLargeTiles()
    {
        Assert.False(ContactRules.LargeCards(new WidgetArea(320, 196)));
        Assert.True(ContactRules.LargeCards(new WidgetArea(560, 376)));
        Assert.Equal(5, ContactRules.CardsPerRow(560, large: false));
        Assert.Equal(3, ContactRules.CardsPerRow(560, large: true));
        Assert.Equal(2, ContactRules.CardsPerRow(120, large: false));
    }

    [Theory]
    [InlineData(76, ContactSearch.Hidden)]
    [InlineData(116, ContactSearch.Hidden)]
    [InlineData(196, ContactSearch.OnDemand)]
    [InlineData(220, ContactSearch.Shown)]
    [InlineData(0, ContactSearch.Shown)]
    public void S1275_SearchIsThereWhenItCostsLittle(double height, ContactSearch expected)
        => Assert.Equal(expected, ContactRules.Search(height));

    // ---- [S-1277] keys -----------------------------------------------------------------------------

    [Fact]
    public void S1277_SelectionMovesThroughThePeopleOnScreen()
    {
        string[] ids = ["a", "b", "c"];

        Assert.Equal("a", ContactRules.MoveSelection(ids, null, WidgetKey.Down));
        Assert.Equal("c", ContactRules.MoveSelection(ids, null, WidgetKey.Up));
        Assert.Equal("c", ContactRules.MoveSelection(ids, "b", WidgetKey.Down));
        Assert.Equal("c", ContactRules.MoveSelection(ids, "c", WidgetKey.Down));
        Assert.Equal("a", ContactRules.MoveSelection(ids, "a", WidgetKey.Up));
        Assert.Equal("a", ContactRules.MoveSelection(ids, "c", WidgetKey.Home));
        Assert.Equal("c", ContactRules.MoveSelection(ids, "a", WidgetKey.End));
        Assert.Null(ContactRules.MoveSelection(ids, "b", WidgetKey.Escape));
        Assert.Equal("b", ContactRules.MoveSelection(ids, "b", WidgetKey.Left));
        Assert.Equal("a", ContactRules.MoveSelection(ids, "gone", WidgetKey.Down));
        Assert.Equal("x", ContactRules.MoveSelection([], "x", WidgetKey.Down));
    }

    [Fact]
    public void S1277_EnterWritesElseCallsElseNothing()
    {
        Assert.Equal((ContactLinkKind.Email, "amara@contoso.com"), ContactRules.EnterLink(Full));
        Assert.Equal((ContactLinkKind.Call, "+41 79"), ContactRules.EnterLink(new Contact { Id = "x", Name = "N", Email = "not an address", Phone = "+41 79" }));
        Assert.Null(ContactRules.EnterLink(new Contact { Id = "y", Name = "N" }));
    }

    // ---- [S-1278] vCard 2.1 quoted-printable ------------------------------------------------------

    [Fact]
    public void S1278_QuotedPrintableValuesAreDecodedAcrossSoftBreaks()
    {
        const string card = "BEGIN:VCARD\r\nVERSION:2.1\r\n"
            + "N;CHARSET=UTF-8;ENCODING=QUOTED-PRINTABLE:M=C3=BCller;J=C3=BCrg=\r\nen;;;\r\n"
            + "FN;CHARSET=UTF-8;QUOTED-PRINTABLE:J=C3=BCrgen M=C3=BC=\r\nller\r\n"
            + "ORG;ENCODING=QUOTED-PRINTABLE;CHARSET=ISO-8859-1:B=E4ckerei Z=FCrich\r\n"
            + "TEL;CELL;PREF:+41 79 555 00 00\r\nTEL;HOME:+41 44 555 00 00\r\n"
            + "END:VCARD\r\n";

        var contact = Assert.Single(VCards.Read(card)).Contact;
        Assert.Equal("Jürgen Müller", contact.Name);
        Assert.Equal("Bäckerei Zürich", contact.Company);
        Assert.Equal("+41 79 555 00 00", contact.Phone);
    }

    [Theory]
    [InlineData("Caf=C3=A9", null, "Café")]
    [InlineData("a=3Db", "UTF-8", "a=b")]
    [InlineData("trailing=", null, "trailing=")]
    [InlineData("=ZZ stays", null, "=ZZ stays")]
    [InlineData("Gr=FC=DFe", "ISO-8859-1", "Grüße")]
    [InlineData("", null, "")]
    public void S1278_DecodingIsLenient(string value, string? charset, string expected)
        => Assert.Equal(expected, Ical.DecodeQuotedPrintable(value, charset));
}
