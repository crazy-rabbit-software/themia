using Themia.App.Shell;
using Themia.App.Shell.Gallery;

namespace Themia.Widget.Header;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> the add page's picture (<see cref="ShowSampleAsync"/>), <c>plain</c>
/// a title and a glyph without a caption ("This PC"), <c>long</c> a title longer than its strip (its ellipsis),
/// <c>bare</c> a title alone, no glyph.
/// </summary>
internal sealed partial class HeaderView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "plain", "long", "bare"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "plain":
                ApplyConfig(new HeaderConfig { Text = "This PC", Glyph = Glyphs.Display });
                break;
            case "long":
                ApplyConfig(new HeaderConfig { Text = "The weather where the family lives, this week and next", Glyph = Glyphs.Weather, Caption = "Zürich, Bern and Lucerne, refreshed every fifteen minutes" });
                break;
            case "bare":
                ApplyConfig(new HeaderConfig { Text = "Inbox" });
                break;
            default:
                return ShowSampleAsync();
        }

        return Task.CompletedTask;
    }
}
