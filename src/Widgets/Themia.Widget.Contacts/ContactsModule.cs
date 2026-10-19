using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Contacts;

[assembly: ThemiaWidget("contacts", typeof(ContactsModule))]

namespace Themia.Widget.Contacts;

/// <summary>Contacts: the people of the signed-in accounts, searched and opened. SPEC.md beside this file.</summary>
public sealed class ContactsModule : IWidgetModule
{
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
        DefaultWidth = 320,
        DefaultHeight = 260,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.CalendarMail, 1, 2),
    };

    /// <summary>[S-3719] [S-7511] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => ContactsMenu.Lines(menu, (ContactsConfig)config, SettingsHost.Office?.Accounts(HostFeatures.Contacts) ?? []);

    public FrameworkElement CreateView(IWidgetHost host) => new ContactsView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ContactsConfig))]
internal sealed partial class ContactsJsonContext : JsonSerializerContext;
