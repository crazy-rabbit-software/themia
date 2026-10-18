using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("contacts", typeof(Themia.App.Widgets.Modules.ContactsModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Contacts widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ContactsModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ContactsConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Contacts(rows, (ContactsConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new ContactsView(host);
}
