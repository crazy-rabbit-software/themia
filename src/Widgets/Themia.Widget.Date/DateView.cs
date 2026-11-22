using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
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
/// [S-10800] [S-10803] Today in its style: the face laid out at the step its Size names, then scaled up as one until its
/// ink fills the card (<see cref="ReadingFit"/>, [S-12808]), so a larger card is a larger date, never a band round it.
/// [S-15804] Each line is held to the language's widest day in the style (WR-15: a running reading never moves): the face is
/// sized and stood by that template, today's text drawn in its box at the widget's alignment, so the date keeps its size
/// and place from one day to the next. The family's inset at the sides and the foot (the card gives the top); a mate
/// joined under it takes the foot ([S-12483]). Turns over within a minute of midnight. [S-12833] A smaller card scales it
/// down as one to its main reading at the legibility floor, the page's weekday and month giving way first.
/// </summary>
internal sealed partial class DateView : Grid, IWidgetView, IWidgetAligned, IWidgetSizeAware
{
    private readonly Held _line = new(FamilyType.Big40, ColourRole.TextPrimary);
    private readonly Held _weekday = new(FamilyType.Body, ColourRole.TextSecondary);
    private readonly Held _number = new(FamilyType.Hero68, ColourRole.TextPrimary);
    private readonly Held _month = new(FamilyType.Body, ColourRole.TextSecondary);
    private readonly ReadingFit _fit = new() { Margin = default };

    /// <summary>
    /// [S-15700] The page's weekday and month, each a part of the fit in a box of its own that gives way as a whole: each
    /// line stands centred on its own ink (a "22" whose figures ink off its box's middle), not the three on the widest box.
    /// </summary>
    private readonly Border _weekdayBox;
    private readonly Border _monthBox;
    private DateConfig _config = new();
    private ContentAlign _align = ContentAlign.Center;
    private WidgetArea _area;
    private Ticker? _ticker;
    private IDisposable? _subscription;

    /// <summary>[S-11850] The sample's day; null on the desktop (today).</summary>
    private DateOnly? _frozen;

    public DateView()
    {
        var under = FamilyType.Under(DateLayout.NumberStep).FontSize;
        _weekdayBox = _fit.GivesWayBox(_weekday.Cell, under);
        _monthBox = _fit.GivesWayBox(_month.Cell, under);
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

    /// <summary>Build the face for the style at its step, held to the language's widest day; the fit scales it to the card.</summary>
    private void Lay()
    {
        // [S-12833] The page's weekday and month give way where the card would draw them under the floor, decided by the fit
        // on the face as it is drawn (today's words, measured), as every block's.
        _fit.Face = BlockFace.Of(DateLayout.PartSizes(_config));
        _fit.Parts.Clear();

        _fit.Align = _align;
        var locale = Strings.Current;
        if (_config.Style == DateWidgetStyle.Page)
        {
            var number = DateLayout.NumberStep;
            var held = DateLayout.HeldPage(locale);
            _number.Lay(number, held.Number, _align);
            _weekday.Lay(FamilyType.Under(number), held.Weekday, _align);
            _month.Lay(FamilyType.Under(number), held.Month, _align);

            _fit.Parts.Add(_weekdayBox);
            _fit.Parts.Add(_number.Cell);
            _fit.Parts.Add(_monthBox);
        }
        else
        {
            _line.Lay(DateLayout.LineStep, DateLayout.HeldLine(_config.Style, locale), _align);
            _fit.Parts.Add(_line.Cell);
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
            _weekday.Shown.Text = weekday;
            _number.Shown.Text = number;
            _month.Shown.Text = month;
        }
        else
        {
            _line.Shown.Text = DateLayout.Line(_config.Style, locale, day);
        }

        ToolTipService.SetToolTip(this, DateLayout.Full(locale, day));

        // [S-14201] Another day may ink lower in the same box (a descender): the fit places the face on its ink again,
        // which XAML does not do while its size stays.
        _fit.Refit();
    }

    /// <summary>
    /// [S-15804] A line held to its template (WR-15): today's text drawn over the widest day's, unseen, in one cell as wide as
    /// the wider, so the fit sizes and stands the line by the template whatever day it shows (<see cref="HoldMark"/>: the
    /// layout dump and the gap check judge its sides there, as a clock's held figures).
    /// </summary>
    private sealed class Held
    {
        public Held(TypeStep step, ColourRole role)
        {
            Shown = Family.Text(step, role);
            Template = Family.Text(step, role);
            Template.Opacity = 0;
            Template.IsHitTestVisible = false;
            AutomationProperties.SetAccessibilityView(Template, AccessibilityView.Raw);
            foreach (var text in new[] { Shown, Template })
            {
                text.TextWrapping = TextWrapping.NoWrap;
            }

            Cell = new Grid { Children = { Template, Shown } };
            HoldMark.Mark(Cell);
        }

        /// <summary>Today's text.</summary>
        public TextBlock Shown { get; }

        /// <summary>The widest day's text, unseen: the cell's size and the ink it stands by.</summary>
        public TextBlock Template { get; }

        /// <summary>The part the fit lays.</summary>
        public Grid Cell { get; }

        /// <summary>Its step, its template and where today's text stands in it.</summary>
        public void Lay(TypeStep step, string template, ContentAlign align)
        {
            Family.Step(Shown, step);
            Family.Step(Template, step);
            Template.Text = template;
            var across = Aligned.Horizontal(align);
            var text = Aligned.Text(align);
            foreach (var line in new[] { Shown, Template })
            {
                line.HorizontalAlignment = across;
                line.TextAlignment = text;
            }
        }
    }
}
