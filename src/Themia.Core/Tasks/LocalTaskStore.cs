using System.Text;
using System.Text.Json;
using Themia.Core.Diagnostics;
using Themia.Core.Storage;

namespace Themia.Core.Tasks;

/// <summary>What a load of <c>tasks.json</c> came to.</summary>
/// <param name="Writable">
/// False when a file exists that could not be read or set aside: saving would replace the user's
/// lists with this session's empty pool, so the caller must not.
/// </param>
public sealed record LocalTaskLoad(IReadOnlyList<LocalTaskList> Lists, LoadOutcome Outcome, string? Detail, bool Writable = true);

/// <summary>
/// [S-603] The lists kept on this PC, in <c>tasks.json</c> beside the app state.
/// <para>
/// Its own file rather than the state file: the tasks are the user's data, not layout, so copying a
/// widget, exporting a layout or resetting one must not carry or lose them. A <c>schemaVersion</c>
/// envelope; every entry validated through <see cref="LocalTaskLists.Parse"/>. A file that cannot be
/// trusted is moved aside, never deleted or overwritten; a newer schema is set aside rather than
/// half-read; absent is a quiet empty pool. Saves are atomic (temp file, then a move) and go through
/// one gate, so two writes cannot interleave. The caller owns the read-modify-write.
/// </para>
/// </summary>
public sealed class LocalTaskStore
{
    public const int SchemaVersion = 1;

    public const string FileName = "tasks.json";

    private const string Scope = "tasks";

    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public LocalTaskStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
    }

    public string Path { get; }

    /// <summary><c>tasks.json</c> beside a state file, so a portable or test profile takes its lists with it.</summary>
    public static string PathBeside(string stateFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateFile);
        return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(stateFile) ?? ".", FileName);
    }

    /// <summary>Read and validate. Never throws for bad data.</summary>
    public LocalTaskLoad Load()
    {
        string json;
        try
        {
            if (!File.Exists(Path))
            {
                return new LocalTaskLoad([], LoadOutcome.Fresh, null);
            }

            json = File.ReadAllText(Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable is not corrupt: the file may be fine and merely locked. Left in place, and
            // nothing is written over it this session.
            Log.Error(Scope, "the task lists could not be read; this PC's lists are read-only until Themia restarts", ex);
            return new LocalTaskLoad([], LoadOutcome.Recovered, "the task lists file could not be read", Writable: false);
        }

        var parsed = Parse(json);

        if (parsed.Refusal is { } refusal)
        {
            return Recover(refusal);
        }

        if (parsed.Dropped > 0)
        {
            Log.Warn(Scope, $"task lists loaded; {parsed.Dropped} malformed entr{(parsed.Dropped == 1 ? "y was" : "ies were")} dropped");
        }

        return new LocalTaskLoad(parsed.Lists, LoadOutcome.Loaded, parsed.Dropped > 0 ? $"{parsed.Dropped} malformed entries dropped" : null);
    }

    /// <summary>
    /// The trust boundary, separate from the disk so it is tested on strings. A refusal is a reason
    /// the whole document cannot be used; a dropped count is entries left out of a usable one.
    /// </summary>
    public static (IReadOnlyList<LocalTaskList> Lists, int Dropped, string? Refusal) Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ([], 0, "the task lists file is empty");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return ([], 0, "the task lists file holds no document");
            }

            if (!root.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32(out var schema) || schema < 1)
            {
                return ([], 0, "the task lists file has no schema version");
            }

            if (schema > SchemaVersion)
            {
                return ([], 0, $"the task lists file is schema v{schema}, newer than this build");
            }

            var dropped = 0;
            var lists = root.TryGetProperty("lists", out var raw) ? LocalTaskLists.Parse(raw, ref dropped) : [];
            return (lists, dropped, null);
        }
        catch (JsonException ex)
        {
            return ([], 0, $"the task lists file is not valid JSON ({ex.Message})");
        }
    }

    /// <summary>The bytes the pool is saved as.</summary>
    public static string Serialize(IReadOnlyList<LocalTaskList> lists)
    {
        ArgumentNullException.ThrowIfNull(lists);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteStartArray("lists");

            foreach (var list in lists)
            {
                writer.WriteStartObject();
                writer.WriteString("id", list.Id);
                writer.WriteString("name", list.Name);
                writer.WriteStartArray("tasks");

                foreach (var task in list.Tasks)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", task.Id);
                    writer.WriteString("title", task.Title);
                    writer.WriteBoolean("done", task.Done);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Write the whole pool, atomically. Throws on failure so the caller can log it.</summary>
    public async Task SaveAsync(IReadOnlyList<LocalTaskList> lists, CancellationToken cancellation = default)
    {
        var json = Serialize(lists);

        await _writeGate.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = $"{Path}.{Environment.ProcessId}.tmp";
            try
            {
                await File.WriteAllTextAsync(temp, json, CancellationToken.None).ConfigureAwait(false);
                Themia.Core.Storage.AtomicFile.Replace(temp, Path);
            }
            catch
            {
                TryDelete(temp);
                throw;
            }
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private LocalTaskLoad Recover(string reason)
    {
        Log.Error(Scope, $"{reason}; starting with no lists on this PC");

        var aside = $"{Path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
        try
        {
            File.Move(Path, aside, overwrite: true);
            Log.Warn(Scope, $"the previous task lists are kept at {aside}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still in place, so a save would write over it: read-only instead.
            Log.Error(Scope, "could not set the unreadable task lists aside", ex);
            return new LocalTaskLoad([], LoadOutcome.Recovered, reason, Writable: false);
        }

        return new LocalTaskLoad([], LoadOutcome.Recovered, reason);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, "could not remove the temp task lists file", ex);
        }
    }
}
