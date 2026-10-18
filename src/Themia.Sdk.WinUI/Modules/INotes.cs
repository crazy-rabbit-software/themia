using Themia.Core.Notes;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-1348] The notes kept on this PC as a Notes widget and its settings page reach them
/// (<see cref="IWidgetHost.Notes"/>, <see cref="SettingsHost.Notes"/>): the host's one pool, so two
/// widgets can show one note, typing in one shows up in the other, and deleting a widget never takes its
/// note. The UI thread is the only caller; the files (<c>notes/*.md</c> and <c>index.json</c>), their
/// atomic writes and their validation stay the host's.
/// </summary>
public interface INotes
{
    /// <summary>
    /// [S-1348] Raised on the UI thread after every change: with a note's id when only its body changed,
    /// with null when the list did (created, renamed, deleted).
    /// </summary>
    event Action<string?>? Changed;

    /// <summary>Raised on the UI thread when <see cref="Saving"/> or <see cref="WriteFailed"/> may have changed.</summary>
    event Action? SaveStateChanged;

    /// <summary>Every note, in list order (title, then id).</summary>
    IReadOnlyList<NoteMeta> Notes { get; }

    /// <summary>True when the last disk write failed; cleared by the next one that lands.</summary>
    bool WriteFailed { get; }

    /// <summary>True while a write is queued or running.</summary>
    bool Saving { get; }

    NoteMeta? Find(string? id);

    /// <summary>[S-1345] A body: the latest typed, else read from disk once; <see cref="NoteReadStatus.Missing"/> for a note there is not.</summary>
    NoteRead Read(string? id);

    /// <summary>[S-1346] A new note named uniquely from <paramref name="baseName"/>; its id.</summary>
    string Create(string baseName, string content = "");

    /// <summary>[S-1348] Replace a body. Dropped, with a warning, when the note is gone ([S-1342]).</summary>
    void Write(string? id, string content);

    void Rename(string id, string title);

    /// <summary>[S-1347] Delete a note. Every widget showing it says so; none is handed another note.</summary>
    void Delete(string id);
}
