using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Sdk;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-11850] [S-5605] The desktop item's sample: a document named on no disk, so the shell draws its
/// type's icon, the same on every machine; never an item of the user's desktop.
/// </summary>
internal sealed partial class DesktopItemView : IWidgetSample
{
    /// <summary>The sample's document.</summary>
    internal static Themia.Sdk.DesktopItem SampleDocument => new(@"C:\Users\Public\Desktop\Report.docx", "Report.docx", DesktopItemKind.File);

    public async Task ShowSampleAsync()
    {
        _fixture.On = true;
        _fixture.Selected = false;
        _fixture.Focused = false;
        _fixture.Renaming = false;
        _fixture.DropTarget = false;
        _fixture.Faded = false;
        _fixture.Item = SampleDocument;
        Render();

        // A picture is taken at once: the document's icon (the shell's, cached once fetched) is in it.
        var pixels = DesktopIconPixels.For(_fit.Icon, XamlRoot?.RasterizationScale ?? 1.0);
        try
        {
            _icon.Source = await _host.DesktopItems.IconAsync(SampleDocument, pixels).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "the sample document's icon could not be loaded; the sample draws without it", ex);
        }
    }
}
