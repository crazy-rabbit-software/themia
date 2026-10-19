using Themia.App.Services.Accounts;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Contacts.Tests;

/// <summary>[S-7511] The contacts widget's options on its properties menu ([S-3719]).</summary>
public sealed class ContactsMenuTests
{
    private int _saves;

    private WidgetTextAsk? _asked;

    private static readonly IReadOnlyList<AccountInfo> Work = [new("a", HostProviders.Microsoft, "a@example.com", "Work", null, null, null)];

    private WidgetMenuLines Build(ContactsConfig config, IReadOnlyList<AccountInfo>? accounts = null)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        ContactsMenu.Lines(lines, config, accounts ?? Work);
        return lines;
    }

    private static ItemMenuEntry Line(IEnumerable<ItemMenuEntry> lines, string text) => Assert.Single(lines, l => l.Text == text);

    [Fact]
    public void S7511_the_first_level_is_appearance_accounts_layout_and_people()
    {
        var lines = Build(new ContactsConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [Str.WidgetUi_AccountsLabel.Text(), ContactsStr.WidgetsA_Contacts_Settings_Layout_Label.Text(), ContactsStr.WidgetsA_Contacts_Settings_Count_Label.Text()],
            lines.Lines.Select(l => l.Text));
    }

    [Fact]
    public void S7511_the_columns_are_offered_on_the_details_layout_only()
    {
        Assert.DoesNotContain(Build(new ContactsConfig()).Lines, l => l.Text == ContactsStr.WidgetsA_Contacts_Menu_Columns.Text());
        Assert.Contains(Build(new ContactsConfig { Layout = ContactsLayout.Details }).Lines, l => l.Text == ContactsStr.WidgetsA_Contacts_Menu_Columns.Text());
    }

    [Fact]
    public void S7511_picking_a_layout_sets_it_and_the_current_one_saves_nothing()
    {
        var config = new ContactsConfig();
        var layout = Line(Build(config).Lines, ContactsStr.WidgetsA_Contacts_Settings_Layout_Label.Text());

        Line(layout.Children!, ContactsStr.Apps_ViewList.Text()).Action!();
        Assert.Equal(0, _saves);

        Line(layout.Children!, ContactsStr.Apps_ViewGrid.Text()).Action!();
        Assert.Equal(ContactsLayout.Grid, config.Layout);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S7511_a_column_turns_off_in_canonical_order_and_the_last_one_is_greyed()
    {
        var config = new ContactsConfig { Layout = ContactsLayout.Details, Columns = [ContactColumn.Name, ContactColumn.Phone] };
        var columns = Line(Build(config).Lines, ContactsStr.WidgetsA_Contacts_Menu_Columns.Text()).Children!;
        var phone = ContactRules.ColumnTitle(ContactColumn.Phone, Strings.Current);

        Assert.Equal(ContactColumns.All.Select(c => ContactRules.ColumnTitle(c, Strings.Current)), columns.Select(c => c.Text));
        Line(columns, phone).Action!();
        Assert.Equal([ContactColumn.Name], config.Columns);

        var name = Line(Line(Build(config).Lines, ContactsStr.WidgetsA_Contacts_Menu_Columns.Text()).Children!, ContactRules.ColumnTitle(ContactColumn.Name, Strings.Current));
        Assert.True(name.Disabled);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("1001")]
    [InlineData("many")]
    public void S7511_the_count_refuses_less_than_five_and_more_than_the_box_takes(string typed)
    {
        Line(Build(new ContactsConfig()).Lines, ContactsStr.WidgetsA_Contacts_Settings_Count_Label.Text()).Action!();

        Assert.NotNull(_asked!.Refuse(typed));
    }

    [Fact]
    public void S7511_a_typed_count_is_rounded_to_a_step_of_five()
    {
        var config = new ContactsConfig();
        Line(Build(config).Lines, ContactsStr.WidgetsA_Contacts_Settings_Count_Label.Text()).Action!();

        _asked!.Done("42");
        Assert.Equal(40, config.Count);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S7511_with_no_contacts_account_the_accounts_line_says_so()
    {
        var accounts = Line(Build(new ContactsConfig(), []).Lines, Str.WidgetUi_AccountsLabel.Text()).Children!;

        Assert.Equal(ContactsStr.WidgetsA_Contacts_NoAccount.Text(), accounts[0].Text);
    }
}
