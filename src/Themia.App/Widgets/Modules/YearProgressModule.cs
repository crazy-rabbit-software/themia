using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("yearProgress", typeof(Themia.App.Widgets.Modules.YearProgressModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The YearProgress widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class YearProgressModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => YearProgressConfig.Descriptor;
}
