using Themia.Sdk;

namespace Themia.Widget.Fence;

/// <summary>
/// [S-11850] What a picture of a fence draws instead of the desktop (the sample, a debug render's states): its own items
/// and the selection, focus, cut and rename it sets. A fixture is drawn, never acted on and never saved.
/// </summary>
internal sealed partial class FenceView
{
    private readonly PreviewFixture _fixture = new();

    private sealed class PreviewFixture
    {
        public bool On { get; set; }

        public List<DesktopItem> Items { get; set; } = [];

        public HashSet<string> Selected { get; } = new(DesktopItemKeys.Comparer);

        public HashSet<string> Faded { get; } = new(DesktopItemKeys.Comparer);

        public string? Focus { get; set; }

        public bool Inactive { get; set; }

        public string? Renaming { get; set; }

        /// <summary>Back to a fence at rest holding <paramref name="items"/>.</summary>
        public void Rest(IEnumerable<DesktopItem> items)
        {
            On = true;
            Items = [.. items];
            Selected.Clear();
            Faded.Clear();
            Focus = null;
            Inactive = false;
            Renaming = null;
        }
    }

    /// <summary>Draw <paramref name="config"/> holding <paramref name="items"/>, and wait for their icons.</summary>
    private async Task ShowFixtureAsync(FenceConfig config, IEnumerable<DesktopItem> items, Action<PreviewFixture>? set = null)
    {
        _fixture.Rest(items);
        set?.Invoke(_fixture);
        _config = config;
        _order = null;
        CloseTitleBox(commit: false);
        Render();
        await Task.WhenAll(_loads).ConfigureAwait(true);
    }
}
