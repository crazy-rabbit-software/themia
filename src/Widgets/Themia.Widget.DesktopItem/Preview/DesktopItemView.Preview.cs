#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Sdk;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-5605] Debug renders only ([S-1917]): one real-looking item per state, never the user's desktop.
/// This PC and the Recycle Bin are the shell's own (every PC has them); the files and the shortcut are
/// names on no disk, so the shell draws their type's icon (and the arrow), the same on every machine.
/// </summary>
internal sealed partial class DesktopItemView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["document", "folder", "shortcut", "thisPc", "recycleBin", "longName", "selected", "focus", "editing", "dropTarget", "cut", "hidden"];

    public Task ShowPreviewStateAsync(string state)
    {
        _fixture.On = true;
        _fixture.Selected = state is "selected" or "focus" or "editing";
        _fixture.Focused = state == "focus";
        _fixture.Renaming = state == "editing";
        _fixture.DropTarget = state == "dropTarget";
        _fixture.Faded = state is "cut" or "hidden";
        _fixture.Item = state switch
        {
            "dropTarget" => new(@"C:\Users\Public\Desktop\Holiday photos", "Holiday photos", DesktopItemKind.Folder),
            "hidden" => new(@"C:\Users\Public\Desktop\desktop notes.txt", "desktop notes.txt", DesktopItemKind.File, Hidden: true),
            "folder" => new(@"C:\Users\Public\Desktop\Holiday photos", "Holiday photos", DesktopItemKind.Folder),
            "shortcut" => new(@"C:\Users\Public\Desktop\Notes.lnk", "Notes", DesktopItemKind.Shortcut),
            "thisPc" => new("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "This PC", DesktopItemKind.ShellItem),
            "recycleBin" => new("::{645FF040-5081-101B-9F08-00AA002F954E}", "Recycle Bin", DesktopItemKind.ShellItem),
            "longName" => new(@"C:\Users\Public\Desktop\Quarterly budget review with all the departments 2026.xlsx", "Quarterly budget review with all the departments 2026.xlsx", DesktopItemKind.File),
            _ => new(@"C:\Users\Public\Desktop\Report.docx", "Report.docx", DesktopItemKind.File),
        };

        Render();
        return Task.CompletedTask;
    }
}
#endif
