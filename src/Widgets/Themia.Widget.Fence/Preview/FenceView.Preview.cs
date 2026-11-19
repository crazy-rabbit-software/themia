#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Sdk;

namespace Themia.Widget.Fence;

/// <summary>
/// [S-10915] Debug renders only ([S-1917]): a fence in each of its states, on the sample's items (names on no disk).
/// <c>sample</c> the sample; <c>empty</c> a new fence; <c>selected</c> two items selected, the focus on one;
/// <c>inactive</c> the same while another window has the keyboard; <c>renaming</c> an item's rename box; <c>title</c> the
/// title's rename box; <c>rolledUp</c> the title bar alone; <c>many</c> more items than it shows (it scrolls, the bottom
/// fading); <c>cut</c> an item cut to the clipboard; <c>sorted</c> its items by name.
/// </summary>
internal sealed partial class FenceView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "empty", "selected", "inactive", "renaming", "title", "rolledUp", "many", "cut", "sorted"];

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
            case "renaming":
                await ShowFixtureAsync(new FenceConfig { Name = name }, SampleItems, f =>
                {
                    f.Selected.Add(SampleItems[0].Key);
                    f.Renaming = SampleItems[0].Key;
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
                await ShowFixtureAsync(new FenceConfig { Name = name }, [.. DesktopItemOrder.Sort(SampleItems)]).ConfigureAwait(true);
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
#endif
