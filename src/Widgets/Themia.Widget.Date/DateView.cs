using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Date;

/// <summary>
/// [S-10800] [S-10803] Today in its style: the face laid out at the step its Size names, in the box its sizing
/// made for the language's widest day, then scaled up as one until its ink fills the card (<see cref="InkFit"/>, [S-12808]), so a
/// larger card is a larger date, never a band round it. The family's inset at the sides and the foot (the card gives the
/// top); a mate joined under it takes the foot ([S-12483]). Turns over within a minute of midnight. [S-12833] A smaller card
/// scales it down as one to its main reading at the legibility floor, the page's weekday and month giving way first
/// (<see cref="DateLayout.Fit"/>).
/// </summary>
internal sealed partial class DateView : Grid, IWidgetView, IWidgetAligned, IWidgetSizeAware
{
    private readonly TextBlock _line = Family.Text(FamilyType.Big40);
    private readonly TextBlock _weekday = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
    private readonly TextBlock _number = Family.Text(FamilyType.Hero68);
    private readonly TextBlock _month = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
    private readonly StackPanel _face = new();
    private readonly InkFit _fit = new();
    private DateConfig _config = new();
    private ContentAlign _align = ContentAlign.Center;
    private WidgetArea _area;
    private Ticker? _ticker;
    private IDisposable? _subscription;

    /// <summary>[S-11850] The sample's day; null on the desktop (today).</summary>
    private DateOnly? _frozen;

    public DateView()
    {
        foreach (var text in new[] { _line, _weekday, _number, _month })
        {
            text.TextWrapping = TextWrapping.NoWrap;
        }

        _fit.Child = _face;
        Children.Add(_fit);
        Padding = Family.SideInset with { Bottom = FamilySpace.Inset };
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        _subscription?.Dispose();
        _subscription = ticker.Subscribe(TimeSpan.FromMinutes(1), Render);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as DateConfig ?? new DateConfig();
        Lay();
    }

    /// <summary>[S-6583] The face at the start, the middle or the end of the card.</summary>
    public void ApplyAlign(ContentAlign align)
    {
        _align = align;
        Lay();
    }

    /// <summary>[S-12833] The card's word on its room: which parts the face keeps in it.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
        _ticker = null;
    }

    /// <summary>[S-11850] Hold the sample day.</summary>
    internal void Freeze(DateOnly day)
    {
        _frozen = day;
        Lay();
    }

    private void OnLanguage(Locale locale) => Lay();

    /// <summary>Build the face for the style and Size at its natural box; the viewbox scales it to the card.</summary>
    private void Lay()
    {
        var locale = Strings.Current;

        // [S-12833] The page's weekday and month give way where the card would draw them under the floor.
        var fit = _area.IsMeasured
            ? DateLayout.Fit(_area.Width - (2 * FamilySpace.Inset), _area.Height - FamilySpace.Inset, _config, locale)
            : FaceFit.All;
        var lines = DateLayout.Lines(_config, fit.Floor);
        var face = DateLayout.Face(_config, locale, fit.Floor);
        _fit.Most = fit.Most;
        _face.Children.Clear();
        _face.Width = face.Width;
        _face.Height = face.Height;
        _fit.Align = Aligned.Horizontal(_align);
        var text = _align switch
        {
            ContentAlign.Center => TextAlignment.Center,
            ContentAlign.Right => TextAlignment.Right,
            _ => TextAlignment.Left,
        };

        if (_config.Style == DateWidgetStyle.Page)
        {
            var number = DateLayout.NumberStep;
            Family.Step(_number, number);
            Family.Step(_weekday, FamilyType.Under(number));
            Family.Step(_month, FamilyType.Under(number));
            foreach (var part in lines ? new[] { _weekday, _number, _month } : [_number])
            {
                part.TextAlignment = text;
                _face.Children.Add(part);
            }
        }
        else
        {
            Family.Step(_line, DateLayout.LineStep);
            _line.TextAlignment = text;
            _face.Children.Add(_line);
        }

        Render();
    }

    private void Render()
    {
        var locale = Strings.Current;
        var day = _frozen ?? DateOnly.FromDateTime(DateTime.Now);
        if (_config.Style == DateWidgetStyle.Page)
        {
            var (weekday, number, month) = DateLayout.Page(locale, day);
            _weekday.Text = weekday;
            _number.Text = number;
            _month.Text = month;
        }
        else
        {
            _line.Text = DateLayout.Line(_config.Style, locale, day);
        }

        ToolTipService.SetToolTip(this, DateLayout.Full(locale, day));
    }
}
