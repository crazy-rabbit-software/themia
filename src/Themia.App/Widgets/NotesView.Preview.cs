#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: a fixed note, at rest (<c>sample</c>) or as it stands while it has the caret
/// (<c>editing</c>: the formatting bar in the header, [S-3963]), so the header can be looked at
/// without a pool or a keyboard. The draft is never saved: a render has no note to save it to.
/// <c>confirm</c> is the sample with the delete question open ([S-1500]); nothing is deleted.
/// </summary>
internal sealed partial class NotesView : IWidgetPreviewState
{
    private const string SampleNote = "# Groceries\n- [x] Oat milk\n- [ ] **Coffee** beans\n- [ ] Bread\n\nAsk about the *Saturday* market.";

    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "editing", "confirm"];

    public Task ShowPreviewStateAsync(string state)
    {
        _state = NoteState.Unbound;
        _editor.IsReadOnly = false;
        _editor.Reset(SampleNote);
        _previewEditing = state == "editing";

        UpdateMessage();
        Lay();

        if (state == "confirm")
        {
            // Never answered: the render closes with the question open.
            _ = AskToDeleteAsync("Groceries");

            // The flyout opens with an animation; the render draws it once it has settled.
            return Task.Delay(900);
        }

        return Task.CompletedTask;
    }
}
#endif
