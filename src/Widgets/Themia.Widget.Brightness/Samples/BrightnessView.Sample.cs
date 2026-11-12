using Themia.App.Services.Brightness;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>[S-11850] The brightness widget's sample: two made-up displays at their levels, never this PC's.</summary>
internal sealed partial class BrightnessView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        _preview = new BrightnessSnapshot(true, [new(@"\\.\SAMPLE1", BrightnessPath.BuiltIn, 70), new(@"\\.\SAMPLE2", BrightnessPath.Ddc, 45)]);
        Lay();
        return Task.CompletedTask;
    }
}
