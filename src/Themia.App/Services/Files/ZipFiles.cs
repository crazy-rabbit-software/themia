using System.IO.Compression;
using Themia.Core.Diagnostics;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-6419] "Compress to ZIP file", as Explorer's: the items into one new ZIP beside them, folders
/// with everything in them (hidden files included), on the thread pool. A ZIP that fails half way is
/// this command's own output and is removed; nothing the user had is ever touched.
/// </summary>
internal static class ZipFiles
{
    private const string Scope = "zip";

    /// <summary>Write <paramref name="zipPath"/> (which must not exist) holding <paramref name="paths"/>. The path on success, null (logged) on failure.</summary>
    public static Task<string?> CompressAsync(IReadOnlyList<string> paths, string zipPath)
        => Task.Run(() => Compress(paths, zipPath));

    private static string? Compress(IReadOnlyList<string> paths, string zipPath)
    {
        var created = false;

        try
        {
            using (var stream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

                foreach (var path in paths)
                {
                    if (Directory.Exists(path))
                    {
                        AddFolder(archive, path, Path.GetFileName(Path.TrimEndingDirectorySeparator(path)));
                    }
                    else
                    {
                        archive.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Optimal);
                    }
                }
            }

            ShellFileOps.Notify([Path.GetDirectoryName(zipPath) ?? zipPath]);
            return zipPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidDataException)
        {
            Log.Warn(Scope, $"the ZIP of {paths.Count} item(s) could not be written", ex);

            if (created)
            {
                try
                {
                    File.Delete(zipPath);
                }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
                {
                    Log.Warn(Scope, "the unfinished ZIP could not be removed", cleanup);
                }
            }

            return null;
        }
    }

    /// <summary>A folder and everything under it, under <paramref name="root"/> in the archive; an empty folder keeps an entry of its own.</summary>
    private static void AddFolder(ZipArchive archive, string folder, string root)
    {
        // Hidden and system files too, as Explorer's ZIP has them; directory links are not followed.
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = false };
        var any = false;

        foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", options))
        {
            any = true;
            var name = root + "/" + Path.GetRelativePath(folder, entry.FullName).Replace('\\', '/');

            if (entry is DirectoryInfo directory)
            {
                if (!directory.EnumerateFileSystemInfos("*", new EnumerationOptions { AttributesToSkip = 0 }).Any())
                {
                    archive.CreateEntry(name + "/");
                }
            }
            else
            {
                archive.CreateEntryFromFile(entry.FullName, name, CompressionLevel.Optimal);
            }
        }

        if (!any)
        {
            archive.CreateEntry(root + "/");
        }
    }
}
