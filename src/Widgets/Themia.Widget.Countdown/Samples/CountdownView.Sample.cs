using Themia.Core.Model;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2050] The countdown's sample: a launch three days after the sample day at 16:00, set two
/// days before it, in the widget's own style, so the three styles ([S-6030]) are drawn over the same target.
/// </summary>
internal sealed partial class CountdownView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        var now = SampleDay.Now;
        _clock = () => now;
        _config = new CountdownConfig { Style = _config.Style, Label = "Launch", Size = _config.Size, ShowProgress = _config.ShowProgress, Format = _config.Format };
        _config.Retarget(SampleDay.At(SampleDay.Today.AddDays(3), new TimeOnly(16, 0), TimeZoneInfo.Local), now.AddDays(-2));

        // [S-6037] The name is the card's header.
        ShowName();

        Rebuild();
        return Task.CompletedTask;
    }
}
