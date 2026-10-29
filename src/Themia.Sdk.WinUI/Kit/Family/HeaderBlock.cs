using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6710] The Header block: [glyph 16] 8 [name, BodyStrong, secondary, ellipsis] … [trailing], one
/// line of 20, the card's header (it sits in the card's <see cref="Family.HeaderInset"/>). The
/// trailing slot is the view's own element: a <see cref="HeaderTrail"/> of family parts
/// (<see cref="Button"/>, <see cref="Count"/>, a problem glyph, a ring, a label) folds by
/// <see cref="HeaderRules"/>; a lone label ("by CPU", a <c>HeaderTrail</c> of the exchange rates')
/// gives way to the name by [S-655]'s rule.
/// </summary>
public sealed partial class HeaderBlock : Grid
{
    private readonly TextBlock _title;
    private readonly FrameworkElement? _trailing;

    public HeaderBlock(string glyph, TextBlock title, FrameworkElement? trailing = null)
    {
        ArgumentNullException.ThrowIfNull(title);

        _title = title;
        _trailing = trailing;

        var icon = Family.Glyph(glyph);
        icon.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);

        Family.Step(title, FamilyType.BodyStrong);
        title.Foreground = Family.Brush(ColourRole.TextSecondary);
        title.VerticalAlignment = VerticalAlignment.Center;

        // [S-640] A name that does not fit ends in an ellipsis: a Grid does not clip a child to its cell.
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.TextWrapping = TextWrapping.NoWrap;

        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        SetColumn(icon, 0);
        SetColumn(title, 1);
        Children.Add(icon);
        Children.Add(title);

        if (trailing is null)
        {
            return;
        }

        trailing.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(trailing, 2);
        Children.Add(trailing);

        // [S-655] A label that only informs gives way to the title on a narrow tile (HeaderFit),
        // through MaxWidth, never Visibility, which views already set on their own labels. A label
        // beside the kit's header extras comes in a HeaderTrail: the rule reaches it there too.
        var label = trailing switch
        {
            TextBlock one => one,
            Themia.App.Widgets.HeaderTrail trail => trail.Label,
            _ => null,
        };

        if (label is not null)
        {
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            SizeChanged += (_, args) => label.MaxWidth = HeaderFit.TrailingLabelWidth(args.NewSize.Width, HeaderRules.Leading);
        }
        else if (trailing is HeaderParts parts)
        {
            parts.Margin = new Thickness(FamilySpace.S2, 0, 0, 0);
            _parts = parts;
        }
    }

    private readonly HeaderParts? _parts;

    /// <summary>[S-6710] Fold the trailing parts to the width the layout offers, in the measure pass.</summary>
    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        if (_parts is not null && double.IsFinite(availableSize.Width))
        {
            _parts.Fold(availableSize.Width, NameWidth());
        }

        return base.MeasureOverride(availableSize);
    }

    /// <summary>The name's whole width, as it would stand with room to spare.</summary>
    private double NameWidth()
    {
        var copy = new TextBlock { Text = _title.Text, FontSize = _title.FontSize, FontWeight = _title.FontWeight, FontFamily = _title.FontFamily };
        copy.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        return copy.DesiredSize.Width;
    }

    /// <summary>
    /// [S-6710] A header button: 24 × 24, a 16 glyph in the secondary role, the one radius, the subtle
    /// fills on hover and press (a control, never the item layer's). It stands 2 over the header's line
    /// either way, so a header with buttons is still one line of 20 and its name does not move.
    /// </summary>
    public static Border Button(string glyph, string tooltip, Action run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var element = new Border
        {
            Child = Family.Glyph(glyph),
            Width = FamilyShape.HeaderButton,
            Height = FamilyShape.HeaderButton,
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
    /// never the accent. Over an image (a cell's badge) it takes the solid fill instead.
    /// </summary>
    public static Border Count(TextBlock text, bool overImage = false)
    {
        ArgumentNullException.ThrowIfNull(text);

        Family.Step(text, FamilyType.Caption);
        text.Foreground = Family.Brush(ColourRole.TextPrimary);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;

        return new Border
        {
            Child = text,
            Height = FamilyShape.PillHeight,
            MinWidth = FamilyShape.PillHeight,
            CornerRadius = Family.Round(FamilyShape.PillHeight, FamilyShape.PillHeight),
            Padding = new Thickness(FamilyShape.PillPadding, 0, FamilyShape.PillPadding, 0),
            Background = Family.Brush(overImage ? ColourRole.Solid : ColourRole.Subtle),
            BorderBrush = overImage ? Family.Brush(ColourRole.ControlStroke) : null,
            BorderThickness = new Thickness(overImage ? FamilyShape.Hairline : 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
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
    public static TextBlock Label(string text) => Family.Text(FamilyType.Caption, ColourRole.TextSecondary, text);
}

/// <summary>
/// [S-6710] A header's trailing parts, in their order (problem, ring, label, buttons, count), folded
/// to the header's width by <see cref="HeaderRules"/>: the label first, then the buttons from the
/// last, then the count; the name is cut only when nothing is left to give way.
/// </summary>
public sealed partial class HeaderParts : StackPanel
{
    private readonly List<(HeaderPartKind Kind, FrameworkElement Element)> _parts = [];

    public HeaderParts()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>Add a part of <paramref name="kind"/>; returns it.</summary>
    public T Add<T>(HeaderPartKind kind, T element)
        where T : FrameworkElement
    {
        ArgumentNullException.ThrowIfNull(element);
        _parts.Add((kind, element));
        Children.Add(element);
        Space();
        return element;
    }

    /// <summary>Show the parts that fit a header <paramref name="width"/> wide whose name needs <paramref name="nameWidth"/>.</summary>
    public void Fold(double width, double nameWidth)
    {
        var parts = _parts.Select(p => new HeaderPart(p.Kind, Measured(p.Element))).ToList();
        var shown = HeaderRules.Shown(width, nameWidth, parts);
        for (var i = 0; i < _parts.Count; i++)
        {
            var visibility = shown[i] ? Visibility.Visible : Visibility.Collapsed;
            if (_parts[i].Element.Visibility != visibility)
            {
                _parts[i].Element.Visibility = visibility;
            }
        }

        Space();
    }

    /// <summary>The gaps between the shown parts, and the last button's glyph on the inset.</summary>
    private void Space()
    {
        HeaderPartKind? previous = null;
        FrameworkElement? last = null;
        var margins = new Dictionary<FrameworkElement, Thickness>();
        foreach (var (kind, element) in _parts)
        {
            if (element.Visibility != Visibility.Visible)
            {
                continue;
            }

            margins[element] = new Thickness(previous is { } before ? HeaderRules.Gap(before, kind) : 0, kind == HeaderPartKind.Button ? -FamilySpace.ButtonOverflow : 0, 0, kind == HeaderPartKind.Button ? -FamilySpace.ButtonOverflow : 0);
            previous = kind;
            last = element;
        }

        if (last is not null && previous == HeaderPartKind.Button)
        {
            var m = margins[last];
            margins[last] = new Thickness(m.Left, m.Top, -HeaderRules.ButtonOverhang, m.Bottom);
        }

        // Set only what changed: this runs in the header's measure pass.
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
