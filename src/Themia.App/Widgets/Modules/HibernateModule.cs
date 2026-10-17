using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("hibernate", typeof(Themia.App.Widgets.Modules.HibernateModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Hibernate widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class HibernateModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => HibernateConfig.Descriptor;
}
