using Themia.App.Services.Accounts;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Email;

/// <summary>
/// [S-7512] The Mail widget's options, all on its properties menu ([S-3719]): under Style what the
/// style draws with ([S-6403], [S-6070]: the subject and account lines, the preview's lines, the
/// table's columns); then Appearance ([S-3723]), the accounts it reads ([S-541], the kit's Accounts
/// submenu) and the folder. It has no settings page.
/// <para>
/// Accounts are signed in from ⚙ › Accounts, not here: the menu lists what is already signed in
/// and opens that page, so there is exactly one place an account comes into being.
/// </para>
/// </summary>
internal static class EmailMenu
{
    /// <param name="accounts">The signed-in mail accounts (<see cref="SettingsHost.AccountsWith"/>).</param>
    public static void Lines(WidgetMenuLines menu, EmailConfig email, IReadOnlyList<AccountInfo> accounts)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(accounts);

        var look = menu.StyleLines;
        if (email.Offers(EmailSetting.ListFields))
        {
            // [S-543] The optional lines of the list and compact styles. Sender and time always show.
            look.Add(Field(menu, email, MailListField.Subject, EmailStr.WidgetsB_Settings_Email_ShowSubject.Text()));
            look.Add(Field(menu, email, MailListField.Account, EmailStr.WidgetsB_Settings_Email_ShowAccount_Label.Text()));
        }

        if (email.Offers(EmailSetting.PreviewLines))
        {
            look.Add(menu.Number(Glyphs.AlignLeft, EmailStr.Email_PreviewLines_Label.Text(), email.EffectivePreviewLines, 1, 5, 1, v => LocaleFormat.Integer(v), value => email.PreviewLines = value));
        }

        if (email.Offers(EmailSetting.Columns))
        {
            look.Add(Columns(menu, email));
        }

        menu.Lines.Add(OfficeMenuLines.Accounts(menu, accounts, Glyphs.Mail,
            new KitAccountWords(EmailStr.WidgetsB_Settings_Email_NoAccount_Label.Text(), EmailStr.WidgetsB_Settings_Email_Every_Label.Text()),
            email.EffectiveAccountIds, ids => email.AccountIds = ids));
        menu.Lines.Add(menu.Choice(Glyphs.Folder, EmailStr.Email_Folder_Label.Text(),
            [.. EmailFolders.Listable.Select(f => (f, f.Label()))], email.Folder, value => email.Folder = value));
    }

    /// <summary>[S-543] One optional line's switch, the fields kept in their canonical order.</summary>
    private static ItemMenuEntry Field(WidgetMenuLines menu, EmailConfig email, MailListField field, string text)
        => menu.Toggle(null, text, email.EffectiveListFields.Contains(field), on =>
        {
            var fields = email.EffectiveListFields.ToList();
            fields.Remove(field);
            if (on)
            {
                fields.Add(field);
            }

            // Kept in the canonical order, so the file reads the same however it was toggled.
            email.ListFields = [.. EmailConfig.DefaultListFields.Where(fields.Contains)];
        });

    /// <summary>
    /// [S-543] The table's columns as the menu's list ([S-3729]): those on, in their order, each with
    /// Move up, Move down and Remove (greyed on the last one: one always stays); the ones that are off
    /// to add after them; then what a narrow table drops first.
    /// </summary>
    private static ItemMenuEntry Columns(WidgetMenuLines menu, EmailConfig email)
    {
        // The defaults made the widget's own so the list edits them in place: the same columns.
        var columns = email.RowColumns is { Count: > 0 } chosen ? chosen : [.. EmailConfig.DefaultColumns];
        email.RowColumns = columns;

        var add = new List<ItemMenuEntry>();
        foreach (var column in Enum.GetValues<MailColumn>().Where(c => !columns.Contains(c)))
        {
            add.Add(menu.Command(Glyphs.Add, column.Label(), () => columns.Add(column)));
        }

        add.Add(ItemMenuEntry.Separator);
        add.Add(WidgetMenuLines.Note(EmailStr.WidgetsB_Settings_Email_ColumnsNote.Text()));

        return menu.List(Glyphs.Grid, EmailStr.WidgetsB_Email_Menu_Columns.Text(), columns,
            new MenuList<MailColumn>(c => c.Label(), string.Empty)
            {
                Glyph = ColumnGlyph,
                AddLines = add,
                Min = 1,
            });
    }

    private static string ColumnGlyph(MailColumn column) => column switch
    {
        MailColumn.From => Glyphs.Contact,
        MailColumn.Subject => Glyphs.Mail,
        MailColumn.Date => Glyphs.Calendar,
        MailColumn.Preview => Glyphs.Preview,
        _ => Glyphs.Contact,
    };
}
