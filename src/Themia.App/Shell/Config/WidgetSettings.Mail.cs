using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-541] The Mail widget's settings: which of Themia's accounts it reads, the folder, and how
/// each message is drawn. The classic widget's Source and Display
/// tabs, on one page.
/// <para>
/// Accounts themselves are signed in on the Accounts page, not here: this page lists what is
/// already signed in and links there, so there is exactly one place an account comes into being.
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    public static void Email(List<FrameworkElement> rows, EmailConfig email, Action save, Action<ConfigTarget> navigate)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(navigate);

        void OpenAccounts() => navigate(new ConfigTarget.App(AppPage.Accounts));

        rows.Add(FluentLayout.Header(Str.WidgetUi_AccountsLabel.Text()));
        MailAccountRows(rows, email, save, OpenAccounts);

        rows.Add(FluentLayout.Header(Str.WidgetsB_Settings_Email_Messages.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Folder, Str.Email_Folder_Label.Text(), Str.WidgetsB_Settings_Email_Folder_Desc.Text(),
            FluentLayout.Choice<MailFolder>(
                [.. MailFolders.Listable.Select(f => (f, f.Label()))],
                email.Folder,
                value => { email.Folder = value; save(); })));

        rows.Add(FluentLayout.Header(Str.WidgetsB_Settings_Email_Display.Text()));

        // [S-6403] The view is the widget's style (the shared Style row at the top of the page):
        // only what that style draws with is offered here, and picking another style rebuilds the
        // page ([S-6010]).
        var own = EmailConfig.SettingsFor(email.Style);

        if (own.Contains(EmailSetting.ListFields))
        {
            // [S-543] The optional lines of the list and compact styles. Sender and time always show.
            foreach (var (field, glyph, title, description) in new[]
            {
                (MailListField.Subject, Glyphs.Mail, Str.WidgetsB_Settings_Email_ShowSubject.Text(), (string?)null),
                (MailListField.Account, Glyphs.Contact, Str.WidgetsB_Settings_Email_ShowAccount_Label.Text(), Str.WidgetsB_Settings_Email_ShowAccount_Desc.Text()),
            })
            {
                rows.Add(FluentLayout.Row(glyph, title, description, FluentLayout.Toggle(email.EffectiveListFields.Contains(field), on =>
                {
                    var fields = email.EffectiveListFields.ToList();
                    fields.Remove(field);
                    if (on)
                    {
                        fields.Add(field);
                    }

                    // Kept in the canonical order, so the file reads the same however it was toggled.
                    email.ListFields = [.. EmailConfig.DefaultListFields.Where(fields.Contains)];
                    save();
                })));
            }
        }

        if (own.Contains(EmailSetting.PreviewLines))
        {
            rows.Add(FluentLayout.Row(Glyphs.Wrap, Str.Email_PreviewLines_Label.Text(), Str.WidgetsB_Settings_Email_PreviewLines_Desc.Text(),
                FluentLayout.Slider(email.EffectivePreviewLines, 1, 5, 1, string.Empty,
                    value => { email.PreviewLines = (int)Math.Round(value); save(); })));
        }

        if (!own.Contains(EmailSetting.Columns))
        {
            return;
        }

        rows.Add(FluentLayout.Note(Str.WidgetsB_Settings_Email_ColumnsNote.Text()));

        var columns = new StackPanel { Spacing = Fluent.Space1 };
        rows.Add(columns);
        MailColumnRows(columns, email, save);
    }

    /// <summary>
    /// "Every mail account", or a choice of them. Every is the default and means accounts added later
    /// join too; turning it off starts from the accounts there are now, so nothing disappears.
    /// </summary>
    private static void MailAccountRows(List<FrameworkElement> rows, EmailConfig email, Action save, Action openAccounts)
    {
        var accounts = AppServices.Accounts?.WithFeature(AccountFeatures.Mail) ?? [];

        var manage = new Button { Content = accounts.Count == 0 ? Str.WidgetsB_Settings_Email_AddAccount.Text() : Str.WidgetsB_Settings_Email_ManageAccounts.Text() };
        manage.Click += (_, _) => openAccounts();

        if (accounts.Count == 0)
        {
            rows.Add(FluentLayout.Row(Glyphs.Mail, Str.WidgetsB_Settings_Email_NoAccount_Label.Text(),
                Str.WidgetsB_Settings_Email_NoAccount_Desc.Text(), manage));
            return;
        }

        var toggles = new List<ToggleSwitch>();
        var chosenRows = new List<FrameworkElement>();

        // Set while this page moves a switch itself, so the switch's own handler does not take that
        // for the user choosing one account.
        var quiet = false;
        var every = email.EffectiveAccountIds.Count == 0;

        void ShowChoice()
        {
            foreach (var row in chosenRows)
            {
                row.Visibility = email.EffectiveAccountIds.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        rows.Add(FluentLayout.Row(Glyphs.Mail, Str.WidgetsB_Settings_Email_Every_Label.Text(), Str.WidgetsB_Settings_Email_Every_Desc.Text(),
            FluentLayout.Toggle(every, on =>
            {
                // Off starts from every account there is now, so turning it off changes nothing on
                // screen until an account is then turned off.
                email.AccountIds = on ? [] : [.. accounts.Select(a => a.Id)];

                quiet = true;
                foreach (var toggle in toggles)
                {
                    toggle.IsOn = true;
                }

                quiet = false;

                save();
                ShowChoice();
            })));

        foreach (var account in accounts)
        {
            var index = toggles.Count;
            var toggle = FluentLayout.Toggle(every || email.EffectiveAccountIds.Contains(account.Id), on =>
            {
                if (quiet)
                {
                    return;
                }

                var ids = email.EffectiveAccountIds.ToList();
                ids.Remove(account.Id);
                if (on)
                {
                    ids.Add(account.Id);
                }

                // At least one stays: an empty list means "every account", the opposite of the
                // user turning the last one off.
                if (ids.Count == 0)
                {
                    quiet = true;
                    toggles[index].IsOn = true;
                    quiet = false;
                    return;
                }

                email.AccountIds = [.. accounts.Select(a => a.Id).Where(ids.Contains)];
                save();
            });

            toggles.Add(toggle);

            var description = account.Problem ?? $"{account.Provider.Label()} · {account.Address}";
            var row = FluentLayout.Row(EmailView.FolderGlyph(MailFolder.Inbox), account.Label, description, toggle);
            chosenRows.Add(row);
            rows.Add(row);
        }

        rows.Add(FluentLayout.Row(Glyphs.Contact, Str.WidgetUi_AccountsLabel.Text(), Str.WidgetsB_Settings_Email_Accounts_Desc.Text(), manage));
        ShowChoice();
    }

    /// <summary>
    /// [S-543] The row view's columns: on or off, and in which order. One always stays on. The
    /// columns that are on are put in order by dragging one onto another or with Alt+Shift+Up / Down
    /// ([S-3802]); the ones that are off follow them and do not move.
    /// </summary>
    private static void MailColumnRows(StackPanel host, EmailConfig email, Action save)
    {
        RowReorder<MailColumn>? reorder = null;

        bool Commit(List<MailColumn> next, bool moved)
        {
            if (moved)
            {
                email.RowColumns = next;
                save();
                Rebuild();
            }

            return moved;
        }

        reorder = new RowReorder<MailColumn>(
            (dragged, target) =>
            {
                var next = email.EffectiveColumns.ToList();
                Commit(next, DeskListOrder.MoveTo(next, next.IndexOf(dragged), next.IndexOf(target)));
            },
            (column, delta) =>
            {
                var next = email.EffectiveColumns.ToList();
                return Commit(next, DeskListOrder.Move(next, next.IndexOf(column), delta));
            });

        void Rebuild()
        {
            host.Children.Clear();

            var chosen = email.EffectiveColumns.ToList();
            var all = chosen.Concat(Enum.GetValues<MailColumn>().Where(c => !chosen.Contains(c))).ToList();

            foreach (var column in all)
            {
                var toggle = FluentLayout.Toggle(chosen.Contains(column), value =>
                {
                    var next = email.EffectiveColumns.ToList();
                    if (value)
                    {
                        next.Add(column);
                    }
                    else if (next.Count > 1)
                    {
                        next.Remove(column);
                    }

                    email.RowColumns = next;
                    save();
                    Rebuild();
                });

                var row = FluentLayout.Row(ColumnGlyph(column), column.Label(), null, toggle);
                if (chosen.Contains(column))
                {
                    reorder!.Attach(row, column);
                }

                host.Children.Add(row);
            }

            if (chosen.Count > 1)
            {
                host.Children.Add(FluentLayout.Note(Str.Shell_List_ReorderHint.Text()));
            }
        }

        Rebuild();
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
