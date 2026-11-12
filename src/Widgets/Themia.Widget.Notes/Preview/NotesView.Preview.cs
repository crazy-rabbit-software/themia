#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: the note's sample (<see cref="ShowSampleAsync"/>), at rest (<c>sample</c>) or as it
/// stands while it has the caret (<c>editing</c>: the formatting bar in the header, [S-3963]), so the header
/// can be looked at without a pool or a keyboard. <c>confirm</c> is the sample with the delete question
/// open ([S-1500]); nothing is deleted.
/// </summary>
internal sealed partial class NotesView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "editing", "confirm"];

    public async Task ShowPreviewStateAsync(string state)
    {
        _previewEditing = state == "editing";
        await ShowSampleAsync().ConfigureAwait(true);

        if (state == "confirm")
        {
            // Never answered: the render closes with the question open.
            _ = AskToDeleteAsync("Groceries");

            // The flyout opens with an animation; the render draws it once it has settled.
            await Task.Delay(900).ConfigureAwait(true);
        }
    }
}
#endif
