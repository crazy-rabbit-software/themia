using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6714] The Status block (empty, problem, set up): one row of 28 where the content's first row
/// would be — [state glyph 16] 8 [one sentence, Body, secondary, ellipsis, its whole text in the
/// tooltip] 12 [its one action]. A set-up's action is the accent main action; Try again and Open
/// Accounts are standard buttons. Too narrow for both, the sentence goes and the glyph and action
/// stay (<see cref="StatusRules.ShowsSentence"/>). Like a row, it carries the side inset itself.
/// [S-12834] Its row is whole grid cells (<see cref="LineRules.EdgeRow"/>: 30 on the default grid), the sentence's box
/// filling it with its words centred, so the rows a face keeps under it (WR-10) start right under it on a grid line.
/// </summary>
public sealed partial class StatusBlock : Grid
{
    private readonly FontIcon _glyph;
    private readonly TextBlock _sentence;
    private Button? _action;
    private Action? _run;

    public StatusBlock()
    {
        Padding = Family.SideInset;
        VerticalAlignment = VerticalAlignment.Top;

        // [S-12815] The row a card's state stands in is its first row: on the card's top edge, as a list's first row
        // (a view that stands it under something else sets its margin).
        Margin = Themia.App.Widgets.ListEdge.Margin;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _glyph = Family.Glyph(string.Empty, FamilyShape.Icon, ColourRole.TextTertiary);
        _glyph.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);
        _sentence = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
        _sentence.VerticalAlignment = VerticalAlignment.Top;
        SetColumn(_sentence, 1);
        Children.Add(_glyph);
        Children.Add(_sentence);
    }

    /// <summary>
    /// [S-14260] A state's row standing in a list's first row's place, over the list's face (its rows kept empty under it,
    /// WR-10): one of the list's items, as any row is (owner, 2026-11-19, WR-3: "the items dont reach from edge to edge";
    /// docs/WIDGET-RULES.md: "An item's box (its row band, cell, hover area) runs from the card's left edge to its right
    /// edge"). Its box runs edge to edge with the side inset inside it, and it is reported as an item
    /// (<see cref="Themia.App.Widgets.ItemMark"/>), so a list whose card holds one row (its least) and nothing to list is
    /// laid and judged as that one row, its sentence never stretched.
    /// </summary>
    public static StatusBlock Line()
    {
        var row = new StatusBlock();
        Themia.App.Widgets.ItemMark.Mark(row);
        return row;
    }

    /// <summary>Fold to the width the layout offers, in the measure pass.</summary>
    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        if (double.IsFinite(availableSize.Width))
        {
            Fold(availableSize.Width);
        }

        // [S-12834] The row's whole cells; the sentence's words centred in its box, which fills the row.
        var row = double.IsFinite(Height) ? Height : LineRules.EdgeRow(Themia.App.Widgets.ItemGrids.Of(this));
        SectionHeading.Fill(_sentence, row);

        var desired = base.MeasureOverride(availableSize);
        return new Windows.Foundation.Size(desired.Width, row);
    }

    /// <summary>The sentence (its tooltip is the whole of it).</summary>
    public TextBlock Sentence => _sentence;

    /// <summary>The action's button, when there is one.</summary>
    public Button? ActionButton => _action;

    /// <summary>
    /// Say <paramref name="sentence"/> as a <paramref name="kind"/> state, led by <paramref name="glyph"/>
    /// (the widget's own; a problem's is always the warning), with its one action when it has one.
    /// </summary>
    public void Show(StatusKind kind, string glyph, string sentence, string? action = null, Action? run = null)
    {
        var look = StatusRules.Look(kind);
        _glyph.Glyph = kind == StatusKind.Problem ? Themia.App.Shell.Glyphs.Warning : glyph;
        _glyph.Foreground = Family.Brush(look.Glyph);
        _sentence.Text = sentence;
        ToolTipService.SetToolTip(_sentence, sentence);

        if (_action is not null)
        {
            Children.Remove(_action);
            _action = null;
        }

        _run = run;
        if (action is not null && run is not null)
        {
            _action = ControlsBlock.TextButton(action, look.MainAction, () => _run?.Invoke());
            _action.Margin = new Thickness(FamilySpace.ColumnGap, 0, 0, 0);

            // [S-12834] On the row's foot, so what stands under the line starts right under its button too.
            _action.VerticalAlignment = VerticalAlignment.Bottom;
            ToolTipService.SetToolTip(_action, sentence);
            SetColumn(_action, 2);
            Children.Add(_action);
        }

        Visibility = Visibility.Visible;
        InvalidateMeasure();
    }

    public void Hide() => Visibility = Visibility.Collapsed;

    private void Fold(double width)
    {
        var inner = width - Padding.Left - Padding.Right;
        var actionWidth = 0.0;
        if (_action is not null)
        {
            _action.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            actionWidth = _action.DesiredSize.Width - _action.Margin.Left;
        }

        var visibility = width <= 0 || StatusRules.ShowsSentence(inner, actionWidth) ? Visibility.Visible : Visibility.Collapsed;
        if (_sentence.Visibility != visibility)
        {
            _sentence.Visibility = visibility;
        }
    }
}
