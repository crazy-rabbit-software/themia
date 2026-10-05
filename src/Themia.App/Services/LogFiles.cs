using System.Text;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Storage;

namespace Themia.App.Services;

/// <summary>
/// [S-980] The log's sinks: the rotating files under <c>%LOCALAPPDATA%\Themia\logs</c> and the
/// event log mirror ([S-986]).
/// <para>
/// Local, not roaming app data: a log describes this machine, and a roaming profile should not
/// carry 20 MB of it from desk to desk. The uninstall's data purge already covers the local
/// folder.
/// </para>
/// <para>
/// One writer: a lock around one open stream, flushed after every record so the line before a
/// crash is on disk. The stream is shared for reading and deleting, so the user can open the
/// file in an editor, and rotation can rename it, while it is being written.
/// </para>
/// </summary>
internal static class LogFiles
{
    private static readonly Lock Gate = new();
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static FileStream? _stream;
    private static bool _broken;

    /// <summary>The log folder.</summary>
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppStateStore.FolderName,
        "logs");

    /// <summary>The file being written now.</summary>
    public static string CurrentFile => Path.Combine(Folder, LogFileRules.CurrentName);

    /// <summary>
    /// The sink every process gets — the debugger — and the floor. The debug renders
    /// stop here: they must never write into the user's log folder while the user's own copy
    /// is running.
    /// </summary>
    public static void InstallBasics()
    {
        Log.Floor = LogPolicy.Floor(IsDebugBuild);
        Log.AddSink(static record => System.Diagnostics.Debug.WriteLine(LogFormat.Record(record)));
    }

    /// <summary>
    /// The files and the event log mirror, for the real app. Sweeps archives left past their
    /// count or age by earlier sessions first ([S-985]), so a machine that was off for a month
    /// does not keep a month-old log until the next rotation.
    /// </summary>
    public static void InstallFiles()
    {
        Sweep();
        Log.AddSink(static record =>
        {
            Write(record);
            EventLogSink.Report(record);
        });
    }

    private static bool IsDebugBuild =>
#if DEBUG
        true;
#else
        false;
#endif

    private static void Write(LogRecord record)
    {
        var bytes = Utf8.GetBytes(LogFormat.Record(record) + Environment.NewLine);

        lock (Gate)
        {
            if (_broken)
            {
                return;
            }

            try
            {
                _stream ??= Open();

                if (LogFileRules.ShouldRotate(_stream.Length, bytes.Length))
                {
                    Rotate();
                }

                _stream.Write(bytes);
                _stream.Flush();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Cannot log a failure of the log. Stop trying for this session rather than retry —
                // and throw — on every record of a full or read-only disk; the debugger still hears.
                _broken = true;
                _stream?.Dispose();
                _stream = null;
                System.Diagnostics.Debug.WriteLine($"[themia] log file disabled for this session: {ex.Message}");
            }
        }
    }

    private static FileStream Open()
    {
        Directory.CreateDirectory(Folder);
        return new FileStream(CurrentFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
    }

    /// <summary>Under <see cref="Gate"/>: the current file becomes an archive and a fresh one starts.</summary>
    private static void Rotate()
    {
        _stream?.Dispose();
        _stream = null;

        var now = DateTimeOffset.Now;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var archive = Path.Combine(Folder, LogFileRules.ArchiveName(now, attempt));
            if (File.Exists(archive))
            {
                continue;
            }

            File.Move(CurrentFile, archive);
            break;
        }

        _stream = Open();
        Sweep();
    }

    /// <summary>[S-985] Delete the archives past their count or age. Best-effort: a boot is not failed over it.</summary>
    private static void Sweep()
    {
        try
        {
            var directory = new DirectoryInfo(Folder);
            if (!directory.Exists)
            {
                return;
            }

            var segments = directory
                .GetFiles("themia-*.log")
                .Select(f => new LogSegment(f.Name, f.Length, f.LastWriteTimeUtc))
                .ToList();

            foreach (var name in LogFileRules.ToDelete(segments, DateTimeOffset.UtcNow))
            {
                try
                {
                    File.Delete(Path.Combine(Folder, name));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // ignore: an archive another process still holds goes on the next sweep.
                    System.Diagnostics.Debug.WriteLine($"[themia] could not delete {name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // ignore: a folder that cannot be listed is not worth failing a start over; the writer
            // reports its own trouble.
            System.Diagnostics.Debug.WriteLine($"[themia] could not sweep the log folder: {ex.Message}");
        }
    }
}
