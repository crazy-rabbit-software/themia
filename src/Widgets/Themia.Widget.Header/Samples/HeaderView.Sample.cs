using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.Header;

/// <summary>
/// [S-11850] The add page's picture of the Header widget, in every build: a title ("My day") and the calendar's glyph,
/// at the natural strip (no caption: the picture is drawn at the kind's natural box). A header that already has a title of its own (a ready-made
/// group's, [S-12485]) shows it: its words are the person's, not data a sample stands in for. [S-9604] One that shows
/// its place and has none yet (a ready-made group about a place, as placed) names the sample place, as its mates'
/// pictures are about it. Held by this view alone, never written into a store.
/// </summary>
internal sealed partial class HeaderView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        if (string.IsNullOrWhiteSpace(_config.Text))
        {
            ApplyConfig(new HeaderConfig
            {
                Text = HeaderStr.Header_SampleTitle.Text(),
                Glyph = Glyphs.Calendar,
            });
        }
        else if (_config is { ShowsPlace: true, HasLocation: false, UseDeviceLocation: false })
        {
            var placed = (HeaderConfig)_config.Clone();
            placed.Latitude = SamplePlace.Latitude;
            placed.Longitude = SamplePlace.Longitude;
            placed.PlaceName = SamplePlace.LocationName;
            ApplyConfig(placed);
        }

        return Task.CompletedTask;
    }
}
