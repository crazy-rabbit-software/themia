using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services.Web;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12706] A weather graph's whole content: the one <see cref="HourGraph"/>, drawn at its baseline with a caption
/// while there is nothing to draw ([S-6753]: loading, offline, nothing for the window), never a Status row in its place. The view follows its answer
/// through the SDK's <c>SkyFeed</c> ([S-11171], redrawing every half hour so the window moves on with the clock)
/// and hands each change here with its kind's rules (the answer at a moment to a <see cref="HourGraphModel"/>,
/// null for nothing in the window), so every graph says its states the same way. A graph's view is this panel
/// (it derives from it) ([S-12800]). [S-6754] Its plot runs to the card's edges, the top one too: it owns it ([S-5700]).
/// </summary>
public partial class HourGraphPanel : Grid, IWidgetTopEdge
{
    /// <summary>[S-5700] [S-6754] The plot is drawn to the card's top edge; the top line keeps the inset over it.</summary>
    public bool OwnsTopEdge => true;

    /// <summary>[S-12706] How often a graph redraws without news: the window and the now marker move on.</summary>
    public static TimeSpan Cadence { get; } = TimeSpan.FromMinutes(30);

    private readonly HourGraph _graph = new();
    private readonly string _glyph;

    /// <param name="glyph">The kind's glyph, leading the graph's top line while it has no reading.</param>
    public HourGraphPanel(string glyph)
    {
        _glyph = glyph;
        Children.Add(_graph);
    }

    /// <summary>[S-11850] A sample's fixed moment; null on the desktop (the clock's).</summary>
    public DateTimeOffset? PinnedNow { get; set; }

    /// <summary>
    /// [S-12706] [S-6753] Draw the answer in hand through <paramref name="map"/>; with none, or nothing in the window, the
    /// graph is drawn all the same at its baseline in <paramref name="config"/>'s look, with why as its caption:
    /// <paramref name="status"/>'s sentence, or <paramref name="empty"/> (owner, 2026-11-19: "it should show the default
    /// values in the graph and not just not show the graph"). The card menu's Refresh is the retry.
    /// </summary>
    public void Render<T>(HourGraphConfig config, Fetched<T>? held, FetchStatus status, Func<T, DateTimeOffset, HourGraphModel?> map, string empty)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(map);
        // [S-6754] Nothing to draw keeps the frame's time line too: "Now" at its start (its end is the answer's).
        var now = Str.WeatherGraph_Now.Text();
        var model = held is { Value: { } value }
            ? map(value, PinnedNow ?? DateTimeOffset.Now) ?? HourGraphModel.Nothing(config, empty) with { Glyph = _glyph, StartLabel = now }
            : HourGraphModel.Nothing(config, OnlineParts.Caption(status)) with { Glyph = _glyph, StartLabel = now };
        _graph.Problems(OnlineParts.Problems(held));
        _graph.Show(model);
    }
}
