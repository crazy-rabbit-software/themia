using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services.Accounts;
using Themia.App.Services.Office;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Shell.Config;

/// <summary>
/// The account rows the calendar and contacts settings share, and the contacts page itself.
/// <para>
/// Accounts are signed in on the Accounts page, not here ([S-537] of the classic widgets): this
/// lists the accounts already signed in that can feed the widget, and links there — so there is one
/// place an account comes into being, and the page is not filtered by widget.
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    /// <summary>[S-580] The contacts widget: accounts, layout, the details table's columns, count and search.</summary>
    public static void Contacts(WidgetSettingsRows settings, ContactsConfig config)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(config);
        var rows = settings.Rows;
        var save = settings.Save;

        rows.Add(FluentLayout.Header(Str.WidgetUi_AccountsLabel.Text()));
        OfficeSettingsRows.Accounts(settings, HostFeatures.Contacts, Glyphs.Contact,
            new OfficeSettingsRows.OfficeAccountTexts(Str.WidgetsA_Contacts_NoAccount, Str.WidgetsA_Contacts_Settings_Every, Str.WidgetsA_Contacts_Settings_AddHint),
            () => config.AccountIds, ids => config.AccountIds = ids, save, perAccount: null);

        rows.Add(FluentLayout.Header(Str.ConfigTabs_Display.Text()));

        rows.Add(FluentLayout.Row(Glyphs.View, Str.WidgetsA_Contacts_Settings_Layout_Label.Text(),
            null,
            FluentLayout.Choice<ContactsLayout>(
                [(ContactsLayout.List, Str.Apps_ViewList.Text()), (ContactsLayout.Compact, Str.Apps_ViewCompact.Text()), (ContactsLayout.Details, Str.CtxMenu_Details.Text()), (ContactsLayout.Grid, Str.Apps_ViewGrid.Text())],
                config.EffectiveLayout,
                value => { config.Layout = value; save(); })));

        // [S-582] The table's columns: canonical order, and the last one cannot be turned off. [S-6074]
        // Offered on the details layout only; picking another builds the page again.
        if (config.Offers(ContactsSetting.Columns))
        {
            ContactColumnRows(rows, config, save);
        }

        // [S-583] / [S-1276] At least five, in steps of five, and no upper bound, as the original.
        rows.Add(FluentLayout.Row(Glyphs.Count, Str.WidgetsA_Contacts_Settings_Count_Label.Text(),
            Str.WidgetsA_Contacts_Settings_Count_Desc.Text(),
            FluentLayout.Stepper(config.EffectiveCount, 5, 5, value => { config.Count = value; save(); })));
    }

    private static void ContactColumnRows(List<FrameworkElement> rows, ContactsConfig config, Action save)
    {
        var toggles = new Dictionary<ContactColumn, ToggleSwitch>();
        var quiet = false;

        foreach (var column in ContactColumns.All)
        {
            var toggle = FluentLayout.Toggle(config.EffectiveColumns.Contains(column), on =>
            {
                if (quiet)
                {
                    return;
                }

                config.Columns = ContactRules.ToggleColumn(config.EffectiveColumns, column);
                save();

                quiet = true;
                foreach (var (c, t) in toggles)
                {
                    t.IsOn = config.EffectiveColumns.Contains(c);
                }

                quiet = false;
            });

            toggles[column] = toggle;
            rows.Add(FluentLayout.Row(Glyphs.Grid, Str.WidgetsA_Contacts_Settings_Column.Format(("name", ContactRules.ColumnTitle(column, Strings.Current))), null, toggle));
        }
    }
}
