#if DEBUG
using Themia.App.Widgets;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1747] Debug renders only: a view whose own item menus <c>--menu-png</c> draws, built by the same
/// code a right-click runs (the folder widget's file, Open with, folder and background menus). The
/// render finds the view through its module by id and names each PNG after the menu.
/// </summary>
public interface IWidgetPreviewMenus
{
    /// <summary>Each menu with the name its PNG is written under (<c>&lt;id&gt;-&lt;name&gt;.png</c>), in order.</summary>
    Task<IReadOnlyList<(string Name, IReadOnlyList<ItemMenuEntry> Entries)>> PreviewMenusAsync();
}

#endif
