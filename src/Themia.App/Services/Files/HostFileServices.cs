using Microsoft.UI.Xaml.Media;
using Themia.Core.Files;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Services.Files;

/// <summary>[S-1324] <see cref="IWidgetHost.FileIcons"/>: the host's one icon, thumbnail, type and count cache.</summary>
internal sealed class HostFileIcons : IFileIcons
{
    public static HostFileIcons Instance { get; } = new();

    private HostFileIcons()
    {
    }

    public int CountCap => FileIcons.CountCap;

    public bool TryIcon(FolderEntry entry, int pixels, out ImageSource? image) => FileIcons.TryIcon(entry, pixels, out image);

    public Task<ImageSource?> IconAsync(FolderEntry entry, int pixels) => FileIcons.IconAsync(entry, pixels);

    public Task<ImageSource?> ThumbnailAsync(FolderEntry entry, int pixels) => FileIcons.ThumbnailAsync(entry, pixels);

    public string TypeName(FolderEntry entry) => FileIcons.TypeName(entry);

    public Task<int?> CountAsync(FolderEntry folder) => FileIcons.CountAsync(folder);
}

/// <summary>[S-1329] <see cref="IWidgetHost.FileClipboard"/>: the real Windows clipboard, with the host's memory of a cut.</summary>
internal sealed class HostFileClipboard : IFileClipboard
{
    public static HostFileClipboard Instance { get; } = new();

    private HostFileClipboard()
    {
    }

    public event Action? Changed
    {
        add => FileClipboard.Changed += value;
        remove => FileClipboard.Changed -= value;
    }

    public bool HasFiles => FileClipboard.HasFiles;

    public bool SetFiles(IReadOnlyList<string> paths, bool cut) => FileClipboard.SetFiles(paths, cut);

    public bool IsCut(string path) => FileClipboard.IsCut(path);

    public bool CutStillPending() => FileClipboard.CutStillPending();

    public FileClipboardContent? Read() => FileClipboard.Read();

    public IReadOnlyList<string> SaveVirtualFiles(string destination) => FileClipboard.SaveVirtualFiles(destination);

    public void CompleteCut() => FileClipboard.CompleteCut();
}

/// <summary>[S-1321] <see cref="IWidgetHost.Folders"/>: the disk, the change notifications, git and the registry.</summary>
internal sealed class HostFolders : IFolders
{
    public static HostFolders Instance { get; } = new();

    private HostFolders()
    {
    }

    public FolderSnapshot Read(string path, CancellationToken token) => FolderListing.Read(path, token);

    public IFolderWatch Watch(string path, Action changed, bool subfolders = false, bool namesOnly = false)
        => FolderWatch.Start(path, changed, subfolders, namesOnly);

    public Task<GitRepoStatus?> GitStatusAsync(string folder) => GitStatus.GetAsync(folder);

    public Task<IReadOnlyList<ShellNewTemplate>> NewFileTemplatesAsync() => ShellNewTemplates.GetAsync();

    public Task<string?> CompressAsync(IReadOnlyList<string> paths, string zipPath) => ZipFiles.CompressAsync(paths, zipPath);
}

/// <summary>[S-1330] <see cref="IWidgetHost.FileOps"/>: the shell's own operations on the host's one worker, and OLE's drag out.</summary>
internal sealed class HostFileOps : IShellFileOps
{
    public static HostFileOps Instance { get; } = new();

    private HostFileOps()
    {
    }

    public event Action<IReadOnlyList<string>>? Completed
    {
        add => ShellFileOps.Completed += value;
        remove => ShellFileOps.Completed -= value;
    }

    public Task<FileOpOutcome> CopyAsync(IReadOnlyList<string> sources, string destination) => ShellFileOps.CopyAsync(sources, destination);

    public Task<FileOpOutcome> MoveAsync(IReadOnlyList<string> sources, string destination) => ShellFileOps.MoveAsync(sources, destination);

    public Task<FileOpOutcome> RecycleAsync(IReadOnlyList<string> paths) => ShellFileOps.RecycleAsync(paths);

    public Task<FileOpOutcome> RenameAsync(string path, string newName) => ShellFileOps.RenameAsync(path, newName);

    public string? NewFolder(string destination) => ShellFileOps.NewFolder(destination);

    public string? NewFile(string destination, ShellNewTemplate template) => ShellFileOps.NewFile(destination, template);

    public void Open(string path) => ShellFileOps.Open(path);

    public void OpenWith(string path) => ShellFileOps.OpenWith(path);

    public void Properties(IReadOnlyList<string> paths) => ShellFileOps.Properties(paths);

    public void ShowInExplorer(IReadOnlyList<string> paths) => ShellFileOps.ShowInExplorer(paths);

    public int SetCloudPin(IReadOnlyList<string> paths, CloudPin pin) => ShellFileOps.SetCloudPin(paths, pin);

    public string? ResolveShortcut(string path, bool withoutArguments = false) => ShellFileOps.ResolveShortcut(path, withoutArguments);

    public DropEffect DragOut(IReadOnlyList<string> paths, DropEffect allowed, nint window) => DragDropInterop.DragOut(paths, allowed, window);
}

/// <summary>[S-6416] <see cref="IWidgetHost.ExplorerMenus"/>: the shell's <c>IContextMenu</c> and the Open with handlers.</summary>
internal sealed class HostExplorerMenus : IExplorerMenus
{
    public static HostExplorerMenus Instance { get; } = new();

    private HostExplorerMenus()
    {
    }

    public int MaxOpenWith => Themia.App.Services.Files.OpenWithApps.MaxShown;

    public void ShowForItems(IReadOnlyList<string> paths, int screenX, int screenY, nint owner) => ShellContextMenu.ShowForItems(paths, screenX, screenY, owner);

    public void ShowForBackground(string folder, int screenX, int screenY, nint owner) => ShellContextMenu.ShowForBackground(folder, screenX, screenY, owner);

    public bool InvokeVerb(IReadOnlyList<string> paths, string verb, nint owner) => ShellContextMenu.InvokeVerb(paths, verb, owner);

    public (int X, int Y) ToScreen(nint window, int x, int y) => ShellContextMenu.ToScreen(window, x, y);

    public IReadOnlyList<OpenWithApp> OpenWithApps(string path) => Themia.App.Services.Files.OpenWithApps.For(path);

    public bool OpenWith(string path, string name) => Themia.App.Services.Files.OpenWithApps.Open(path, name);
}

/// <summary>[S-4841] <see cref="IWidgetHost.RecentFiles"/>: the host's one Recent Items list.</summary>
internal sealed class HostRecentItems : IRecentItems
{
    public static HostRecentItems Instance { get; } = new();

    private HostRecentItems()
    {
    }

    public IReadOnlyList<RecentFile>? Current => RecentItems.Current;

    public bool Unreadable => RecentItems.Unreadable;

    public IDisposable Subscribe(Action changed) => RecentItems.Subscribe(changed);

    public void Refresh() => RecentItems.Refresh();

    public void Remove(RecentFile file) => RecentItems.Remove(file);
}
