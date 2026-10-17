using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;

[assembly: ThemiaWidget("quote", typeof(Themia.App.Widgets.Modules.QuoteModule))]

namespace Themia.App.Widgets.Modules;

/// <summary>[S-6902] The Quote widget as a module, declared inside the app until it moves to its own project.</summary>
public sealed class QuoteModule : IWidgetModule
{
    public WidgetDescriptor Descriptor => QuoteConfig.Descriptor;

    public FrameworkElement CreateView() => new QuoteView();
}
