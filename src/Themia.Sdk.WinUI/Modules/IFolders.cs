using Themia.App.Services.Files;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-1321] A folder on this PC as a widget reads it (<see cref="IWidgetHost.Folders"/>): its listing,
/// a watch on it, the repository it is in, Explorer's New file types and a ZIP of some of its items.
/// The disk, the change notifications, <c>git</c> and the registry stay host code.
/// </summary>
public interface IFolders
{
    /// <summary>
    /// List a folder, unsorted and unfiltered. <b>Background work</b> (a mapped drive whose server went
    /// away can sit for most of a minute): never on the UI thread. Never throws: every failure is a
    /// named <see cref="FolderListingStatus"/>.
    /// </summary>
    FolderSnapshot Read(string path, CancellationToken token);

    /// <summary>
    /// [S-1333] Follow changes under <paramref name="path"/>: <paramref name="changed"/> runs on the
    /// calling (UI) thread when something in it changes, or an in-app file operation touched it. Dispose to stop.
    /// </summary>
    IFolderWatch Watch(string path, Action changed, bool subfolders = false, bool namesOnly = false);

    /// <summary>[S-1325] The repository <paramref name="folder"/> is in and its status, or null when it is in none (or there is no git).</summary>
    Task<GitRepoStatus?> GitStatusAsync(string folder);

    /// <summary>[S-1331] Explorer's New file types, sorted by label: read once per run, later asks share the one read.</summary>
    Task<IReadOnlyList<ShellNewTemplate>> NewFileTemplatesAsync();

    /// <summary>[S-6419] Compress <paramref name="paths"/> into <paramref name="zipPath"/> off the UI thread: the ZIP's path, or null (logged) when it failed.</summary>
    Task<string?> CompressAsync(IReadOnlyList<string> paths, string zipPath);
}

/// <summary>One widget's watch on a folder. Dispose to stop, once.</summary>
public interface IFolderWatch : IDisposable
{
    /// <summary>Whether change notifications are arriving; false means the widget polls (some network shares).</summary>
    bool Watching { get; }
}
