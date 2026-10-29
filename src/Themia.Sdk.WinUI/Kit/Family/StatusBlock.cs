using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6714] The Status block (empty, problem, set up): one row of 32 where the content's first row
/// would be — [state glyph 16] 8 [one sentence, Body, secondary, ellipsis, its whole text in the
/// tooltip] 12 [its one action]. A set-up's action is the accent main action; Try again and Open
/// Accounts are standard buttons. Too narrow for both, the sentence goes and the glyph and action
/// stay (<see cref="StatusRules.ShowsSentence"/>). Like a row, it carries the side inset itself.
/// </summary>
public sealed partial class StatusBlock : Grid
{
    private readonly FontIcon _glyph;
    private readonly TextBlock _sentence;
    private Button? _action;
    private Action? _run;

    public StatusBlock()
    {
        Height = FamilyShape.Row1;
        Padding = Family.SideInset;
        VerticalAlignment = VerticalAlignment.Top;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _glyph = Family.Glyph(string.Empty, FamilyShape.Icon, ColourRole.TextTertiary);
        _glyph.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);
        _sentence = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
        _sentence.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(_sentence, 1);
        Children.Add(_glyph);
        Children.Add(_sentence);
    }

    /// <summary>Fold to the width the layout offers, in the measure pass.</summary>
    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        if (double.IsFinite(availableSize.Width))
        {
            Fold(availableSize.Width);
        }

        return base.MeasureOverride(availableSize);
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
