using Themia.Sdk;

namespace Themia.Widget.Fence;

/// <summary>
/// [S-11850] What a picture of a fence draws instead of the desktop (the sample, a debug render's states): items in its
/// cells, drawn as the desktop's icons are, and the selection, focus and cut it sets. A fixture is drawn, never acted on and
/// never saved; on the desktop a fence's items are the desktop's own icons.
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

        /// <summary>Back to a fence at rest holding <paramref name="items"/>.</summary>
        public void Rest(IEnumerable<DesktopItem> items)
        {
            On = true;
            Items = [.. items];
            Selected.Clear();
            Faded.Clear();
            Focus = null;
            Inactive = false;
        }
    }

    /// <summary>Draw <paramref name="config"/> holding <paramref name="items"/>, and wait for their icons.</summary>
    private async Task ShowFixtureAsync(FenceConfig config, IEnumerable<DesktopItem> items, Action<PreviewFixture>? set = null)
    {
        _fixture.Rest(items);
        set?.Invoke(_fixture);
        _config = config;
        CloseTitleBox(commit: false);
        Render();
        await Task.WhenAll(_loads).ConfigureAwait(true);
    }
}
