using Themia.App.Widgets;
using Themia.Sdk;

namespace Themia.Widget.Fence;

/// <summary>
/// [S-11850] The add page's picture of a fence, in every build: a fence named "Work" holding a few of the sample desk's
/// items (names on no disk, so the shell draws their type's icons, the same on every machine), held by this view alone,
/// never written into a store. Render-only states go in <c>Preview/</c>, Debug only.
/// </summary>
internal sealed partial class FenceView : IWidgetSample
{
    /// <summary>[S-11850] The sample fence's items: a document, a sheet, a folder, a text file, a PDF and a shortcut.</summary>
    internal static readonly DesktopItem[] SampleItems =
    [
        new(@"C:\Users\Public\Desktop\Quarterly report.docx", "Quarterly report.docx", DesktopItemKind.File),
        new(@"C:\Users\Public\Desktop\Budget 2026.xlsx", "Budget 2026.xlsx", DesktopItemKind.File),
        new(@"C:\Users\Public\Desktop\Invoices", "Invoices", DesktopItemKind.Folder),
        new(@"C:\Users\Public\Desktop\Meeting notes.txt", "Meeting notes.txt", DesktopItemKind.File),
        new(@"C:\Users\Public\Desktop\Project plan.pdf", "Project plan.pdf", DesktopItemKind.File),
        new(@"C:\Users\Public\Desktop\Notes.lnk", "Notes", DesktopItemKind.Shortcut),
    ];

    public Task ShowSampleAsync() => ShowFixtureAsync(new FenceConfig { Name = FenceStr.Fence_SampleName.Text() }, SampleItems);
}
