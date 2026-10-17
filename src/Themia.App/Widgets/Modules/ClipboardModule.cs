using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("clipboard", typeof(Themia.App.Widgets.Modules.ClipboardModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Clipboard widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class ClipboardModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => ClipboardConfig.Descriptor;

    public FrameworkElement CreateView() => new ClipboardView();
}
