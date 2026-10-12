#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Model;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-4122]): point the widget at a folder every Windows PC has, read-only, so a
/// render shows real names, icons and thumbnails rather than whoever runs it's profile.
/// "web" is <c>%WINDIR%\Web</c> (folders), "wallpapers" its Windows wallpapers (pictures).
/// </summary>
internal sealed partial class FolderView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["web", "wallpapers"];

    public async Task ShowPreviewStateAsync(string state)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var path = state switch
        {
            "web" => System.IO.Path.Combine(windows, "Web"),
            "wallpapers" => System.IO.Path.Combine(windows, "Web", "Wallpaper", "Windows"),
            _ => null,
        };

        if (path is null)
        {
            return;
        }

        var preview = (FolderConfig)_config.Clone();
        preview.Path = path;
        ApplyConfig(preview);

        // The listing is read on the pool; give it, and the thumbnails, a moment before the shot.
        await Task.Delay(1500).ConfigureAwait(true);
    }
}
#endif
