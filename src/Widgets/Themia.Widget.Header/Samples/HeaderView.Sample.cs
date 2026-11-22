using Themia.App.Shell;
using Themia.App.Widgets;

namespace Themia.Widget.Header;

/// <summary>
/// [S-11850] The add page's picture of the Header widget, in every build: a title ("My day") and the calendar's glyph,
/// at the natural strip (no caption: the picture is drawn at the kind's natural box). A header that already has a title of its own (a ready-made
/// group's, [S-12485]) shows it, as one that names a place does: its words are the person's, not data a sample stands in for. Held by this view alone,
/// never written into a store.
/// </summary>
internal sealed partial class HeaderView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        // [S-15540] One that names its place (a repeater's row of places) shows it: the place is the row's, not sample data.
        if (string.IsNullOrWhiteSpace(_config.Text) && _config.PlaceLabel is null)
        {
            ApplyConfig(new HeaderConfig
            {
                Text = HeaderStr.Header_SampleTitle.Text(),
                Glyph = Glyphs.Calendar,
            });
        }

        return Task.CompletedTask;
    }
}
