using Themia.App.Shell.Gallery;
using Themia.Sdk;

namespace Themia.Widget.Fence;

/// <summary>
/// [S-10915] Debug renders only ([S-1917]): a fence in each of its states, on the sample's items (names on no disk), drawn
/// in its cells of the icon grid as the desktop's icons are. <c>sample</c> the sample; <c>empty</c> a new fence;
/// <c>selected</c> two items selected, the focus on one; <c>inactive</c> the same while another window has the keyboard;
/// <c>title</c> the title's rename box; <c>rolledUp</c> the title bar alone; <c>many</c> more items than it shows (the
/// body's thumb); <c>cut</c> an item cut to the clipboard; <c>sorted</c> its items by name.
/// </summary>
internal sealed partial class FenceView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "empty", "selected", "inactive", "title", "rolledUp", "many", "cut", "sorted"];

    /// <summary>[S-11603] The state's own config sizes the card: rolled up, the title bar alone.</summary>
    public Themia.Core.Model.WidgetConfig? DrawnConfig => _fixture.On ? _config : null;

    public async Task ShowPreviewStateAsync(string state)
    {
        var name = FenceStr.Fence_SampleName.Text();
        switch (state)
        {
            case "empty":
                await ShowFixtureAsync(new FenceConfig(), []).ConfigureAwait(true);
                break;
            case "selected" or "inactive":
                await ShowFixtureAsync(new FenceConfig { Name = name }, SampleItems, f =>
                {
                    f.Selected.Add(SampleItems[1].Key);
                    f.Selected.Add(SampleItems[2].Key);
                    f.Focus = SampleItems[2].Key;
                    f.Inactive = state == "inactive";
                }).ConfigureAwait(true);
                break;
            case "title":
                await ShowFixtureAsync(new FenceConfig { Name = name }, SampleItems).ConfigureAwait(true);
                OpenTitleBox();
                break;
            case "rolledUp":
                await ShowFixtureAsync(new FenceConfig { Name = name, RolledUp = true }, SampleItems).ConfigureAwait(true);
                break;
            case "many":
                await ShowFixtureAsync(new FenceConfig { Name = name }, Many()).ConfigureAwait(true);
                break;
            case "cut":
                await ShowFixtureAsync(new FenceConfig { Name = name }, SampleItems, f => f.Faded.Add(SampleItems[3].Key)).ConfigureAwait(true);
                break;
            case "sorted":
                await ShowFixtureAsync(new FenceConfig { Name = name, Sort = Themia.Core.Desktop.DesktopSortBy.Name }, SampleItems).ConfigureAwait(true);
                break;
            default:
                await ShowSampleAsync().ConfigureAwait(true);
                break;
        }
    }

    /// <summary>Twenty items: more than a fence of its natural size shows.</summary>
    private static IEnumerable<DesktopItem> Many()
    {
        string[] kinds = [".docx", ".xlsx", ".pdf", ".txt", ".png", ".zip", ".mp3"];
        for (var i = 1; i <= 20; i++)
        {
            var name = $"File {i}{kinds[i % kinds.Length]}";
            yield return new DesktopItem($@"C:\Users\Public\Desktop\{name}", name, DesktopItemKind.File);
        }
    }
}
