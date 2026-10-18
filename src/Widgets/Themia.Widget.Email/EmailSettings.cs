using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.App.Shell;
using Themia.App.Shell.Config;

namespace Themia.Widget.Email;

/// <summary>
/// [S-541] The Mail widget's settings: which of Themia's accounts it reads, the folder, and how
/// each message is drawn. The classic widget's Source and Display
/// tabs, on one page.
/// <para>
/// Accounts themselves are signed in on the Accounts page, not here: this page lists what is
/// already signed in and links there, so there is exactly one place an account comes into being.
/// </para>
/// </summary>
internal static class EmailSettings
{
    public static void Rows(List<FrameworkElement> rows, EmailConfig email, Action save, Action showAccounts)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(showAccounts);


        rows.Add(FluentLayout.Header(Str.WidgetUi_AccountsLabel.Text()));
        MailAccountRows(rows, email, save, showAccounts);

        rows.Add(FluentLayout.Header(EmailStr.WidgetsB_Settings_Email_Messages.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Folder, EmailStr.Email_Folder_Label.Text(), EmailStr.WidgetsB_Settings_Email_Folder_Desc.Text(),
            FluentLayout.Choice<MailFolder>(
                [.. EmailFolders.Listable.Select(f => (f, f.Label()))],
                email.Folder,
                value => { email.Folder = value; save(); })));

        rows.Add(FluentLayout.Header(Str.WidgetsB_Settings_Email_Display.Text()));

        // [S-6403] The view is the widget's style (on the properties panel, [S-6536]): only what
        // that style draws with is offered here, and picking another style rebuilds the page ([S-6071]).
        if (email.Offers(EmailSetting.ListFields))
        {
            // [S-543] The optional lines of the list and compact styles. Sender and time always show.
            foreach (var (field, glyph, title, description) in new[]
            {
                (MailListField.Subject, Glyphs.Mail, EmailStr.WidgetsB_Settings_Email_ShowSubject.Text(), (string?)null),
                (MailListField.Account, Glyphs.Contact, EmailStr.WidgetsB_Settings_Email_ShowAccount_Label.Text(), EmailStr.WidgetsB_Settings_Email_ShowAccount_Desc.Text()),
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

        if (email.Offers(EmailSetting.PreviewLines))
        {
            rows.Add(FluentLayout.Row(Glyphs.Wrap, EmailStr.Email_PreviewLines_Label.Text(), EmailStr.WidgetsB_Settings_Email_PreviewLines_Desc.Text(),
                FluentLayout.Slider(email.EffectivePreviewLines, 1, 5, 1, string.Empty,
                    value => { email.PreviewLines = (int)Math.Round(value); save(); })));
        }

        if (!email.Offers(EmailSetting.Columns))
        {
            return;
        }

        rows.Add(FluentLayout.Note(EmailStr.WidgetsB_Settings_Email_ColumnsNote.Text()));

        var columns = new StackPanel { Spacing = Fluent.Space1 };
        rows.Add(columns);
        MailColumnRows(columns, email, save);
    }

    /// <summary>[S-541] [S-7063] "Every mail account", or a choice of them: the kit's accounts row.</summary>
    private static void MailAccountRows(List<FrameworkElement> rows, EmailConfig email, Action save, Action openAccounts)
    {
        var accounts = SettingsHost.AccountsWith(HostFeatures.Mail)
            .Select(a => new KitAccountRow(a.Id, a.Label, a.Problem ?? $"{ProviderCatalog.Of(a.Provider).Label.Text()} · {a.Address}"))
            .ToList();
        KitRows.Accounts(rows, Glyphs.Mail, accounts,
            new KitAccountTexts(EmailStr.WidgetsB_Settings_Email_NoAccount_Label.Text(), EmailStr.WidgetsB_Settings_Email_Every_Label.Text(), EmailStr.WidgetsB_Settings_Email_NoAccount_Desc.Text()),
            () => [.. email.EffectiveAccountIds], ids => email.AccountIds = ids, save, openAccounts);
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
