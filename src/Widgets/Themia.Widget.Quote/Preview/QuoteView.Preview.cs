#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): the quote shown is the rotation's of the day, so a render pins one:
/// <c>sample</c> the bundled Newton (a sentence of a typical length), <c>long</c> the longest bundled
/// one (the plant-a-tree proverb), so the step and the ellipsis compare across runs and days.
/// </summary>
internal sealed partial class QuoteView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "long"];

    public Task ShowPreviewStateAsync(string state)
    {
        _pinned = state switch
        {
            "sample" => 3,
            "long" => 13,
            _ => _pinned,
        };

        Render();
        return Task.CompletedTask;
    }
}
#endif
