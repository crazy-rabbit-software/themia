using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-1324] What a file row or tile draws for an item (<see cref="IWidgetHost.FileIcons"/>): the
/// shell's real icon, a picture's thumbnail, the shell's type name and a folder's item count, each
/// extracted off the UI thread and cached by the host once for every widget. The shell calls stay
/// host code. UI thread.
/// </summary>
public interface IFileIcons
{
    /// <summary>[S-1322] Counting stops here; a folder with more shows "999+".</summary>
    int CountCap { get; }

    /// <summary>The cached icon, if there is one already (no extraction): lets a redraw paint at once.</summary>
    bool TryIcon(FolderEntry entry, int pixels, out ImageSource? image);

    /// <summary>The shell icon for an entry at <paramref name="pixels"/> (physical) pixels, or null.</summary>
    Task<ImageSource?> IconAsync(FolderEntry entry, int pixels);

    /// <summary>A thumbnail for an image file, or null for one that has none, is not an image, or is a cloud placeholder.</summary>
    Task<ImageSource?> ThumbnailAsync(FolderEntry entry, int pixels);

    /// <summary>[S-1322] The shell's name for the item's type ("Text Document"), or Explorer's fallback.</summary>
    string TypeName(FolderEntry entry);

    /// <summary>[S-1322] How many entries a folder holds, up to <see cref="CountCap"/>; null for one that cannot be read.</summary>
    Task<int?> CountAsync(FolderEntry folder);
}
