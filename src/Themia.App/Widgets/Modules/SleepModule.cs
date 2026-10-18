using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("sleep", typeof(Themia.App.Widgets.Modules.SleepModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Sleep widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class SleepModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => SleepConfig.Descriptor;

    public FrameworkElement CreateView(IWidgetHost host) => new PowerButtonView(SleepConfig.Descriptor.Id, host);
}
