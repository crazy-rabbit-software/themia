using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>[S-11850] The quick toggles' sample: switched on, as the add page shows them.</summary>
public sealed partial class QuickToggleView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _preview = ToggleReading.Of(true);
        Lay();
        return Task.CompletedTask;
    }
}
