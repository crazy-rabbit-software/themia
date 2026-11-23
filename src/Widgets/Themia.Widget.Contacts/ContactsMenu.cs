using Themia.App.Services.Accounts;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Items;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Contacts;

/// <summary>
/// [S-7511] [S-16972] [S-16973] The contacts widgets' options, all on their page ([S-3719]): the accounts they read (the SDK's
/// account lines, [S-580]); the list's item base lines (its parts in order, the gap or a table's column titles, its rows),
/// its search box and how many people ([S-583]); the Contact's person and its parts.
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

        AccountLine(menu, config, accounts);

        // [S-16972] How they are listed: the item base's lines, then the search box and how many.
        menu.Lines.Add(SettingsLine.Separator);
        var locale = Strings.Current;
        ItemMenuLines.List(menu, config, ContactItems.Parts, name: part => ContactItems.PartName(part, locale));
        menu.Lines.Add(menu.Toggle(Glyphs.Search, ContactsStr.Contacts_SearchBox.Text(), config.SearchBox, on => config.SearchBox = on));

        // [S-583] / [S-1276] At least five, in steps of five.
        menu.Lines.Add(KitMenuLines.Count(menu, Glyphs.Count, ContactsStr.WidgetsA_Contacts_Settings_Count_Label.Text(), config.EffectiveCount, 5, MaxCount, 5, value => config.Count = value));
    }

    /// <summary>[S-16973] The Contact's: the person (one of the people the contacts widgets read last), the parts, the accounts.</summary>
    public static void Lines(WidgetMenuLines menu, ContactConfig config, IReadOnlyList<AccountInfo> accounts)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(accounts);

        var locale = Strings.Current;
        var options = new List<(string Value, string Text)> { (string.Empty, ContactsStr.Contacts_Pick.Text()) };
        options.AddRange(ContactsSeen.Last.Take(200).Select(c => (c.Id, c.Name)));
        if (!options.Any(o => o.Value == config.Person))
        {
            options.Add((config.Person, ContactsStr.WidgetsA_Contacts_None.Text()));
        }

        menu.Lines.Add(menu.Choice(Glyphs.Emoji, ContactsStr.Contacts_Person.Text(), options, config.Person, value => config.Person = value));
        menu.Lines.Add(ItemMenuLines.Parts(menu, config, ContactItems.Parts, name: part => ContactItems.PartName(part, locale)));
        menu.Lines.Add(SettingsLine.Separator);
        AccountLine(menu, config, accounts);
    }

    private static void AccountLine(WidgetMenuLines menu, ContactsBaseConfig config, IReadOnlyList<AccountInfo> accounts)
        => menu.Lines.Add(WidgetMenuLines.Shared(SharedSettings.ContactAccounts, OfficeMenuLines.Accounts(menu, accounts, Glyphs.Contact,
            new KitAccountWords(ContactsStr.WidgetsA_Contacts_NoAccount.Text(), ContactsStr.WidgetsA_Contacts_Settings_Every.Text()),
            config.AccountIds, ids => config.AccountIds = ids)));
}
