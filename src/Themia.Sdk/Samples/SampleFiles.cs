#if DEBUG
using Themia.Core.Diagnostics;

namespace Themia.Sdk.Samples;

/// <summary>One invented file (or folder) of the sample day: its name, its size, how long ago it changed, and its bytes when they matter (a picture).</summary>
public sealed record SampleFile(string Name, long Size, double HoursAgo, bool IsFolder = false, byte[]? Bytes = null);

/// <summary>
/// Debug renders only ([S-2056]): Robin's documents, written as files under the temp folder
/// (<c>%TEMP%\themia-sample\&lt;folder&gt;</c>) so the shell draws its stock icon for each extension
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
    /// Write <paramref name="files"/> into <c>%TEMP%\themia-sample\<paramref name="folder"/></c> (emptied first), each
    /// changed at its time before the sample moment, and return the folder's path; null (logged) when it could not.
    /// </summary>
    public static string? Write(string folder, IReadOnlyList<SampleFile> files)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(files);
        var path = Path.Combine(Path.GetTempPath(), "themia-sample", folder);

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

            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, $"the sample folder {folder} could not be written", ex);
            return null;
        }
    }
}
#endif
