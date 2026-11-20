using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-5605] [S-9513] Debug renders only ([S-1917]): one real-looking icon per state, never one of the user's.
/// <para>
/// Desktop items: <c>document</c>, <c>folder</c>, <c>link</c> (a shortcut file, with the arrow), <c>thisPc</c>,
/// <c>recycleBin</c>, <c>twoWords</c> (a name of two words on its two lines, [S-5606]), <c>longName</c> (cut
/// at two lines), and the interaction states <c>hover</c>, <c>selected</c>, <c>selectedHover</c>,
/// <c>inactive</c>, <c>focus</c>, <c>editing</c> (the rename box; <c>editingLong</c> on the long name, [S-13950]), <c>dropTarget</c>, <c>cut</c>, <c>hidden</c>.
/// This PC and the Recycle Bin are the shell's own (every PC has them); the files and the link are names on
/// no disk, so the shell draws their type's icon (and the arrow), the same on every machine.
/// </para>
/// <para>
/// Own icons: <c>sample</c> is the sample (<see cref="ShowSampleAsync"/>); <c>web</c> a web address (no
/// favicon is fetched in a render: its globe), <c>ownFolder</c> and <c>app</c> Windows' own, <c>settings</c> a
/// Settings page, <c>layout</c> an app layout of three panes, <c>emoji</c> an icon of the user's,
/// <c>ownTwoWords</c> and <c>ownLongName</c> the names, <c>missing</c> a target that is gone, <c>refused</c>
/// one the rule refuses, <c>waiting</c> one that opens nothing yet, and <c>ownSelected</c>, <c>ownInactive</c>,
/// <c>ownFocus</c>, <c>ownEditing</c>, <c>ownDropTarget</c>. A fixture is applied, never saved.
/// </para>
/// </summary>
internal sealed partial class DesktopItemView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } =
    [
        "sample", "document", "folder", "link", "thisPc", "recycleBin", "twoWords", "longName",
        "hover", "selected", "selectedHover", "inactive", "focus", "editing", "editingLong", "dropTarget", "cut", "hidden",
        "web", "ownFolder", "app", "settings", "layout", "emoji", "ownTwoWords", "ownLongName", "missing", "refused", "waiting",
        "ownSelected", "ownInactive", "ownFocus", "ownEditing", "ownDropTarget",
    ];

    public async Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample":
                await ShowSampleAsync().ConfigureAwait(true);
                return;

            // ---- desktop items ----
            case "document" or "hover" or "selected" or "selectedHover" or "inactive" or "focus" or "editing" or "cut":
                ShowItem(SampleDocument, state);
                break;
            case "folder" or "dropTarget" or "twoWords":
                ShowItem(new(@"C:\Users\Public\Desktop\Holiday photos", "Holiday photos", DesktopItemKind.Folder), state);
                break;
            case "hidden":
                ShowItem(new(@"C:\Users\Public\Desktop\desktop notes.txt", "desktop notes.txt", DesktopItemKind.File, Hidden: true), state);
                break;
            case "link":
                ShowItem(new(@"C:\Users\Public\Desktop\Notes.lnk", "Notes", DesktopItemKind.Shortcut), state);
                break;
            case "thisPc":
                ShowItem(new("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "This PC", DesktopItemKind.ShellItem), state);
                break;
            case "recycleBin":
                ShowItem(new("::{645FF040-5081-101B-9F08-00AA002F954E}", "Recycle Bin", DesktopItemKind.ShellItem), state);
                break;
            case "longName" or "editingLong":
                ShowItem(new(@"C:\Users\Public\Desktop\Quarterly budget review with all the departments 2026.xlsx", "Quarterly budget review with all the departments 2026.xlsx", DesktopItemKind.File), state);
                break;

            // ---- own icons ----
            case "web":
                ShowOwn(config => config.Target = "https://www.example.com/");
                break;
            case "ownFolder":
                ShowOwn(config => config.Target = @"C:\Windows");
                break;
            case "app":
                ShowOwn(config => config.Target = @"C:\Windows\notepad.exe");
                break;
            case "settings":
                ShowOwn(config => config.Target = "ms-settings:display");
                break;
            case "layout":
                ShowOwn(config =>
                {
                    config.Label = "Morning";
                    config.Layout = DemoLayout();
                });
                break;
            case "emoji":
                ShowOwn(config =>
                {
                    config.Target = @"C:\Windows\notepad.exe";
                    config.Label = "Notes";
                    config.IconEmoji = "\U0001F680";
                });
                break;
            case "ownTwoWords":
                ShowOwn(config =>
                {
                    config.Target = @"C:\Windows\notepad.exe";
                    config.Label = "Mission Planner";
                });
                break;
            case "ownLongName":
                ShowOwn(config =>
                {
                    config.Target = @"C:\Windows";
                    config.Label = "Quarterly planning documents and meeting notes";
                });
                break;
            case "missing":
                ShowOwn(config => config.Target = @"C:\Users\Public\Documents\Gone report.docx");
                _health = TargetHealth.Missing;
                PaintHealth();
                break;
            case "refused":
                ShowOwn(config => config.Target = "javascript:alert(1)");
                break;
            case "waiting":
                ShowOwn(_ => { });
                break;
            case "ownSelected" or "ownInactive" or "ownFocus" or "ownEditing" or "ownDropTarget":
                ShowOwn(config => config.Target = LaunchTargets.ShellTarget("Personal"));
                break;
            default:
                return;
        }

        _fixture.Selected = state is "selected" or "selectedHover" or "inactive" or "focus" or "editing" or "editingLong" or "ownSelected" or "ownInactive" or "ownFocus" or "ownEditing";
        _fixture.Inactive = state is "inactive" or "ownInactive";
        _fixture.Focused = state is "focus" or "ownFocus";
        _fixture.Renaming = state is "editing" or "editingLong" or "ownEditing";
        _fixture.DropTarget = state is "dropTarget" or "ownDropTarget";
        _fixture.Faded = state is "cut" or "hidden";
        _hovered = state is "hover" or "selectedHover";
        Render();
        await _iconLoad.ConfigureAwait(true);
    }

    /// <summary>Draw <paramref name="item"/> as a desktop item at rest.</summary>
    private void ShowItem(Themia.Sdk.DesktopItem item, string state)
    {
        _ = state;
        _fixture.Rest(item);
        Changed();
    }

    /// <summary>[S-1405] A demo app layout: Notepad on the left 60 %, Explorer over the Windows folder on the right. Never launched.</summary>
    private static LayoutNode DemoLayout() => new()
    {
        Id = "r",
        Kind = LayoutNodeKind.Split,
        Direction = SplitDirection.Vertical,
        Ratio = 0.6,
        A = Pane("p1", LayoutSource.App, @"C:\Windows\notepad.exe"),
        B = new LayoutNode
        {
            Id = "s",
            Kind = LayoutNodeKind.Split,
            Direction = SplitDirection.Horizontal,
            Ratio = 0.5,
            A = Pane("p2", LayoutSource.App, @"C:\Windows\explorer.exe"),
            B = Pane("p3", LayoutSource.Folder, @"C:\Windows"),
        },
    };

    private static LayoutNode Pane(string id, LayoutSource source, string target) => new()
    {
        Id = id,
        Kind = LayoutNodeKind.Pane,
        Assignment = new LayoutAssignment { Source = source, Target = target },
    };
}
