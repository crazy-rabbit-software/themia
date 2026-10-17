using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("signOut", typeof(Themia.App.Widgets.Modules.SignOutModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The SignOut widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class SignOutModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => SignOutConfig.Descriptor;
}
