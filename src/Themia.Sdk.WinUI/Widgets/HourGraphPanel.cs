using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services.Web;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12706] A weather graph's whole content: the one <see cref="HourGraph"/>, or the Status block's one row while
/// there is nothing to draw ([S-6734]: loading, offline, nothing for the window). The view follows its answer
/// through the SDK's <c>SkyFeed</c> ([S-11171], redrawing every half hour so the window moves on with the clock)
/// and hands each change here with its kind's rules (the answer at a moment to a <see cref="HourGraphModel"/>,
/// null for nothing in the window), so every graph says its states the same way. A graph's view is this panel
/// (it derives from it) ([S-12800]).
/// </summary>
public partial class HourGraphPanel : Grid
{
    /// <summary>[S-12706] How often a graph redraws without news: the window and the now marker move on.</summary>
    public static TimeSpan Cadence { get; } = TimeSpan.FromMinutes(30);

    private readonly HourGraph _graph = new();
    private readonly StatusBlock _state = new();
    private readonly string _glyph;

    /// <param name="glyph">The kind's glyph, leading its Status row.</param>
    public HourGraphPanel(string glyph)
    {
        _glyph = glyph;
        _graph.Visibility = Visibility.Collapsed;
        _state.Visibility = Visibility.Collapsed;
        Children.Add(_graph);
        Children.Add(_state);
    }

    /// <summary>[S-11850] A sample's fixed moment; null on the desktop (the clock's).</summary>
    public DateTimeOffset? PinnedNow { get; set; }

    /// <summary>
    /// [S-12706] Draw the answer in hand through <paramref name="map"/>, or say why there is none: <paramref name="status"/>'s
    /// row (Retry runs <paramref name="retry"/>), or <paramref name="empty"/> when the answer has nothing for the window.
    /// </summary>
    public void Render<T>(Fetched<T>? held, FetchStatus status, Func<T, DateTimeOffset, HourGraphModel?> map, string empty, Action retry)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(map);
        if (held is not { Value: { } value })
        {
            // [S-6734] Not set up is the card's Set up row ([S-1862]); every other state is one row here.
            OnlineParts.ShowState(_state, status, _glyph, retry);
            _graph.Visibility = Visibility.Collapsed;
            return;
        }

        if (map(value, PinnedNow ?? DateTimeOffset.Now) is not { } model)
        {
            _state.Show(StatusKind.Empty, _glyph, empty);
            _graph.Visibility = Visibility.Collapsed;
            return;
        }

        _state.Hide();
        _graph.Visibility = Visibility.Visible;
        _graph.Problems(OnlineParts.Problems(held));
        _graph.Show(model);
    }
}
