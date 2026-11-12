using Themia.App.Widgets;

namespace Themia.Widget.ScreenTime;

/// <summary>[S-11850] The screen-time widgets' sample: a month of the sample day's usage (<see cref="SampleScreenTime"/>), never this PC's.</summary>
internal sealed partial class ScreenTimeView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        UseSource(new SampleScreenTime());
        Render();
        return Task.CompletedTask;
    }
}
