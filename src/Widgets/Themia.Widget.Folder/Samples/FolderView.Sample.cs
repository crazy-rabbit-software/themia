using Themia.Core.Model;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-4122] [S-2056] The folder's sample: Robin's documents (three folders, a dozen files),
/// written under the temp folder (<see cref="SampleFiles"/>), so the picture shows ordinary names, the
/// shell's stock icons, and never a name, path or picture of this PC.
/// </summary>
internal sealed partial class FolderView : IWidgetSample
{
    public Task ShowSampleAsync() => ShowFolderAsync(SampleFiles.Write("Documents", SampleFiles.Documents));

    /// <summary>Point the widget at <paramref name="path"/> (a sample folder; nothing for null) and give the listing a moment.</summary>
    internal async Task ShowFolderAsync(string? path)
    {
        if (path is null)
        {
            return;
        }

        // The breadcrumb names the sample folder as Robin's, never the temp folder's path.
        _crumbAlias = (System.IO.Path.GetDirectoryName(path)!, @"C:\Users\Robin");
        var preview = (FolderConfig)_config.Clone();
        preview.Path = path;
        ApplyConfig(preview);

        // The listing is read on the pool; give it, and the thumbnails, a moment before the shot.
        await Task.Delay(1500).ConfigureAwait(true);
    }
}
