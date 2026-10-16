using System.Text.Json;
using Themia.Core.Diagnostics;
using Themia.Core.Storage;

namespace Themia.Core.Accounts;

/// <summary>What a load of <c>accounts.json</c> came to.</summary>
/// <param name="Accounts">The valid accounts, in file order.</param>
/// <param name="Outcome">Fresh (no file), Loaded, or Recovered (unreadable; set aside).</param>
/// <param name="Detail">The repairs made, or why the file could not be read.</param>
/// <param name="Writable">
/// False when a file exists that could not even be read (locked, no permission). Saving over it
/// would replace the user's accounts with this session's empty list, so the caller must not.
/// </param>
public sealed record AccountLoad(IReadOnlyList<Account> Accounts, LoadOutcome Outcome, string? Detail, bool Writable = true);

/// <summary>
/// [S-524] <c>accounts.json</c> on disk: atomic and single-writer.
/// <para>
/// A save goes to a temp file beside the target and is moved over it, so a crash mid-write leaves
/// the old file or the new one, never half of one. Saves are serialised through one gate, so two
/// writers — a token refresh and a rename landing together — cannot interleave. The caller still
/// owns the read-modify-write (it holds the authoritative list); this only promises that what it
/// hands over arrives whole.
/// </para>
/// <para>
/// A file that cannot be trusted is moved aside, never deleted or overwritten in place: it holds
/// someone's sign-ins, and an empty list on screen is recoverable where a lost file is not.
/// </para>
/// </summary>
public sealed class AccountStore
{
    private const string Scope = "accounts";

    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public AccountStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
    }

    public string Path { get; }

    /// <summary><c>accounts.json</c> beside a state file.</summary>
    public static string PathBeside(string stateFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateFile);
        return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(stateFile) ?? ".", "accounts.json");
    }

    /// <summary>Read and validate. Never throws for bad data.</summary>
    public AccountLoad Load(DateTimeOffset now)
    {
        string json;
        try
        {
            if (!File.Exists(Path))
            {
                return new AccountLoad([], LoadOutcome.Fresh, null);
            }

            json = File.ReadAllText(Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not set aside: the file may be perfectly good and merely locked for a moment.
            Log.Error(Scope, "accounts file could not be read; starting with no accounts this session", ex);
            return new AccountLoad([], LoadOutcome.Recovered, "the accounts file could not be read", Writable: false);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return Recover("the accounts file is empty", null);
        }

        AccountDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(json, AccountsJsonContext.Default.AccountDocument);
        }
        catch (JsonException ex)
        {
            return Recover("the accounts file is not valid JSON", ex);
        }

        if (document is null)
        {
            return Recover("the accounts file holds no document", null);
        }

        if (document.SchemaVersion > AccountDocument.CurrentSchemaVersion)
        {
            return Recover($"the accounts file is schema v{document.SchemaVersion}, newer than this build", null);
        }

        var repairs = new List<string>();
        var accounts = AccountRules.Validate(document, now, repairs);

        if (repairs.Count > 0)
        {
            Log.Warn(Scope, $"accounts loaded with {repairs.Count} repair(s): {string.Join("; ", repairs)}");
        }

        return new AccountLoad(accounts, LoadOutcome.Loaded, repairs.Count > 0 ? string.Join("; ", repairs) : null);
    }

    /// <summary>Write the whole list, atomically. Throws on failure so the caller can say so.</summary>
    public async Task SaveAsync(IReadOnlyList<Account> accounts, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        var json = JsonSerializer.Serialize(AccountRules.ToDocument(accounts), AccountsJsonContext.Default.AccountDocument);

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

    private AccountLoad Recover(string reason, Exception? error)
    {
        Log.Error(Scope, $"{reason}; starting with no accounts", error);

        var aside = $"{Path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
        try
        {
            File.Move(Path, aside, overwrite: true);
            Log.Warn(Scope, $"the previous accounts file is kept at {aside}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still in place, so a save would write over it: say the store is read-only instead.
            Log.Error(Scope, "could not set the unreadable accounts file aside", ex);
            return new AccountLoad([], LoadOutcome.Recovered, reason, Writable: false);
        }

        return new AccountLoad([], LoadOutcome.Recovered, reason);
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
            Log.Warn(Scope, "could not remove the temp accounts file", ex);
        }
    }
}
