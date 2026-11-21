using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.Icon;

/// <summary>
/// [S-15010] The Icon widget's view on the one icon face ([S-15001]): the glyph its config picked, filling its card; it reads
/// nothing and opens nothing, so it is never unavailable and lights nothing. Its name is the kind's ("Icon").
/// </summary>
internal sealed partial class IconWidgetView(IWidgetHost host, IconKind kind) : IconView(host, kind)
{
    protected override void OnAttach(Ticker ticker, WidgetConfig config) => OnApply(config);

    protected override void OnApply(WidgetConfig config) => Show((config as IconWidgetConfig)?.Glyph);

    protected override void OnDetach()
    {
        // ignore: it holds nothing to let go of.
    }

    /// <summary>[S-15011] The glyph picked, or the star.</summary>
    private void Show(string? glyph) => Present(new IconShown(IconChoice.Drawn(glyph, IconWidgetModule.Default)) { Name = IconStr.Icon_Label.Text() });
}
