#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): a fixture list, never the user's own Recent Items.</summary>
internal sealed partial class RecentFilesView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "empty"];

    public Task ShowPreviewStateAsync(string state)
    {
        var now = DateTimeOffset.Now;
        RecentFile File(string folder, string name, double hoursAgo)
            => new($@"C:\Users\Sample\{folder}\{name}", name, folder, now.AddHours(-hoursAgo), $@"C:\Users\Sample\Recent\{name}.lnk");

        _files = state == "sample"
            ?
            [
                File("Documents", "Quarterly report.docx", 0.2),
                File("Downloads", "Invoice 2026-10.pdf", 1.5),
                File("Desktop", "Budget 2027.xlsx", 3),
                File("Pictures", "Lisbon offsite.jpg", 20),
                File("Projects", "notes.md", 26),
                File("Documents", "Design review.pptx", 50),
                File("Downloads", "setup-guide.txt", 72),
                File("Music", "Morning playlist.m3u", 100),
            ]
            : [];
        _unreadable = false;
        Render();
        return Task.CompletedTask;
    }
}
#endif
