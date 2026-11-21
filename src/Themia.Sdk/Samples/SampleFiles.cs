using Themia.Core.Diagnostics;

namespace Themia.Sdk.Samples;

/// <summary>One invented file (or folder) of the sample day: its name, its size, how long ago it changed, and its bytes when they matter (a picture).</summary>
public sealed record SampleFile(string Name, long Size, double HoursAgo, bool IsFolder = false, byte[]? Bytes = null);

/// <summary>
/// The sample day ([S-11850], [S-2056]): Robin's documents, written as files under the temp folder
/// (<c>%TEMP%\themia-sample\&lt;process id&gt;\&lt;folder&gt;</c>, [S-15163]) so the shell draws its stock icon for each extension
/// and a widget lists them as it lists any folder, while no name, path or picture of this PC shows.
/// The files hold zeros of their size; nothing else is ever read or written.
/// </summary>
public static class SampleFiles
{
    private const string Scope = "sample-files";

    /// <summary>The documents folder the folder widget's sample lists.</summary>
    public static IReadOnlyList<SampleFile> Documents { get; } =
    [
        new("Onboarding", 0, 1.2, IsFolder: true),
        new("Northwind", 0, 26, IsFolder: true),
        new("Receipts", 0, 24 * 6, IsFolder: true),
        new("Onboarding flow v3.pptx", 4_812_544, 0.3),
        new("Quarterly numbers Q3.xlsx", 86_016, 0.4),
        new("1-1 notes Sam.docx", 24_576, 0.9),
        new("Northwind contract draft.pdf", 312_320, 2),
        new("Budget 2027.xlsx", 61_440, 3.5),
        new("Whiteboard sketch.jpg", 1_843_200, 5),
        new("Release notes 4.2.docx", 33_792, 18),
        new("Lisbon offsite agenda.pdf", 145_408, 22),
        new("Store listing copy.txt", 3_072, 26),
        new("Team photo.png", 2_457_600, 24 * 3),
        new("Meeting notes.md", 5_120, 24 * 4),
        new("Expenses October.xlsx", 40_960, 24 * 9),
    ];

    /// <summary>
    /// Write <paramref name="files"/> into <c>%TEMP%\themia-sample\&lt;process id&gt;\<paramref name="folder"/></c> (emptied
    /// first), each changed at its time before the sample moment, and return the folder's path; null (logged) when it could
    /// not. [S-15163] The folder is this process's own, and written once while its files stay the same: a render draws
    /// several cards of the folder kind one after another, and parallel renders draw at once, each card listing (and
    /// watching) the folder while the next one wrote it again; the delete under a listing failed or emptied it, and the
    /// card kept the real profile folder (the user's own names, an empty label) or listed a part of the documents. A
    /// process's first write sweeps the folders of processes that are gone.
    /// </summary>
    public static string? Write(string folder, IReadOnlyList<SampleFile> files)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(files);
        var path = Path.Combine(Root, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), folder);

        lock (Written)
        {
            if (Written.TryGetValue(path, out var held) && Same(held, files) && Directory.Exists(path))
            {
                return path;
            }

            Written.Remove(path);
            if (!_swept)
            {
                _swept = true;
                SweepGone();
            }

            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                Directory.CreateDirectory(path);

                foreach (var file in files)
                {
                    var target = Path.Combine(path, file.Name);
                    var changed = SampleDay.Now.AddHours(-file.HoursAgo).UtcDateTime;

                    if (file.IsFolder)
                    {
                        Directory.CreateDirectory(target);
                        Directory.SetLastWriteTimeUtc(target, changed);
                        continue;
                    }

                    using (var stream = new FileStream(target, FileMode.Create, FileAccess.Write))
                    {
                        if (file.Bytes is { } bytes)
                        {
                            stream.Write(bytes);
                        }
                        else
                        {
                            stream.SetLength(file.Size);
                        }
                    }

                    File.SetLastWriteTimeUtc(target, changed);
                }

                Written[path] = [.. files];
                return path;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn(Scope, $"the sample folder {folder} could not be written", ex);
                return null;
            }
        }
    }

    /// <summary>Where every process's sample folders stand: <c>%TEMP%\themia-sample</c>.</summary>
    private static string Root => Path.Combine(Path.GetTempPath(), "themia-sample");

    /// <summary>[S-15163] The folders this process wrote, by path, with what each holds.</summary>
    private static readonly Dictionary<string, SampleFile[]> Written = new(StringComparer.OrdinalIgnoreCase);

    private static bool _swept;

    /// <summary>[S-15163] Whether two writes hold the same files: names, sizes, ages, kinds and bytes.</summary>
    internal static bool Same(IReadOnlyList<SampleFile> a, IReadOnlyList<SampleFile> b)
        => a.Count == b.Count && a.Zip(b).All(p => p.First.Name == p.Second.Name && p.First.Size == p.Second.Size
            && p.First.HoursAgo.Equals(p.Second.HoursAgo) && p.First.IsFolder == p.Second.IsFolder
            && (p.First.Bytes ?? []).AsSpan().SequenceEqual(p.Second.Bytes ?? []));

    /// <summary>[S-15163] Delete the sample folders of processes no longer running (named by their id); one in use stays, logged.</summary>
    private static void SweepGone()
    {
        string[] folders;
        try
        {
            folders = Directory.Exists(Root) ? Directory.GetDirectories(Root) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, "the sample folders could not be listed", ex);
            return;
        }

        foreach (var folder in folders)
        {
            if (!int.TryParse(Path.GetFileName(folder), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id)
                || id == Environment.ProcessId || Running(id))
            {
                continue;
            }

            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn(Scope, $"the sample folder of gone process {id} could not be deleted", ex);
            }
        }
    }

    private static bool Running(int id)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // ignore: no process has that id any more, which is the answer.
            return false;
        }
        catch (InvalidOperationException)
        {
            // ignore: it exited while it was asked, which is the answer.
            return false;
        }
    }
}
