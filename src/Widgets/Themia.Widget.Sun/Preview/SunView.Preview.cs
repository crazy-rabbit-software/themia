using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> is the sample (<see cref="ShowSampleAsync"/>); [S-15809] <c>noPlace</c> the
/// sun with its place cleared ([S-2848]): it keeps its face, drawn for the default place at the sample's moment, under the
/// card's compact Set up line (WR-10), as a cleared sun stands on the desktop ([S-1881]).
/// </summary>
internal sealed partial class SunView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "noPlace"];

    /// <summary>[S-15809] The state's own config where it is one the card would set up (its place cleared).</summary>
    public WidgetConfig? DrawnConfig { get; private set; }

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "noPlace")
        {
            var config = (SunConfig)_config.Clone();
            config.ClearLocation();
            DrawnConfig = config;
            _now = SampleDay.Now;
            ApplyConfig(config);
            return Task.CompletedTask;
        }

        return state == "sample" ? ShowSampleAsync() : Task.CompletedTask;
    }
}
