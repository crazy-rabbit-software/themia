using Themia.App.Shell.Gallery;

namespace Themia.Widget.Metrics;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> is the add page's picture (<see cref="ShowSampleAsync"/>), <c>stale</c> the
/// sample kept through a failed refresh (the status's problem glyph), <c>empty</c> no answer yet: the graph stands at its
/// foot and the reading is "--" (WR-10). A system metric's states are its host's (the sample PC).
/// </summary>
internal sealed partial class MetricsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale", "empty"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is "sample" or "stale" or "empty")
        {
            ShowSample(stale: state == "stale", empty: state == "empty");
        }

        return Task.CompletedTask;
    }
}
