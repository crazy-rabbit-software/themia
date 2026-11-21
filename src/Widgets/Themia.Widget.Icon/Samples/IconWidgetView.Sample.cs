using Themia.App.Widgets;

namespace Themia.Widget.Icon;

/// <summary>[S-11850] The add page's picture of the Icon widget: the default icon, the star. It reads nothing.</summary>
internal sealed partial class IconWidgetView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        Show(null);
        return Task.CompletedTask;
    }
}
