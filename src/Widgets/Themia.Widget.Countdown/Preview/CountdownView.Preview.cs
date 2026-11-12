#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> is a launch three days after the sample day at 16:00, set two days before it ([S-2050]), in the
/// widget's own style, so the three styles ([S-6030]) are drawn over the same target.
/// </summary>
internal sealed partial class CountdownView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state != "sample")
        {
            return Task.CompletedTask;
        }

        var now = SampleDay.Now;
        _clock = () => now;
        _config = new CountdownConfig { Style = _config.Style, Label = "Launch" };
        _config.Retarget(SampleDay.At(SampleDay.Today.AddDays(3), new TimeOnly(16, 0), TimeZoneInfo.Local), now.AddDays(-2));

        // [S-6037] The name is the card's header.
        ShowName();

        Rebuild();
        return Task.CompletedTask;
    }
}
#endif
