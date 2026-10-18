#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Model;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> is a launch in three days, set two days ago, in the
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

        var now = DateTimeOffset.Now;
        _config = new CountdownConfig { Style = _config.Style, Label = "Launch" };
        _config.Retarget(now.AddDays(3).AddHours(5).AddMinutes(20), now.AddDays(-2));

        Rebuild();
        return Task.CompletedTask;
    }
}
#endif
