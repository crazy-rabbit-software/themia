using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>[S-11850] [S-1917] [S-2056] The recent files' sample: the sample day's files, never the user's own Recent Items.</summary>
internal sealed partial class RecentFilesView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        var now = SampleDay.Now;

        // [S-2056] Invented documents of the sample day, written as empty files under the temp folder so
        // the shell draws each type's stock icon; a row shows the folder name given here, never that path.
        var documents = SampleFiles.Write("Documents", SampleFiles.Documents);
        RecentFile File(string folder, string name, double hoursAgo)
            => new(System.IO.Path.Combine(documents ?? $@"C:\Users\Robin\{folder}", name), name, folder, now.AddHours(-hoursAgo), $@"C:\Users\Robin\Recent\{name}.lnk");

        ShowFiles(now,
        [
            File("Documents", "Onboarding flow v3.pptx", 0.3),
            File("Downloads", "Quarterly numbers Q3.xlsx", 0.4),
            File("Documents", "1-1 notes Sam.docx", 0.9),
            File("Downloads", "Northwind contract draft.pdf", 2),
            File("Desktop", "Budget 2027.xlsx", 3.5),
            File("Pictures", "Whiteboard sketch.jpg", 5),
            File("Documents", "Release notes 4.2.docx", 18),
            File("Downloads", "Lisbon offsite agenda.pdf", 22),
            File("Documents", "Store listing copy.txt", 26),
            File("Documents", "Meeting notes.md", 24 * 4),
        ]);
        return Task.CompletedTask;
    }

    /// <summary>Show <paramref name="files"/> in place of Recent Items, the clock at <paramref name="now"/>.</summary>
    internal void ShowFiles(DateTimeOffset now, IReadOnlyList<RecentFile> files)
    {
        _clock = () => now;
        _files = files;
        _unreadable = false;
        Render();
    }
}
