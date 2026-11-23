using Themia.App.Services.Accounts;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Email.Tests;

/// <summary>[S-7512] The Mail widget's options on its page ([S-3719]): what each style offers under Style, and what a line does.</summary>
public sealed class EmailMenuTests
{
    private int _saves;

    private static readonly IReadOnlyList<AccountInfo> Mail =
    [
        new("a", HostProviders.Microsoft, "a@example.com", "Work", null, null, null),
        new("b", HostProviders.Google, "b@example.com", "Home", null, null, null),
    ];

    private WidgetMenuLines Build(EmailConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++);
        EmailMenu.Lines(lines, config, Mail);
        return lines;
    }

    private static SettingsLine Line(IEnumerable<SettingsLine> lines, string text) => Assert.Single(lines, l => l.Text == text);

    [Fact]
    public void S16971_the_page_is_the_item_lines_then_the_accounts_and_the_folder()
    {
        var lines = Build(new EmailConfig());

        Assert.Equal(
            [Str.Items_Parts.Text(), string.Empty, Str.Items_Gap.Text(), Str.Kit_Rows.Text(), string.Empty, Str.WidgetUi_AccountsLabel.Text(), EmailStr.Email_Folder_Label.Text()],
            lines.Lines.Select(l => l.IsSeparator ? string.Empty : l.Text));
        Assert.Equal(EmailFolders.Listable.Select(f => f.Label()), lines.Lines[^1].Children!.Select(c => c.Text));

        // The parts in their order with the preview's switch; a table offers its column titles in the gap's place.
        Assert.Equal(["From", "Subject", "Account", "Date", "Preview"], lines.Lines[0].Children!.Select(c => c.Text));
        Assert.Contains(Build(new EmailConfig { Style = Themia.Sdk.Items.ItemStyle.Table }).Lines, l => l.Text == Str.Items_ColumnTitles.Text());
    }

    [Fact]
    public void S7512_the_accounts_are_every_mail_account_then_each_one()
    {
        var accounts = Line(Build(new EmailConfig()).Lines, Str.WidgetUi_AccountsLabel.Text()).Children!.Where(l => !l.IsSeparator).Select(l => l.Text);

        Assert.Equal([EmailStr.WidgetsB_Settings_Email_Every_Label.Text(), "Work", "Home", Str.WidgetsA_Office_ManageAccounts.Text()], accounts);
    }
}
