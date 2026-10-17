using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("notes", typeof(Themia.App.Widgets.Modules.NotesModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Notes widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class NotesModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => NotesConfig.Descriptor;
}
