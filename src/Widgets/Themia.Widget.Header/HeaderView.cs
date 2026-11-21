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
/// widget's alignment, in the card's inset. [S-14307] Its one line grows until the width stops it (owner, 2026-11-19, of
/// the clock, the date and "the Header widget, whose one line grows until the width stops it": "Centred is fine"): the
/// block scales up as one (<see cref="ReadingFit"/>) until its ink fills the card's height or its width, the height left
/// shared alike over and under it, across where its alignment puts it. [S-12483] The same whether a card joins it or not.
/// </summary>
internal sealed partial class HeaderView : Grid, IWidgetView, IWidgetAligned, IWidgetSizeAware
{
    private readonly HeaderBlock _block = new();
    private readonly ReadingFit _fit = new();
    private HeaderConfig _config = new();
    private ContentAlign _align = ContentAlign.Left;

    public HeaderView()
    {
        // [S-14307] The line in the strip's middle at its own size in its natural strip, alike over and under (the card's
        // top inset partly given back), and grown as one in a larger card, the same band over and under it.
        Padding = Family.SideInset;
        _fit.Margin = new Thickness(0, Band - FamilySpace.Inset, 0, Band);
        _fit.Parts.Add(_block);
        Children.Add(_fit);
    }

    /// <summary>
    /// [S-14307] The band over and under the line: what the natural strip (<see cref="HeaderLayout.Height"/>) leaves round a
    /// line's ink (its title's size, its capitals over a descender), so the line stands at its own size there.
    /// </summary>
    private static double Band => (HeaderLayout.Height(caption: false) - FamilyType.BodyStrong.FontSize) / 2;

    /// <summary>
    /// [S-14307] The line's width: at most the card's inside, so a title longer than the strip is cut with its ellipsis at
    /// its own size (the fit then scales it no larger) and a short one grows until the width stops it.
    /// </summary>
    public void ApplySize(WidgetArea area)
    {
        var room = area.IsMeasured ? Math.Max(0, area.Width - (2 * FamilySpace.Inset)) : double.PositiveInfinity;
        if (!_block.MaxWidth.Equals(room))
        {
            _block.MaxWidth = room;
        }
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

        _fit.Align = _align;
        _block.Show(title, _config.Glyph, _config.Caption, Aligned.Horizontal(_align));
    }
}
