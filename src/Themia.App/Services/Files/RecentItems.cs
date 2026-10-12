using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-4841] Windows' Recent Items (<c>shell:Recent</c>, <see cref="Environment.SpecialFolder.Recent"/>):
/// the shortcuts Windows writes on every file open and Explorer's Home "Recent" is built from, read
/// into the files they point at. One process-wide list for every Recent files widget.
/// <para>
/// [S-4843] Event-driven: one <see cref="FolderWatch"/> on the folder while any widget listens,
/// started with the first and disposed with the last. A read runs on the thread pool (resolving a
/// shortcut is COM and touches the disk; checking a target on a share can wait on the network) and
/// is single-flight: a change during a read reads once more after it. A read that fails keeps the
/// last list. UI thread.
/// </para>
/// </summary>
internal static class RecentItems
{
    private const string Scope = "recent-items";

    private static readonly List<Action> Listeners = [];
    private static FolderWatch? _watch;
    private static bool _reading;
    private static bool _again;

    /// <summary>The list, newest first; null before the first read.</summary>
    public static IReadOnlyList<RecentFile>? Current { get; private set; }

    /// <summary>The last read failed: the folder could not be listed.</summary>
    public static bool Unreadable { get; private set; }

    /// <summary>Where Windows keeps the shortcuts, or null on a machine without one.</summary>
    public static string? Folder
    {
        get
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            return string.IsNullOrEmpty(path) ? null : path;
        }
    }

    /// <summary>Listen; <paramref name="changed"/> runs on the UI thread after each read. Dispose to stop.</summary>
    public static IDisposable Subscribe(Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        Listeners.Add(changed);

        if (_watch is null && Folder is { } folder)
        {
            _watch = FolderWatch.Start(folder, Refresh);
        }

        Refresh();
        return new Subscription(changed);
    }

    /// <summary>Read again (single-flight). Never throws.</summary>
    public static void Refresh()
    {
        if (_reading)
        {
            _again = true;
            return;
        }

        _ = ReadAsync();
    }

    /// <summary>[S-4844] Take a file off the list: delete its Recent Items shortcut, as Explorer's "Remove from Recent" does. The file itself is untouched.</summary>
    public static void Remove(RecentFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (Folder is not { } folder || !string.Equals(Path.GetDirectoryName(file.LinkPath), folder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
            || !RecentFileRules.IsRecentLink(Path.GetFileName(file.LinkPath)))
        {
            Log.Warn(Scope, "refused to remove a shortcut that is not in Recent Items");
            return;
        }

        try
        {
            File.Delete(file.LinkPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, "could not remove a recent item", ex);
        }

        Refresh();
    }

    private static async Task ReadAsync()
    {
        _reading = true;

        try
        {
            do
            {
                _again = false;

                IReadOnlyList<RecentFile>? read;
                try
                {
                    read = await Task.Run(Read).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    Log.Warn(Scope, "reading Recent Items threw", ex);
                    read = null;
                }

                Unreadable = read is null;
                if (read is not null)
                {
                    Current = read;
                }

                foreach (var listener in Listeners.ToArray())
                {
                    try
                    {
                        listener();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(Scope, "a Recent Items listener threw", ex);
                    }
                }
            }
            while (_again && Listeners.Count > 0);
        }
        finally
        {
            _reading = false;
        }
    }

    /// <summary>The newest shortcuts, resolved and checked. Null when the folder cannot be listed. Thread pool.</summary>
    private static IReadOnlyList<RecentFile>? Read()
    {
        if (Folder is not { } folder)
        {
            return null;
        }

        List<(string, DateTimeOffset)> links;
        try
        {
            links = [.. new DirectoryInfo(folder)
                .EnumerateFiles("*.lnk", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false })
                .Select(f => (f.FullName, new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero)))];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Warn(Scope, "could not list Recent Items", ex);
            return null;
        }

        var resolved = RecentFileRules.Newest(links)
            .Select(l => new RecentLink(l.LinkPath, l.Written, ShellFileOps.ResolveShortcut(l.LinkPath)))
            .ToList();

        return RecentFileRules.Select(resolved, IsFile);
    }

    private static bool IsFile(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Debug(Scope, $"could not look at a recent target: {ex.Message}");
            return false;
        }
    }

    private sealed class Subscription(Action changed) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Listeners.Remove(changed);

            if (Listeners.Count == 0)
            {
                _watch?.Dispose();
                _watch = null;
            }
        }
    }
}
