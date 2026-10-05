using System.Globalization;
using System.Text;
using System.Text.Json;
using Themia.Core.Diagnostics;

namespace Themia.Core.Notes;

/// <summary>A note as a picker sees it.</summary>
/// <param name="Id">32 lowercase hex digits (<see cref="NoteStore.IsValidId"/>); also the body's file name.</param>
/// <param name="Title">Already cleaned (<see cref="NoteStore.CleanTitle"/>): one line, never blank.</param>
/// <param name="UpdatedAt">Unix seconds of the last body write. "Recently changed", never the list order.</param>
public sealed record NoteMeta(string Id, string Title, long UpdatedAt);

/// <summary>Whether a body was there to read.</summary>
public enum NoteReadStatus
{
    /// <summary>Read; <see cref="NoteRead.Text"/> is the body (possibly empty).</summary>
    Found,

    /// <summary>No such note: deleted, or an id this store never minted. Not the same as "empty".</summary>
    Missing,

    /// <summary>The note exists but its file could not be read this time (locked, permissions).</summary>
    Failed,
}

/// <summary>The outcome of reading one body. <see cref="Text"/> is empty unless <see cref="Status"/> is Found.</summary>
public readonly record struct NoteRead(NoteReadStatus Status, string Text)
{
    public static NoteRead Missing { get; } = new(NoteReadStatus.Missing, string.Empty);

    public static NoteRead Failed { get; } = new(NoteReadStatus.Failed, string.Empty);
}

/// <summary>What a mutation came to, other than a disk failure (those throw).</summary>
public enum NoteOutcome
{
    Done,

    /// <summary>The id is well formed but no note has it. Nothing was written: no resurrection.</summary>
    NotFound,

    /// <summary>The id is not one this store could have minted, so it never reaches a path.</summary>
    InvalidId,
}

/// <summary>
/// [S-1340] The notes kept on this PC: a note is a document the app owns, not a property of the widget
/// showing it.
/// <para>
/// <c>&lt;dir&gt;/&lt;id&gt;.md</c> holds each body byte for byte — greppable, backed up with
/// everything else, recoverable with a text editor — and <c>&lt;dir&gt;/index.json</c>
/// (<c>schemaVersion</c> + <c>{id, title, updatedAt}</c> rows) the metadata a picker needs. The split
/// means a corrupt index costs titles, never text: the index is rebuilt by adopting every
/// <c>&lt;id&gt;.md</c> on disk ([S-1343]).
/// </para>
/// <para>
/// [S-1341] Single writer and atomic: every file is written to a temp sibling and moved over the
/// target, and every read-modify-write of the index runs under one lock. The index is read once and
/// then kept in memory — this process is the only writer, so re-reading it per call (as the original,
/// with one writer per window, had to) would only add I/O.
/// </para>
/// <para>
/// Synchronous on purpose: the files are small, the logic is the part worth testing, and the one
/// caller (<c>NotePool</c>) runs every call off the UI thread in order. Disk failures throw
/// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> for that caller to log.
/// </para>
/// </summary>
public sealed class NoteStore
{
    public const int SchemaVersion = 1;

    public const string DirectoryName = "notes";

    public const string IndexFileName = "index.json";

    /// <summary>
    /// The title of a note the index lost track of, and of one given a blank title. Deliberately a
    /// disk-level fallback rather than a UI string.
    /// </summary>
    public const string Untitled = "Untitled";

    /// <summary>A pasted paragraph must not bloat the index or a picker row.</summary>
    public const int MaxTitle = 200;

    private const string Scope = "notes";

    private static long _tempSequence;

    private readonly Lock _gate = new();
    private readonly TimeProvider _clock;
    private List<NoteMeta>? _index;

    public NoteStore(string directory, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = directory;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Where the notes live.</summary>
    public string Directory { get; }

    private string IndexPath => Path.Combine(Directory, IndexFileName);

    /// <summary>
    /// <c>notes\</c> beside a state file, so a portable or test profile
    /// takes its notes with it.
    /// </summary>
    public static string DirectoryBeside(string stateFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateFile);
        return Path.Combine(Path.GetDirectoryName(stateFile) ?? ".", DirectoryName);
    }

    // -----------------------------------------------------------------------------------------
    // Rules (pure)
    // -----------------------------------------------------------------------------------------

    /// <summary>A fresh id: <see cref="Guid"/> in its 32-digit form.</summary>
    public static string NewId() => Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);

    /// <summary>
    /// [S-1342] The trust boundary for ids. An id reaches this store from a config file a user can
    /// edit, and is about to become a file name; checking its whole <i>shape</i> — exactly 32
    /// lowercase hex digits, the only thing <see cref="NewId"/> makes — rather than rejecting
    /// <c>..</c> is what keeps a path from ever leaving the notes folder. Lowercase only, so two
    /// spellings can never name one file on a case-insensitive disk.
    /// </summary>
    public static bool IsValidId(string? id)
    {
        if (id is null || id.Length != 32)
        {
            return false;
        }

        foreach (var c in id)
        {
            if (c is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// [S-1344] A title the index can hold: control characters (a pasted newline) become spaces,
    /// trimmed, capped at <see cref="MaxTitle"/>; blank becomes <see cref="Untitled"/>.
    /// </summary>
    public static string CleanTitle(string? title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return Untitled;
        }

        var flat = new StringBuilder(title.Length);
        foreach (var c in title)
        {
            flat.Append(char.IsControl(c) ? ' ' : c);
        }

        var trimmed = flat.ToString().Trim();
        if (trimmed.Length == 0)
        {
            return Untitled;
        }

        if (trimmed.Length > MaxTitle)
        {
            // Never split a surrogate pair at the cut.
            var cut = char.IsHighSurrogate(trimmed[MaxTitle - 1]) ? MaxTitle - 1 : MaxTitle;
            trimmed = trimmed[..cut].TrimEnd();
        }

        return trimmed;
    }

    /// <summary>
    /// [S-1344] The list order: title, case-insensitively, then id, so it is total and stable. Not by
    /// date — a picker that reordered itself under the pointer while a note was typed in would be
    /// unusable.
    /// </summary>
    public static List<NoteMeta> Sorted(IEnumerable<NoteMeta> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        var list = notes.ToList();
        list.Sort(static (a, b) =>
        {
            var byTitle = string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
            return byTitle != 0 ? byTitle : string.CompareOrdinal(a.Id, b.Id);
        });
        return list;
    }

    /// <summary>
    /// [S-1344] <paramref name="baseName"/>, or "<paramref name="baseName"/> 2", "… 3": the first
    /// that no note already has, so a desk of new notes is not five rows all called "Note".
    /// </summary>
    public static string UniqueTitle(IEnumerable<NoteMeta> notes, string baseName)
    {
        ArgumentNullException.ThrowIfNull(notes);

        var clean = CleanTitle(baseName);
        var taken = new HashSet<string>(notes.Select(n => n.Title), StringComparer.OrdinalIgnoreCase);

        if (!taken.Contains(clean))
        {
            return clean;
        }

        for (var n = 2; ; n++)
        {
            var candidate = CleanTitle(string.Create(CultureInfo.InvariantCulture, $"{clean} {n}"));
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// [S-1343] The index's trust boundary, on strings so it is tested without a disk. A refusal
    /// means the document as a whole cannot be used (not JSON, no or another schema version); rows
    /// that are malformed — a bad id, a duplicate, a missing title — are dropped and counted.
    /// </summary>
    public static (List<NoteMeta> Notes, int Dropped, string? Refusal) ParseIndex(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ([], 0, "the notes index is empty");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return ([], 0, "the notes index holds no document");
            }

            if (!root.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var schema))
            {
                return ([], 0, "the notes index has no schema version");
            }

            if (schema != SchemaVersion)
            {
                // A future shape is not half-read: its titles are not trusted, its bodies are adopted.
                return ([], 0, $"the notes index is schema v{schema}, not v{SchemaVersion}");
            }

            var notes = new List<NoteMeta>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var dropped = 0;

            if (root.TryGetProperty("notes", out var rows) && rows.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in rows.EnumerateArray())
                {
                    if (row.ValueKind == JsonValueKind.Object
                        && row.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
                        && idElement.GetString() is { } id && IsValidId(id) && seen.Add(id)
                        && row.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String)
                    {
                        var updated = row.TryGetProperty("updatedAt", out var stamp) && stamp.ValueKind == JsonValueKind.Number && stamp.TryGetInt64(out var seconds) && seconds >= 0
                            ? seconds
                            : 0;
                        notes.Add(new NoteMeta(id, CleanTitle(titleElement.GetString()), updated));
                    }
                    else
                    {
                        dropped++;
                    }
                }
            }

            return (notes, dropped, null);
        }
        catch (JsonException ex)
        {
            return ([], 0, $"the notes index is not valid JSON ({ex.Message})");
        }
    }

    /// <summary>The bytes the index is saved as.</summary>
    public static string SerializeIndex(IEnumerable<NoteMeta> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteStartArray("notes");

            foreach (var note in notes)
            {
                writer.WriteStartObject();
                writer.WriteString("id", note.Id);
                writer.WriteString("title", note.Title);
                writer.WriteNumber("updatedAt", note.UpdatedAt);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    // -----------------------------------------------------------------------------------------
    // Operations
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Every note, in list order. Never throws: an unreadable store is an empty list plus a log
    /// line, because a picker that errors is a dead end and an empty one still offers "New note".
    /// </summary>
    public IReadOnlyList<NoteMeta> List()
    {
        lock (_gate)
        {
            return Sorted(Index());
        }
    }

    /// <summary>
    /// [S-1345] One body. "Missing" is not "empty": a caller that could not tell them apart would
    /// happily save emptiness over a note that is gone.
    /// </summary>
    public NoteRead Read(string? id)
    {
        if (!IsValidId(id))
        {
            return NoteRead.Missing;
        }

        lock (_gate)
        {
            if (!Index().Exists(n => n.Id == id))
            {
                return NoteRead.Missing;
            }
        }

        try
        {
            return new NoteRead(NoteReadStatus.Found, File.ReadAllText(BodyPath(id!)));
        }
        catch (FileNotFoundException)
        {
            return NoteRead.Missing;
        }
        catch (DirectoryNotFoundException)
        {
            return NoteRead.Missing;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(Scope, $"could not read note {id}", ex);
            return NoteRead.Failed;
        }
    }

    /// <summary>
    /// [S-1342] Replace a body and stamp the note. A note that is not in the index is not written —
    /// an id that outlived its note in some widget's config would otherwise come back as an
    /// untitled orphan.
    /// </summary>
    public NoteOutcome Write(string? id, string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!IsValidId(id))
        {
            return NoteOutcome.InvalidId;
        }

        lock (_gate)
        {
            var index = Index();
            var at = index.FindIndex(n => n.Id == id);
            if (at < 0)
            {
                return NoteOutcome.NotFound;
            }

            // Body first: if the index write then fails, the text is safe and only the stamp is stale.
            // The other order would advertise a save that never landed.
            WriteAtomic(BodyPath(id!), content);
            index[at] = index[at] with { UpdatedAt = Now() };
            SaveIndex(index);
            return NoteOutcome.Done;
        }
    }

    /// <summary>Create a note under an id the caller minted (<see cref="NewId"/>). Null for a bad or taken id.</summary>
    public NoteMeta? Create(string? id, string? title, string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!IsValidId(id))
        {
            return null;
        }

        lock (_gate)
        {
            var index = Index();
            if (index.Exists(n => n.Id == id))
            {
                return null;
            }

            var meta = new NoteMeta(id!, CleanTitle(title), Now());
            WriteAtomic(BodyPath(id!), content);
            index.Add(meta);
            SaveIndex(index);
            return meta;
        }
    }

    public NoteOutcome Rename(string? id, string? title)
    {
        if (!IsValidId(id))
        {
            return NoteOutcome.InvalidId;
        }

        lock (_gate)
        {
            var index = Index();
            var at = index.FindIndex(n => n.Id == id);
            if (at < 0)
            {
                return NoteOutcome.NotFound;
            }

            index[at] = index[at] with { Title = CleanTitle(title) };
            SaveIndex(index);
            return NoteOutcome.Done;
        }
    }

    /// <summary>
    /// Delete the index row, then the body. In that order: a leftover body with no row is invisible
    /// and harmless, a leftover row with no body is a note the picker shows and nobody can open.
    /// </summary>
    public NoteOutcome Delete(string? id)
    {
        if (!IsValidId(id))
        {
            return NoteOutcome.InvalidId;
        }

        lock (_gate)
        {
            var index = Index();
            var at = index.FindIndex(n => n.Id == id);
            if (at < 0)
            {
                return NoteOutcome.NotFound;
            }

            index.RemoveAt(at);
            SaveIndex(index);

            // Already gone is the outcome asked for; File.Delete does not throw for a missing file.
            File.Delete(BodyPath(id!));
            return NoteOutcome.Done;
        }
    }

    // -----------------------------------------------------------------------------------------
    // Disk
    // -----------------------------------------------------------------------------------------

    private string BodyPath(string id) => Path.Combine(Directory, id + ".md");

    private long Now() => _clock.GetUtcNow().ToUnixTimeSeconds();

    /// <summary>The index, loaded on first use. Caller holds <see cref="_gate"/>.</summary>
    private List<NoteMeta> Index() => _index ??= LoadIndex();

    /// <summary>
    /// [S-1343] Read the index, telling apart the cases that matter: absent (first run, or an index
    /// deleted while notes remain — adopting the bodies covers both, silently); unusable (corrupt,
    /// another schema — set aside, bodies adopted, loudly); usable (rows whose body has gone are
    /// pruned, since a row with no file is a note nobody can open).
    /// </summary>
    private List<NoteMeta> LoadIndex()
    {
        string json;

        try
        {
            if (!File.Exists(IndexPath))
            {
                return Adopt(loud: false);
            }

            json = File.ReadAllText(IndexPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(Scope, "the notes index could not be read; the notes are recovered from their files", ex);
            return Adopt(loud: true);
        }

        var (notes, dropped, refusal) = ParseIndex(json);

        if (refusal is not null)
        {
            Log.Error(Scope, $"{refusal}; the notes are recovered from their files");
            SetAside();
            var adopted = Adopt(loud: true);

            // Written now, so the recovery happens once rather than on every start.
            TrySave(adopted);
            return adopted;
        }

        var kept = notes.FindAll(n => File.Exists(BodyPath(n.Id)));
        var pruned = notes.Count - kept.Count;

        if (pruned > 0 || dropped > 0)
        {
            Log.Warn(Scope, $"notes index: {pruned} row(s) without a body pruned, {dropped} malformed row(s) dropped");
            TrySave(kept);
        }

        return kept;
    }

    /// <summary>
    /// Every <c>&lt;id&gt;.md</c> in the folder, titled <see cref="Untitled"/>, stamped with its
    /// file time. The text is the part that cannot be replaced, so it is always recovered.
    /// </summary>
    private List<NoteMeta> Adopt(bool loud)
    {
        var notes = new List<NoteMeta>();

        try
        {
            if (!System.IO.Directory.Exists(Directory))
            {
                return notes;
            }

            foreach (var file in System.IO.Directory.EnumerateFiles(Directory, "*.md"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                if (!IsValidId(id))
                {
                    continue;
                }

                notes.Add(new NoteMeta(id, Untitled, new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds()));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(Scope, $"could not list {Directory}", ex);
        }

        if (loud && notes.Count > 0)
        {
            Log.Warn(Scope, $"recovered {notes.Count} note(s) from their files; their titles are reset");
        }

        return notes;
    }

    /// <summary>Keep the unusable index beside the new one: the titles in it are recoverable by hand.</summary>
    private void SetAside()
    {
        var aside = $"{IndexPath}.corrupt-{_clock.GetLocalNow():yyyyMMdd-HHmmss}";

        try
        {
            File.Move(IndexPath, aside, overwrite: true);
            Log.Warn(Scope, $"the unusable notes index is kept at {aside}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(Scope, "could not set the unusable notes index aside", ex);
        }
    }

    private void TrySave(List<NoteMeta> index)
    {
        try
        {
            SaveIndex(index);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The in-memory index is right; the next mutation writes it again.
            Log.Error(Scope, "could not save the repaired notes index", ex);
        }
    }

    private void SaveIndex(List<NoteMeta> index) => WriteAtomic(IndexPath, SerializeIndex(index));

    /// <summary>
    /// [S-1341] Write to a unique sibling, then move it over the target: a crash leaves the old file
    /// or the new one, never half of one. The temp name carries the process and a sequence number so
    /// two writes can never share it.
    /// </summary>
    private static void WriteAtomic(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        var temp = string.Create(
            CultureInfo.InvariantCulture,
            $"{path}.{Environment.ProcessId}.{Interlocked.Increment(ref _tempSequence)}.tmp");

        try
        {
            File.WriteAllText(temp, content);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, "could not remove a temp note file", ex);
        }
    }
}
