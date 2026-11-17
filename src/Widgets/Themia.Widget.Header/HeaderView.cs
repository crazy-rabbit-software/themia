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
/// [S-12483] Over a group mate joined under it the block stands at its foot, so the mate's content follows
/// it as a card's header was followed; otherwise in the middle of the whole strip.
/// </summary>
internal sealed partial class HeaderView : Grid, IWidgetView, IWidgetAligned, IWidgetJoins
{
    private readonly HeaderBlock _block = new();
    private HeaderConfig _config = new();
    private ContentAlign _align = ContentAlign.Left;
    private JoinedSides _joins;

    public HeaderView()
    {
        Padding = Family.SideInset;
        Children.Add(_block);
        Stand();
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

    /// <summary>[S-12483] The block against the mate it heads.</summary>
    public void ApplyJoins(JoinedSides sides)
    {
        _joins = sides;
        Stand();
    }

    public void Detach() => Strings.Changed -= OnLanguage;

    private void OnLanguage(Locale locale) => Render();

    private void Stand()
    {
        if (_joins.Bottom)
        {
            // Over a mate: at the strip's foot, the header's gap to the mate's content under it.
            _block.VerticalAlignment = VerticalAlignment.Bottom;
            _block.Margin = new Thickness(0, 0, 0, FamilySpace.HeaderGap);
            return;
        }

        // Free (or over nothing): in the middle of the whole strip, the card's top inset given back.
        _block.VerticalAlignment = VerticalAlignment.Center;
        _block.Margin = new Thickness(0, _joins.Top ? 0 : -FamilySpace.Inset, 0, 0);
    }

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
