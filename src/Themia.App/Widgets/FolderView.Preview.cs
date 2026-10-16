#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-4122]): point the widget at a folder every Windows PC has, read-only, so a
/// render shows real names, icons and thumbnails rather than whoever runs it's profile.
/// "web" is <c>%WINDIR%\Web</c> (folders), "wallpapers" its Windows wallpapers (pictures), "lasso"
/// the web listing with a band dragged from beside the first name down past the last row ([S-6412]).
/// </summary>
internal sealed partial class FolderView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["web", "wallpapers", "lasso"];

    public async Task ShowPreviewStateAsync(string state)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var path = state switch
        {
            "web" or "lasso" => System.IO.Path.Combine(windows, "Web"),
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

        if (state == "lasso")
        {
            // The same calls a real drag makes, from the empty part of the first row.
            var row = _plan?.RowHeight ?? 32;
            var x = Math.Max(0, _items.ActualWidth - 140);
            _bandMode = BandMode.Replace;
            _bandBefore = null;
            _gesture.BeginPress(x, row / 2, FolderPress.ItemBlank);
            _gesture.Move(x + 90, row * 6);
            DrawBand(_gesture.BandRect);
        }
    }
}
#endif
