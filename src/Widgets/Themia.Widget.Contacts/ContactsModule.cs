using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services.Accounts;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Items;
using Themia.Sdk.WinUI;
using Themia.Widget.Contacts;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("contacts", typeof(ContactsModule))]
[assembly: ThemiaWidget("contact", typeof(ContactModule))]

namespace Themia.Widget.Contacts;

/// <summary>Contacts: the people of the signed-in accounts on the item base, searched and opened. SPEC.md beside this file.</summary>
public sealed class ContactsModule : IWidgetModule
{
    /// <summary>[S-2045] A click on the card opens the contacts of the account it reads, on the web ([S-2005]).</summary>
    internal static CardOpen? Opens(IReadOnlyList<string> accountIds, IReadOnlyList<AccountInfo> accounts)
        => AccountPages.Home(HostFeatures.Contacts, accountIds, accounts);

    /// <summary>
    /// [S-16972] [S-15775] The list's sizing: the item base's in its arrangement, the search field's band over it where the box is
    /// on (30 on the default grid).
    /// </summary>
    internal static WidgetSizing Sizing(ContactsConfig config)
    {
        var sizing = ItemLayout.Sizing(config, ContactItems.Parts);
        var band = config.SearchBox ? ContactItems.Band(Themia.Core.Widgets.ItemGrid.Default()) : 0;
        return sizing with
        {
            Min = sizing.Min with { Height = sizing.Min.Height + band },
            Natural = sizing.Natural with { Height = sizing.Natural.Height + band },
            Max = sizing.Max with { Height = sizing.Max.Height + band },
        };
    }

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("contacts"),
        ConfigType = typeof(ContactsConfig),
        CreateDefault = () => new ContactsConfig(),
        Json = ContactsJsonContext.Default,
        Keywords = ["address book", "people", "phone numbers", "directory"],
        Providers = [HostProviders.Microsoft, HostProviders.Google, HostProviders.CardDav],
        Label = ContactsStr.Widget_Contacts_Label.Local(),
        Description = ContactsStr.WidgetsA_Catalog_Contacts_Description.Local(),
        Glyph = Glyphs.Contact,
        Sizing = c => Sizing((ContactsConfig)c),
        Traits = WidgetTraits.HasItems,
        Links = LinkUse.Of([LinkTopicId.HoveredPerson], [LinkTopicId.HoveredPerson]),
        Add = new(AddFolderId.CalendarMail, 1, 2),
    };

    /// <summary>[S-3719] [S-7511] Every option is on the selection's page: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => ContactsMenu.Lines(menu, (ContactsConfig)config, SettingsHost.Office?.Accounts(HostFeatures.Contacts) ?? []);

    public FrameworkElement CreateView(IWidgetHost host) => new ContactsView(host);
}

/// <summary>[S-16973] The Contact: one person picked, on the item base's face. SPEC.md beside this file.</summary>
public sealed class ContactModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("contact"),
        ConfigType = typeof(ContactConfig),
        CreateDefault = () => new ContactConfig(),
        Json = ContactsJsonContext.Default,
        Keywords = ["person", "contact", "call", "email", "people", "favourite"],
        Providers = [HostProviders.Microsoft, HostProviders.Google, HostProviders.CardDav],
        Label = ContactsStr.Contacts_Single_Label.Local(),
        Description = ContactsStr.Contacts_Single_Desc.Local(),
        Glyph = Glyphs.Contact,
        Sizing = c => ItemLayout.Face(ItemShape.Of(ContactItems.Parts, ((ContactConfig)c).HiddenParts), ItemLayout.WidestLine(["Engineer · Contoso", "ana.silva@contoso.example"]), 0),
        Traits = WidgetTraits.HasItems,
        Links = LinkUse.Of([LinkTopicId.HoveredPerson], [LinkTopicId.HoveredPerson]),
        Add = new(AddFolderId.CalendarMail, 1, 3),
    };

    /// <summary>[S-16973] Which person, what the face shows, then the accounts.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => ContactsMenu.Lines(menu, (ContactConfig)config, SettingsHost.Office?.Accounts(HostFeatures.Contacts) ?? []);

    public FrameworkElement CreateView(IWidgetHost host) => new ContactView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ContactsConfig))]
[JsonSerializable(typeof(ContactConfig))]
internal sealed partial class ContactsJsonContext : JsonSerializerContext;
