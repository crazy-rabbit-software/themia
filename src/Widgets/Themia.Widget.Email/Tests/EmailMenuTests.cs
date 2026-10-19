using Themia.App.Services.Accounts;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Email.Tests;

/// <summary>[S-7512] The Mail widget's options on its properties menu ([S-3719]): what each style offers under Style, and what a line does.</summary>
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
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        EmailMenu.Lines(lines, config, Mail);
        return lines;
    }

    private static ItemMenuEntry Line(IEnumerable<ItemMenuEntry> lines, string text) => Assert.Single(lines, l => l.Text == text);

    private static string Columns => EmailStr.WidgetsB_Email_Menu_Columns.Text();

    [Fact]
    public void S7512_the_list_style_offers_subject_account_and_preview_lines_under_style()
        => Assert.Equal(
            [EmailStr.WidgetsB_Settings_Email_ShowSubject.Text(), EmailStr.WidgetsB_Settings_Email_ShowAccount_Label.Text(), EmailStr.Email_PreviewLines_Label.Text()],
            Build(new EmailConfig { Style = EmailStyle.List }).StyleLines.Select(l => l.Text));

    [Fact]
    public void S7512_compact_offers_the_two_lines_and_the_table_its_columns()
    {
        Assert.Equal(
            [EmailStr.WidgetsB_Settings_Email_ShowSubject.Text(), EmailStr.WidgetsB_Settings_Email_ShowAccount_Label.Text()],
            Build(new EmailConfig { Style = EmailStyle.Compact }).StyleLines.Select(l => l.Text));
        Assert.Equal([Columns], Build(new EmailConfig { Style = EmailStyle.Table }).StyleLines.Select(l => l.Text));
    }

    [Fact]
    public void S7512_the_first_level_is_appearance_the_accounts_and_the_folder()
    {
        var lines = Build(new EmailConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal([Str.WidgetUi_AccountsLabel.Text(), EmailStr.Email_Folder_Label.Text()], lines.Lines.Select(l => l.Text));
        Assert.Equal(EmailFolders.Listable.Select(f => f.Label()), lines.Lines[1].Children!.Select(c => c.Text));
    }

    [Fact]
    public void S7512_a_line_turned_off_keeps_the_others_in_their_order()
    {
        var config = new EmailConfig();
        Line(Build(config).StyleLines, EmailStr.WidgetsB_Settings_Email_ShowSubject.Text()).Action!();

        Assert.Equal([MailListField.Account], config.ListFields);
        Line(Build(config).StyleLines, EmailStr.WidgetsB_Settings_Email_ShowSubject.Text()).Action!();
        Assert.Equal([MailListField.Subject, MailListField.Account], config.ListFields);
        Assert.Equal(2, _saves);
    }

    [Fact]
    public void S7512_the_columns_move_and_the_last_one_cannot_be_removed()
    {
        var config = new EmailConfig { Style = EmailStyle.Table, RowColumns = [MailColumn.From, MailColumn.Subject] };
        var subject = Line(Line(Build(config).StyleLines, Columns).Children!, MailColumn.Subject.Label());
        subject.Order!.Move(1, 0);
        Assert.Equal([MailColumn.Subject, MailColumn.From], config.RowColumns);

        Line(Line(Line(Build(config).StyleLines, Columns).Children!, MailColumn.From.Label()).Children!, Str.Common_Remove.Text()).Action!();
        Assert.Equal([MailColumn.Subject], config.RowColumns);

        var last = Line(Line(Build(config).StyleLines, Columns).Children!, MailColumn.Subject.Label());
        Assert.True(Line(last.Children!, Str.Common_Remove.Text()).Disabled);
    }

    [Fact]
    public void S7512_a_column_that_is_off_is_added_at_the_end()
    {
        var config = new EmailConfig { Style = EmailStyle.Table };
        var columns = Line(Build(config).StyleLines, Columns).Children!;

        // From, Subject and Date are on by default: Preview and Account are offered to add.
        Assert.Single(columns, c => c.Text == MailColumn.Preview.Label() && c.Children is null).Action!();

        Assert.Equal([.. EmailConfig.DefaultColumns, MailColumn.Preview], config.RowColumns);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S7512_the_accounts_are_every_mail_account_then_each_one()
    {
        var accounts = Line(Build(new EmailConfig()).Lines, Str.WidgetUi_AccountsLabel.Text()).Children!.Where(l => !l.IsSeparator).Select(l => l.Text);

        Assert.Equal([EmailStr.WidgetsB_Settings_Email_Every_Label.Text(), "Work", "Home", Str.WidgetsA_Office_ManageAccounts.Text()], accounts);
    }
}
