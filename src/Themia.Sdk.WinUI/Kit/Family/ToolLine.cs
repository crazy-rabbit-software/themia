using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-12481] [S-6710] A tool line: [label, Body, secondary, ellipsis] … [trailing], one line of 20, the first
/// line of a widget's own content where its kind offers tools there: the calendar's month and its arrows, a
/// folder's way back and up, the screen time's day and its arrows, the note's formatting bar. Never a card's
/// header and never the widget's name (no card has a header, [S-12480]; a title is a Header widget of its
/// own, [S-9600]): the label says what the tools act on now ("November 2026", "Documents"). The view lays it
/// in its own first row at its side inset, with <see cref="FamilySpace.HeaderGap"/> under it. With no label
/// and no part to show it is collapsed and takes no room. The trailing <see cref="LineParts"/> of family
/// parts (<see cref="Button"/>, <see cref="Count"/>, a problem glyph, a ring, a label) fold by
/// <see cref="LineRules"/>.
/// </summary>
public sealed partial class ToolLine : Grid
{
    private readonly TextBlock _label;

    public ToolLine()
    {
        _label = Family.Text(FamilyType.Body, ColourRole.TextSecondary, string.Empty);
        _label.VerticalAlignment = VerticalAlignment.Center;

        // [S-640] A label that does not fit ends in an ellipsis: a Grid does not clip a child to its cell.
        _label.TextTrimming = TextTrimming.CharacterEllipsis;
        _label.TextWrapping = TextWrapping.NoWrap;

        Height = FamilySpace.HeaderLine;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Children.Add(_label);

        Parts.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(Parts, 1);
        Children.Add(Parts);
        Parts.Changed += Decide;
        Decide();
    }

    /// <summary>The line's trailing parts: the view adds its buttons, labels and state here.</summary>
    public LineParts Parts { get; } = new();

    /// <summary>What the tools act on now ("November 2026"); empty for none.</summary>
    public string Label
    {
        get => _label.Text;
        set
        {
            if (_label.Text != (value ?? string.Empty))
            {
                _label.Text = value ?? string.Empty;
                Decide();
            }
        }
    }

    /// <summary>The line stands while it has a label or a part to show; collapsed it takes no room.</summary>
    private void Decide() => Visibility = _label.Text.Length > 0 || Parts.Any ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>[S-6710] Fold the trailing parts to the width the layout offers, in the measure pass.</summary>
    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        if (double.IsFinite(availableSize.Width))
        {
            Parts.Fold(availableSize.Width, LabelWidth());
        }

        return base.MeasureOverride(availableSize);
    }

    /// <summary>The label's whole width, as it would stand with room to spare.</summary>
    private double LabelWidth()
    {
        if (_label.Text.Length == 0)
        {
            return 0;
        }

        var copy = new TextBlock { Text = _label.Text, FontSize = _label.FontSize, FontWeight = _label.FontWeight, FontFamily = _label.FontFamily };
        copy.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        return copy.DesiredSize.Width;
    }

    /// <summary>
    /// [S-6710] A line button: 24 × 24, a 16 glyph in the secondary role, the one radius, the subtle fills
    /// on hover and press (a control, never the item layer's). It stands 2 over the line either way, so a
    /// line with buttons is still one line of 20 and its label does not move.
    /// </summary>
    public static Border Button(string glyph, string tooltip, Action run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var element = new Border
        {
            Child = Family.Glyph(glyph),
            Width = FamilyShape.LineButton,
            Height = FamilyShape.LineButton,
            CornerRadius = Family.Radius,
            Background = Family.Brush(ColourRole.Clear),
            Margin = new Thickness(0, -FamilySpace.ButtonOverflow, 0, -FamilySpace.ButtonOverflow),
        };

        ToolTipService.SetToolTip(element, tooltip);
        AutomationProperties.SetName(element, tooltip);
        element.PointerEntered += (_, _) => element.Background = Family.Brush(ColourRole.Subtle);
        element.PointerExited += (_, _) => element.Background = Family.Brush(ColourRole.Clear);
        element.PointerCanceled += (_, _) => element.Background = Family.Brush(ColourRole.Clear);
        element.PointerPressed += (_, _) => element.Background = Family.Brush(ColourRole.SubtlePressed);
        element.Tapped += (_, args) =>
        {
            args.Handled = true;
            run();
        };

        return element;
    }

    /// <summary>
    /// [S-6706] The count pill: Caption in the primary role on the subtle fill, round, 16 tall — neutral,
    /// never the accent.
    /// </summary>
    public static Border Count(TextBlock text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Family.Step(text, FamilyType.Caption);
        text.Foreground = Family.Brush(ColourRole.TextPrimary);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;

        var pill = new Border
        {
            Child = text,
            Height = FamilyShape.PillHeight,
            MinWidth = FamilyShape.PillHeight,
            CornerRadius = Family.Round(FamilyShape.PillHeight, FamilyShape.PillHeight),
            Background = Family.Brush(ColourRole.Subtle),
            VerticalAlignment = VerticalAlignment.Center,
        };

        // The pill is its text and 6 either side ([S-6706]), as a size the text sets rather than a
        // padding: a padding of 6 is off the space scale the render walk holds every padding to.
        text.SizeChanged += (_, args) => pill.Width = Math.Max(FamilyShape.PillHeight, args.NewSize.Width + (2 * FamilyShape.PillPadding));
        return pill;
    }

    /// <summary>[S-6710] The problem glyph: the caution warning, its lines in the tooltip.</summary>
    public static FontIcon Problem(string lines)
    {
        var icon = Family.Glyph(Themia.App.Shell.Glyphs.Warning, FamilyShape.Icon, ColourRole.Caution);
        ToolTipService.SetToolTip(icon, lines);
        return icon;
    }

    /// <summary>[S-6710] The busy ring: 16, while a fetch is out.</summary>
    public static ProgressRing Busy() => new()
    {
        Width = FamilyShape.Icon,
        Height = FamilyShape.Icon,
        IsActive = true,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>[S-6710] An informing label (an age, "by CPU"): Caption, secondary, the first part to give way.</summary>
    public static TextBlock Note(string text) => Family.Text(FamilyType.Caption, ColourRole.TextSecondary, text);
}

/// <summary>
/// [S-6710] A tool line's trailing parts, in their order (problem, ring, label, buttons, count), folded
/// to the line's width by <see cref="LineRules"/>: the label first, then the buttons from the
/// last, then the count; the name is cut only when nothing is left to give way.
/// </summary>
public sealed partial class LineParts : StackPanel
{
    private readonly List<(LinePartKind Kind, FrameworkElement Element)> _parts = [];

    public LineParts()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>A part came, went or now has something to say, or nothing: the line decides again whether it stands.</summary>
    public event Action? Changed;

    /// <summary>Whether any part has something to say now.</summary>
    public bool Any => _parts.Exists(p => !_unwanted.Contains(p.Element));

    /// <summary>Add a part of <paramref name="kind"/> in its place in the line's order; returns it.</summary>
    public T Add<T>(LinePartKind kind, T element)
        where T : FrameworkElement
    {
        ArgumentNullException.ThrowIfNull(element);
        var at = LineRules.InsertAt([.. _parts.Select(p => p.Kind)], kind);
        var child = at < _parts.Count ? Children.IndexOf(_parts[at].Element) : -1;
        _parts.Insert(at, (kind, element));
        if (child >= 0)
        {
            Children.Insert(child, element);
        }
        else
        {
            Children.Add(element);
        }

        Space();
        Changed?.Invoke();
        return element;
    }

    private TextBlock? _about;

    /// <summary>
    /// [S-2260] The informing label that says where or which the widget is about (a place, an adapter, a
    /// host), beside the name that says what it is: added the first time there is something to say,
    /// not wanted while there is none (<see cref="LineRules.About"/>). The first part to give way.
    /// </summary>
    public void SetAbout(string? text)
    {
        var shown = LineRules.About(text);
        if (_about is null)
        {
            if (shown is null)
            {
                return;
            }

            _about = Add(LinePartKind.Label, ToolLine.Note(shown));
        }
        else if (_about.Text != (shown ?? string.Empty))
        {
            _about.Text = shown ?? string.Empty;
            InvalidateMeasure();
            (Parent as UIElement)?.InvalidateMeasure();
        }

        Want(_about, shown is not null);
    }

    /// <summary>[S-2260] Whether the label of <see cref="SetAbout"/> has something to say now.</summary>
    public bool Informs => _about is not null && !_unwanted.Contains(_about);

    /// <summary>
    /// Whether <paramref name="element"/> (one of the parts) has anything to say now: a problem glyph only
    /// while there is a problem. A part that is not wanted is collapsed and takes no room; the fold
    /// decides among the wanted ones.
    /// </summary>
    public void Want(FrameworkElement element, bool wanted)
    {
        if (wanted ? _unwanted.Remove(element) : _unwanted.Add(element))
        {
            // Shown at once (the line's fold decides again in its measure pass).
            element.Visibility = wanted ? Visibility.Visible : Visibility.Collapsed;
            Space();
            InvalidateMeasure();
            (Parent as UIElement)?.InvalidateMeasure();
            Changed?.Invoke();
        }
    }

    private readonly HashSet<FrameworkElement> _unwanted = [];

    /// <summary>Show the parts that fit a line <paramref name="width"/> wide whose label needs <paramref name="nameWidth"/>.</summary>
    public void Fold(double width, double nameWidth)
    {
        // Only the wanted parts are folded: one with nothing to say takes no room and no gap.
        var wanted = _parts.Select(p => !_unwanted.Contains(p.Element)).ToList();
        var shown = LineRules.Shown(width, nameWidth, [.. _parts.Select((p, i) => new LinePart(p.Kind, wanted[i] ? Measured(p.Element) : 0))], wanted);
        var any = false;
        for (var i = 0; i < _parts.Count; i++)
        {
            var element = _parts[i].Element;
            var visible = shown[i];
            any |= visible;
            var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (element.Visibility != visibility)
            {
                element.Visibility = visibility;
            }
        }

        // With every part folded away the gap before them goes too, as LineRules.Needs counts it.
        var margin = new Thickness(any ? FamilySpace.S2 : 0, 0, 0, 0);
        if (Margin != margin)
        {
            Margin = margin;
        }

        Space();
    }

    /// <summary>The gaps between the shown parts, and the last button's glyph on the inset.</summary>
    private void Space()
    {
        LinePartKind? previous = null;
        FrameworkElement? last = null;
        var margins = new Dictionary<FrameworkElement, Thickness>();
        foreach (var (kind, element) in _parts)
        {
            if (element.Visibility != Visibility.Visible)
            {
                continue;
            }

            margins[element] = new Thickness(previous is { } before ? LineRules.Gap(before, kind) : 0, kind == LinePartKind.Button ? -FamilySpace.ButtonOverflow : 0, 0, kind == LinePartKind.Button ? -FamilySpace.ButtonOverflow : 0);
            previous = kind;
            last = element;
        }

        if (last is not null && previous == LinePartKind.Button)
        {
            var m = margins[last];
            margins[last] = new Thickness(m.Left, m.Top, -LineRules.ButtonOverhang, m.Bottom);
        }

        // Set only what changed: this runs in the line's measure pass.
        foreach (var (element, margin) in margins)
        {
            if (element.Margin != margin)
            {
                element.Margin = margin;
            }
        }
    }

    /// <summary>A part's width, measured while it shows and remembered while it is folded away.</summary>
    private double Measured(FrameworkElement element)
    {
        if (!double.IsNaN(element.Width))
        {
            return element.Width;
        }

        if (element.Visibility == Visibility.Visible)
        {
            element.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            _widths[element] = Math.Max(0, element.DesiredSize.Width - element.Margin.Left - element.Margin.Right);
        }

        return _widths.TryGetValue(element, out var width) ? width : 0;
    }

    private readonly Dictionary<FrameworkElement, double> _widths = [];
}
