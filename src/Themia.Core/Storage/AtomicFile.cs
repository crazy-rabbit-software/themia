using Themia.Core.Diagnostics;

namespace Themia.Core.Storage;

/// <summary>
/// [S-40] [S-49] The last step of every atomic write: the finished temp sibling moved over the target.
/// Windows refuses that rename for a moment while another process holds the fresh file without
/// delete sharing (the search indexer, a virus scanner reading what was just written), as an access
/// or sharing error. Those two errors only are retried, a few times with a short doubling pause
/// (10, 20, 40, 80 ms, 150 ms at most), because the holder lets go on its own; this is a bounded
/// retry on one known transient error, not a wait for something to be ready. Any other error, and
/// the last attempt's, is the caller's, who handles it as before.
/// </summary>
public static class AtomicFile
{
    private const string Scope = "storage";

    /// <summary>How many times the rename is tried before its error is thrown.</summary>
    public const int Attempts = 5;

    private const int FirstPauseMs = 10;

    /// <summary>Move <paramref name="temp"/> over <paramref name="path"/>, retrying a transient refusal.</summary>
    public static void Replace(string temp, string path)
        => Replace(temp, path, static (from, to) => File.Move(from, to, overwrite: true), Thread.Sleep);

    /// <summary>The same, with the move and the pause given (tests simulate the refusal).</summary>
    internal static void Replace(string temp, string path, Action<string, string> move, Action<TimeSpan> pause)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(pause);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                move(temp, path);
                return;
            }
            catch (Exception ex) when (attempt < Attempts && IsTransient(ex))
            {
                Log.Warn(Scope, $"the rename over {Path.GetFileName(path)} was refused (attempt {attempt}); trying again", ex);
                pause(TimeSpan.FromMilliseconds(FirstPauseMs << (attempt - 1)));
            }
        }
    }

    /// <summary>Access denied, or a sharing or lock violation (ERROR_SHARING_VIOLATION 32, ERROR_LOCK_VIOLATION 33).</summary>
    private static bool IsTransient(Exception ex)
        => ex is UnauthorizedAccessException
            || (ex.GetType() == typeof(IOException) && (ex.HResult & 0xFFFF) is 32 or 33);
}
