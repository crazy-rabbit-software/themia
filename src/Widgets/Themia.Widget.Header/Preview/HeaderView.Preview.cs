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

    /// <summary>
    /// [S-11603] The state's own config: a render sizes the card by it (the long state's caption is a second line, so its
    /// strip is taller than the one a header without a caption states).
    /// </summary>
    public Themia.Core.Model.WidgetConfig? DrawnConfig { get; private set; }

    public Task ShowPreviewStateAsync(string state)
    {
        HeaderConfig? shown = state switch
        {
            "plain" => new HeaderConfig { Text = "This PC", Glyph = Glyphs.Display },
            "long" => new HeaderConfig { Text = "The weather where the family lives, this week and next", Glyph = Glyphs.Weather, Caption = "Zürich, Bern and Lucerne, refreshed every fifteen minutes" },
            "bare" => new HeaderConfig { Text = "Inbox" },
            _ => null,
        };

        if (shown is null)
        {
            return ShowSampleAsync();
        }

        DrawnConfig = shown;
        ApplyConfig(shown);
        return Task.CompletedTask;
    }
}
