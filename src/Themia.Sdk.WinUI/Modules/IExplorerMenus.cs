using Themia.App.Services.Files;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-6416] [S-6418] The pieces of File Explorer's own menus a file widget offers
/// (<see cref="IWidgetHost.ExplorerMenus"/>): the shell's full menu for items or a folder's background
/// ("Show more options"), one of its commands by its verb, and a file's Open with apps. The shell's
/// <c>IContextMenu</c> stays host code.
/// </summary>
public interface IExplorerMenus
{
    /// <summary>The longest Open with list: Explorer's shows a handful, then "Choose another app".</summary>
    int MaxOpenWith { get; }

    /// <summary>The shell's menu for <paramref name="paths"/> at a screen point (physical pixels); the chosen command runs.</summary>
    void ShowForItems(IReadOnlyList<string> paths, int screenX, int screenY, nint owner);

    /// <summary>The shell's menu for the background of <paramref name="folder"/> (New, View, Paste…).</summary>
    void ShowForBackground(string folder, int screenX, int screenY, nint owner);

    /// <summary>Run one of the shell's commands on <paramref name="paths"/> by its canonical verb; false (logged) when it has none.</summary>
    bool InvokeVerb(IReadOnlyList<string> paths, string verb, nint owner);

    /// <summary>A point in a window's client area (physical pixels) on the screen.</summary>
    (int X, int Y) ToScreen(nint window, int x, int y);

    /// <summary>The recommended apps for <paramref name="path"/>'s type, or none (logged) when the shell has none to say.</summary>
    IReadOnlyList<OpenWithApp> OpenWithApps(string path);

    /// <summary>Open <paramref name="path"/> with the app named <paramref name="name"/>; false (logged) when it would not.</summary>
    bool OpenWith(string path, string name);
}
