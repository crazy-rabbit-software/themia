using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Header;

/// <summary>
/// [S-9600] [S-9601] The Header widget's view: the family's Header block (glyph, name, caption) at the
/// widget's alignment, at the card's side inset, the same at every width, the header's gap (4) under it.
/// [S-12483] In the middle of the whole strip (the card's top inset given back), the same whether a card joins it or not.
/// </summary>
internal sealed partial class HeaderView : Grid, IWidgetView, IWidgetAligned
{
    private readonly HeaderBlock _block = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -FamilySpace.Inset, 0, 0) };
    private HeaderConfig _config = new();
    private ContentAlign _align = ContentAlign.Left;

    public HeaderView()
    {
        Padding = Family.SideInset;
        Children.Add(_block);
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as HeaderConfig ?? new HeaderConfig();
        Render();
    }

    /// <summary>[S-6583] The block at the start, the middle or the end of the strip.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        _align = align;
        Render();
    }

    public void Detach() => Strings.Changed -= OnLanguage;

    private void OnLanguage(Locale locale) => Render();

    private void Render()
    {
        var title = string.IsNullOrWhiteSpace(_config.Text) ? HeaderStr.Header_TitleDefault.Text() : _config.Text;

        // [S-9604] The place of the widgets it heads, once, after the title: they name none of their own ([S-12801]).
        if (_config.PlaceLabel is { } place)
        {
            title = $"{title} · {place}";
        }

        _block.Show(title, _config.Glyph, _config.Caption, Aligned.Horizontal(_align));
    }
}
