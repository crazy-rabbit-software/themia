using Themia.App.Services.Accounts;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Settings;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Contacts.Tests;

/// <summary>[S-7511] The contacts widget's options on its page ([S-3719]).</summary>
public sealed class ContactsMenuTests
{
    private int _saves;

    private WidgetTextAsk? _asked;

    private static readonly IReadOnlyList<AccountInfo> Work = [new("a", HostProviders.Microsoft, "a@example.com", "Work", null, null, null)];

    private WidgetMenuLines Build(ContactsConfig config, IReadOnlyList<AccountInfo>? accounts = null)
    {
        var lines = new WidgetMenuLines(() => _saves++);
        ContactsMenu.Lines(lines, config, accounts ?? Work);
        return lines;
    }

    private static SettingsLine Line(IEnumerable<SettingsLine> lines, string text) => Assert.Single(lines, l => l.Text == text);

    [Fact]
    public void S16972_the_page_is_accounts_then_the_item_lines_the_search_box_and_people()
    {
        var lines = Build(new ContactsConfig());

        Assert.Equal(
            [
                Str.WidgetUi_AccountsLabel.Text(), string.Empty, Str.Items_Parts.Text(), string.Empty, Str.Items_Gap.Text(),
                Str.Kit_Rows.Text(), ContactsStr.Contacts_SearchBox.Text(), ContactsStr.WidgetsA_Contacts_Settings_Count_Label.Text(),
            ],
            lines.Lines.Select(l => l.IsSeparator ? string.Empty : l.Text));
        Assert.Equal(["Icon", "Name", "Role", "Email", "Phone"], lines.Lines[2].Children!.Select(c => c.Text));
    }

    [Fact]
    public void S16973_a_contacts_page_is_the_person_its_parts_and_the_accounts()
    {
        var lines = new WidgetMenuLines(() => _saves++);
        var config = new ContactConfig();
        ContactsMenu.Lines(lines, config, Work);

        Assert.Equal(
            [ContactsStr.Contacts_Person.Text(), Str.Items_Parts.Text(), string.Empty, Str.WidgetUi_AccountsLabel.Text()],
            lines.Lines.Select(l => l.IsSeparator ? string.Empty : l.Text));
    }

    [Theory]
    [InlineData("4", 5)]
    [InlineData("1001", 1000)]
    public void S7511_the_count_is_held_to_five_and_what_the_box_takes(string typed, int held)
    {
        // [S-2370] A number box: a number outside the range is held to its end, never refused.
        var config = new ContactsConfig();
        _asked = Typed.Ask(Line(Build(config).Lines, ContactsStr.WidgetsA_Contacts_Settings_Count_Label.Text()));

        Assert.NotNull(_asked!.Refuse("many"));
        _asked.Done(typed);
        Assert.Equal(held, config.Count);
    }

    [Fact]
    public void S7511_a_typed_count_is_rounded_to_a_step_of_five()
    {
        var config = new ContactsConfig();
        _asked = Typed.Ask(Line(Build(config).Lines, ContactsStr.WidgetsA_Contacts_Settings_Count_Label.Text()));

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
