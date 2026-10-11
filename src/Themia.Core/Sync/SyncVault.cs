using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Themia.Core.Diagnostics;

namespace Themia.Core.Sync;

/// <summary>[S-2604] What a shelf holds: one data kind for one owner (an account, or the web caches).</summary>
public enum SyncKind
{
    Mail,
    Calendar,
    Contacts,
    Tasks,
    Web,
}

/// <summary>Every shelf document carries its schema and whose it is, checked on the way in ([S-2605]).</summary>
public interface ISyncShelf
{
    int SchemaVersion { get; }

    string Owner { get; }
}

/// <summary>How a shelf came off disk.</summary>
public enum ShelfReadOutcome
{
    /// <summary>No file: nothing synced yet. Not a problem; the first sync fills it.</summary>
    Absent,

    Loaded,

    /// <summary>
    /// A file that could not be used (damaged, another key, another schema, another owner, too
    /// large, unreadable). Logged and set aside; the owner starts empty and resyncs.
    /// </summary>
    Discarded,
}

/// <param name="Value">The shelf, or null unless <see cref="Outcome"/> is <see cref="ShelfReadOutcome.Loaded"/>.</param>
/// <param name="Detail">Why it was discarded, for the log; never shown.</param>
public readonly record struct ShelfRead<T>(T? Value, ShelfReadOutcome Outcome, string? Detail)
    where T : class;

/// <summary>
/// [S-2600] The offline sync store: what the account and web widgets show, kept on this PC so a view
/// switch, a card rebuild, a restart or a lost network draws it at once without asking anybody.
/// <para>
/// One file ("shelf") per data kind per owner under <c>sync\</c> in the app's local data folder
/// ([S-2601]), sealed with <see cref="SealedBlob"/> ([S-2602]). The file name is a hash of the kind
/// and owner, so an account's address is not written into a directory listing.
/// </para>
/// <para>
/// <b>Reads</b> validate ([S-2605]): absent is a fresh owner; anything that cannot be trusted is moved
/// aside as <c>.corrupt</c> (one copy kept) and the owner resyncs. A cache is never repaired into a
/// half-shelf at this level; the shelf's own validator drops bad entries afterwards.
/// </para>
/// <para>
/// <b>Writes</b> ([S-2606]) are queued per shelf, the newest snapshot winning, and drained by one
/// background writer: serialise, seal, write a sibling temp file, move it over the shelf. A purge
/// ([S-2607]) goes through the same queue, so a write already out lands before the delete, never
/// after it. A detached vault (debug renders, tests of the services) holds nothing and writes nothing.
/// </para>
/// </summary>
public sealed class SyncVault
{
    public const string FolderName = "sync";

    /// <summary>A shelf larger than this is not one this app wrote (the bounds per kind keep them far smaller).</summary>
    public const long MaxShelfBytes = 64L * 1024 * 1024;

    private const string Scope = "sync-store";

    private readonly string? _directory;
    private readonly byte[]? _key;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Action> _queue = new(StringComparer.OrdinalIgnoreCase);
    private Task _drain = Task.CompletedTask;
    private bool _draining;

    /// <param name="directory">The <c>sync</c> folder.</param>
    /// <param name="key">The <see cref="SealedBlob.KeySize"/>-byte key, already opened.</param>
    public SyncVault(string directory, byte[] key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length != SealedBlob.KeySize)
        {
            throw new ArgumentException($"a shelf key is {SealedBlob.KeySize} bytes", nameof(key));
        }

        _directory = directory;
        _key = key;
    }

    private SyncVault()
    {
    }

    /// <summary>A vault that reads nothing and writes nothing: every load is absent.</summary>
    public static SyncVault Detached() => new();

    public bool Persists => _directory is not null;

    public string? Directory => _directory;

    /// <summary>The file a shelf lives in. Stable across runs; the owner is hashed, never written as is.</summary>
    public string? ShelfPath(SyncKind kind, string owner)
        => _directory is null ? null : Path.Combine(_directory, ShelfName(kind, owner));

    public static string ShelfName(SyncKind kind, string owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{kind}|{owner}"));
        return $"{KindName(kind)}-{Convert.ToHexStringLower(hash.AsSpan(0, 16))}.shelf";
    }

    private static string KindName(SyncKind kind) => kind switch
    {
        SyncKind.Mail => "mail",
        SyncKind.Calendar => "calendar",
        SyncKind.Contacts => "contacts",
        SyncKind.Tasks => "tasks",
        _ => "web",
    };

    private static string Context(SyncKind kind, string owner) => $"{KindName(kind)}|{owner}";

    /// <summary>Read one shelf. Never throws for bad data.</summary>
    public ShelfRead<T> Load<T>(SyncKind kind, string owner, JsonTypeInfo<T> type, int schemaVersion)
        where T : class, ISyncShelf
    {
        ArgumentNullException.ThrowIfNull(type);

        if (ShelfPath(kind, owner) is not { } path || _key is null)
        {
            return new ShelfRead<T>(null, ShelfReadOutcome.Absent, null);
        }

        byte[] blob;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return new ShelfRead<T>(null, ShelfReadOutcome.Absent, null);
            }

            if (info.Length > MaxShelfBytes)
            {
                return Discard<T>(path, kind, $"the shelf is {info.Length} bytes, more than any this app writes");
            }

            blob = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable is not corrupt: left where it is. The owner starts empty this run and the
            // next save replaces it, which for a cache is the right outcome.
            Log.Warn(Scope, $"could not read the {KindName(kind)} shelf; resyncing", ex);
            return new ShelfRead<T>(null, ShelfReadOutcome.Discarded, ex.Message);
        }

        var outcome = SealedBlob.TryOpen(_key, blob, Context(kind, owner), out var plain);
        if (outcome != SealOutcome.Opened)
        {
            return Discard<T>(path, kind, outcome == SealOutcome.Tampered
                ? "the shelf does not open with this PC's key (damaged, or sealed elsewhere)"
                : "the file is not a sealed shelf");
        }

        T? value;
        try
        {
            value = JsonSerializer.Deserialize(plain, type);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Discard<T>(path, kind, $"the shelf is not valid JSON ({ex.Message})");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }

        if (value is null)
        {
            return Discard<T>(path, kind, "the shelf holds no document");
        }

        if (value.SchemaVersion != schemaVersion)
        {
            return Discard<T>(path, kind, $"the shelf is schema v{value.SchemaVersion}, this build reads v{schemaVersion}");
        }

        if (!string.Equals(value.Owner, owner, StringComparison.Ordinal))
        {
            return Discard<T>(path, kind, "the shelf belongs to another owner");
        }

        return new ShelfRead<T>(value, ShelfReadOutcome.Loaded, null);
    }

    /// <summary>
    /// Queue a write of <paramref name="shelf"/>. The object is handed over: the caller must not
    /// change it afterwards (build a new one per save). A newer save of the same shelf replaces one
    /// still queued.
    /// </summary>
    public void SaveSoon<T>(SyncKind kind, string owner, T shelf, JsonTypeInfo<T> type)
        where T : class, ISyncShelf
    {
        ArgumentNullException.ThrowIfNull(shelf);
        ArgumentNullException.ThrowIfNull(type);

        if (ShelfPath(kind, owner) is not { } path || _key is null)
        {
            return;
        }

        var key = _key;
        var context = Context(kind, owner);
        Enqueue(path, () => Write(path, key, context, JsonSerializer.SerializeToUtf8Bytes(shelf, type)));
    }

    /// <summary>
    /// Queue a write whose document is built on the writer's thread: for a shelf that is costly to
    /// build (the web caches), so the UI thread only queues. <paramref name="build"/> must read only
    /// immutable data.
    /// </summary>
    public void SaveSoon<T>(SyncKind kind, string owner, Func<T> build, JsonTypeInfo<T> type)
        where T : class, ISyncShelf
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(type);

        if (ShelfPath(kind, owner) is not { } path || _key is null)
        {
            return;
        }

        var key = _key;
        var context = Context(kind, owner);
        Enqueue(path, () => Write(path, key, context, JsonSerializer.SerializeToUtf8Bytes(build(), type)));
    }

    /// <summary>
    /// [S-2607] Forget everything held for <paramref name="owner"/>: every kind's shelf and its set-aside
    /// copy. Queued behind a write already out, and replacing any write still queued.
    /// </summary>
    public void Purge(string owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (_directory is null)
        {
            return;
        }

        foreach (var kind in Enum.GetValues<SyncKind>())
        {
            var path = ShelfPath(kind, owner)!;
            Enqueue(path, () =>
            {
                Delete(path);
                Delete(path + ".corrupt");
            });
        }

        Log.Info(Scope, "an owner's synced data was purged");
    }

    /// <summary>Wait until every queued write and purge has landed (exit, tests).</summary>
    public async Task FlushAsync()
    {
        while (true)
        {
            Task drain;
            lock (_gate)
            {
                if (!_draining && _queue.Count == 0)
                {
                    return;
                }

                drain = _drain;
            }

            await drain.ConfigureAwait(false);
        }
    }

    private void Enqueue(string path, Action operation)
    {
        lock (_gate)
        {
            _queue[path] = operation;

            if (_draining)
            {
                return;
            }

            _draining = true;
            _drain = Task.Run(Drain);
        }
    }

    private void Drain()
    {
        while (true)
        {
            Action operation;
            lock (_gate)
            {
                if (_queue.Count == 0)
                {
                    _draining = false;
                    return;
                }

                var (path, next) = _queue.First();
                _queue.Remove(path);
                operation = next;
            }

            try
            {
                operation();
            }
            catch (Exception ex)
            {
                // Each operation handles the I/O failures it expects; anything here is a bug. The
                // writer carries on with the next shelf rather than stopping every later save.
                Log.Error(Scope, "a shelf write threw", ex);
            }
        }
    }

    private static void Write(string path, byte[] key, string context, byte[] json)
    {
        byte[] sealedBytes;
        try
        {
            sealedBytes = SealedBlob.Seal(key, json, context);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }

        var temp = $"{path}.{Environment.ProcessId}.tmp";
        try
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(temp, sealedBytes);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The previous shelf is still whole (the move is the commit). The next sync saves again.
            Log.Warn(Scope, "could not save a shelf; the previous copy stays", ex);
            Delete(temp);
        }
    }

    private ShelfRead<T> Discard<T>(string path, SyncKind kind, string reason)
        where T : class
    {
        Log.Warn(Scope, $"the {KindName(kind)} shelf could not be used ({reason}); set aside, resyncing");

        try
        {
            File.Move(path, path + ".corrupt", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, "could not set the unusable shelf aside; the next save replaces it", ex);
        }

        return new ShelfRead<T>(null, ShelfReadOutcome.Discarded, reason);
    }

    private static void Delete(string path)
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
            Log.Warn(Scope, "could not delete a shelf file", ex);
        }
    }
}
