using Themia.App.Services.Accounts;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Sdk.WinUI.Items;

namespace Themia.Widget.Email;

/// <summary>
/// [S-7512] [S-16971] The Mail widget's options, all on its page ([S-3719]): the item base's lines (its parts in order with the
/// preview, the gap or a table's column titles, the rows), then the accounts it reads ([S-541], the kit's Accounts submenu)
/// and the folder. It has no settings page.
/// <para>
/// Accounts are signed in from settings › Accounts, not here: the menu lists what is already signed in
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

        // [S-16971] The item base's lines: the parts in order with the preview, the gap or the column titles, the rows.
        ItemMenuLines.List(menu, email, MailSpec.Parts, own: [menu.Toggle(null, Str.Email_Col_Preview.Text(), email.ShowPreview, on => email.ShowPreview = on)], name: MailSpec.PartName);
        menu.Lines.Add(SettingsLine.Separator);

        menu.Lines.Add(WidgetMenuLines.Shared(SharedSettings.MailAccounts, OfficeMenuLines.Accounts(menu, accounts, Glyphs.Mail,
            new KitAccountWords(EmailStr.WidgetsB_Settings_Email_NoAccount_Label.Text(), EmailStr.WidgetsB_Settings_Email_Every_Label.Text()),
            email.EffectiveAccountIds, ids => email.AccountIds = ids)));
        menu.Lines.Add(menu.Choice(Glyphs.Folder, EmailStr.Email_Folder_Label.Text(),
            [.. EmailFolders.Listable.Select(f => (f, f.Label()))], email.Folder, value => email.Folder = value));
    }
}
