using Themia.App.Services.Accounts;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Contacts;

/// <summary>
/// [S-7511] The contacts widget's options, all on its properties menu ([S-3719]): Appearance
/// ([S-3723]), the accounts it reads (the SDK's account lines, [S-580]), the layout, the details
/// table's columns ([S-582], on the details layout only, [S-6074]) and how many people ([S-583]).
/// It has no settings page.
/// </summary>
internal static class ContactsMenu
{
    /// <summary>[S-583] The most the count line takes: no real bound, but a number box needs one.</summary>
    internal const int MaxCount = 1000;

    /// <param name="accounts">The signed-in accounts offering contacts (the host's office source).</param>
    public static void Lines(WidgetMenuLines menu, ContactsConfig config, IReadOnlyList<AccountInfo> accounts)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(accounts);

        menu.Lines.Add(OfficeMenuLines.Accounts(menu, accounts, Glyphs.Contact,
            new KitAccountWords(ContactsStr.WidgetsA_Contacts_NoAccount.Text(), ContactsStr.WidgetsA_Contacts_Settings_Every.Text()),
            config.AccountIds, ids => config.AccountIds = ids));

        // [S-7511] Where the people come from, then how they are listed.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(menu.Choice(Glyphs.View, ContactsStr.WidgetsA_Contacts_Settings_Layout_Label.Text(),
            [(ContactsLayout.List, ContactsStr.Apps_ViewList.Text()), (ContactsLayout.Compact, ContactsStr.Apps_ViewCompact.Text()), (ContactsLayout.Details, Str.CtxMenu_Details.Text()), (ContactsLayout.Grid, ContactsStr.Apps_ViewGrid.Text())],
            config.EffectiveLayout, value => config.Layout = value));

        // [S-582] The table's columns: canonical order, and the last one cannot be turned off. [S-6074]
        // Offered on the details layout only; picking another builds the menu again.
        if (config.Offers(ContactsSetting.Columns))
        {
            menu.Lines.Add(WidgetMenuLines.Submenu(Glyphs.Grid, ContactsStr.WidgetsA_Contacts_Menu_Columns.Text(), Columns(menu, config)));
        }

        // [S-11406] What each layout shows: the List's rows and lines, Compact's and Details' rows, the
        // Grid's cells a line and lines; then the search box.
        if (config.EffectiveLayout == ContactsLayout.Grid)
        {
            menu.Lines.Add(KitMenuLines.Count(menu, null, Str.Kit_Columns.Text(), Math.Clamp(config.GridColumns, 2, ContactsConfig.MaxGrid), 2, ContactsConfig.MaxGrid, 1, v => config.GridColumns = v));
            menu.Lines.Add(KitMenuLines.Count(menu, null, Str.Kit_Rows.Text(), Math.Clamp(config.GridRows, 1, ContactsConfig.MaxGrid), 1, ContactsConfig.MaxGrid, 1, v => config.GridRows = v));
        }
        else
        {
            menu.Lines.AddRange(KitMenuLines.Rows(menu, config, offersLines: config.EffectiveLayout == ContactsLayout.List, mostLines: 3));
        }

        menu.Lines.Add(menu.Toggle(Glyphs.Search, ContactsStr.Contacts_SearchBox.Text(), config.SearchBox, on => config.SearchBox = on));

        // [S-583] / [S-1276] At least five, in steps of five.
        menu.Lines.Add(KitMenuLines.Count(menu, Glyphs.Count, ContactsStr.WidgetsA_Contacts_Settings_Count_Label.Text(), config.EffectiveCount, 5, MaxCount, 5, value => config.Count = value));
    }

    private static List<ItemMenuEntry> Columns(WidgetMenuLines menu, ContactsConfig config)
    {
        var shown = config.EffectiveColumns;
        return
        [
            .. ContactColumns.All.Select(column =>
            {
                var on = shown.Contains(column);
                return menu.Toggle(null, ContactRules.ColumnTitle(column, Strings.Current), on,
                    _ => config.Columns = ContactRules.ToggleColumn(config.EffectiveColumns, column)) with { Disabled = on && shown.Count == 1 };
            }),
        ];
    }
}
