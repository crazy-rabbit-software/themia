namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2056] The note's sample: a short real note (a heading, a checklist, one line of text) at
/// rest, unbound, so it is never saved: there is no note in the store to save it to.
/// </summary>
internal sealed partial class NotesView : IWidgetSample
{
    private const string SampleNote = "# Groceries\n- [x] Oat milk\n- [ ] Coffee beans\n- [ ] Flowers for Mia\n- [ ] Bread\n\nThe bakery closes at six today.";

    public Task ShowSampleAsync()
    {
        _state = NoteState.Unbound;
        _editor.IsReadOnly = false;
        _editor.Reset(SampleNote);
        UpdateMessage();
        Lay();
        return Task.CompletedTask;
    }
}
