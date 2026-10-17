using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("email", typeof(Themia.App.Widgets.Modules.EmailModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Email widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class EmailModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => EmailConfig.Descriptor;

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WidgetSettings.Email(rows.Rows, (EmailConfig)config, rows.Save, rows.ShowAccounts);

    public FrameworkElement CreateView(IWidgetHost host) => new EmailView(host);
}
