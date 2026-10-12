using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// The Windows 11 design language, as the few values and helpers a widget actually needs.
/// <para>
/// This exists because the port had inherited the original web app's look — arbitrary font sizes,
/// arbitrary opacities standing in for text colours, 6px and 7px paddings, a divider between every
/// row — and the result read as a web dashboard that happened to be running on Windows. Fluent is
/// specific: a type ramp, a 4px spacing grid, two corner radii, named text and fill colours, and a
/// list row of a known height. Matching it is mostly a matter of stopping inventing values.
/// </para>
/// </summary>
internal static class Fluent
{
    // ---- spacing ---------------------------------------------------------------------------

    /// <summary>The 4px grid Fluent lays out on. Every gap a widget uses is one of these.</summary>
    public const double Space1 = 4;

    public const double Space2 = 8;

    public const double Space3 = 12;

    /// <summary>The standard content inset for a card.</summary>
    public const double Space4 = 16;

    public const double Space5 = 20;

    public const double Space6 = 24;

    // ---- shape -----------------------------------------------------------------------------

    /// <summary>Fluent's control radius. Buttons, text fields, row highlights.</summary>
    public const double ControlRadius = 4;

    /// <summary>Fluent's overlay radius. Cards, flyouts, dialogs.</summary>
    public const double OverlayRadius = 8;

    /// <summary>
    /// The height of a standard Windows 11 list row, and of the compact variant a small tile uses.
    /// <para>
    /// Rows are not sized to a fraction of the tile: a list whose rows grow with the window is a web
    /// idea, and next to Explorer or Settings it reads as the wrong application. A tile shows as many
    /// rows of the right height as it has room for.
    /// </para>
    /// </summary>
    public const double RowHeight = 40;

    public const double RowHeightCompact = 32;

    // ---- type ------------------------------------------------------------------------------

    /// <summary>Apply a ramp step to a text block: size, line height and weight together.</summary>
    public static void Apply(this TextBlock text, TypeStep step)
    {
        text.FontSize = step.FontSize;
        text.LineHeight = step.LineHeight;
        text.FontWeight = step.SemiBold ? FontWeights.SemiBold : FontWeights.Normal;
    }

    /// <summary>A text block on a ramp step, in one of the named text colours.</summary>
    public static TextBlock Text(TypeStep step, string colorKey = "TextFillColorPrimaryBrush")
    {
        var text = new TextBlock
        {
            Foreground = FluentLayout.Themed(colorKey, Colors.White),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
        };

        text.Apply(step);
        return text;
    }

    /// <summary>
    /// A widget's header: a 16px icon and a Body Strong title in the secondary text colour, the
    /// shape Windows uses at the top of a pane.
    /// </summary>
    public static Grid Header(string glyph, TextBlock title, FrameworkElement? trailing = null)
    {
        var icon = Icon(glyph);
        icon.Margin = new Thickness(0, 0, Space2, 0);

        title.Apply(FluentType.BodyStrong);
        title.Foreground = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);
        title.VerticalAlignment = VerticalAlignment.Center;

        // [S-640] A title that does not fit ends in an ellipsis. A Grid does not clip a child to
        // its cell, so an untrimmed title on a narrow tile was cut mid-word ("Countdow") or ran
        // under the trailing element ("Proc" with "by CPU" drawn over it).
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.TextWrapping = TextWrapping.NoWrap;

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        Grid.SetColumn(icon, 0);
        Grid.SetColumn(title, 1);
        grid.Children.Add(icon);
        grid.Children.Add(title);

        if (trailing is not null)
        {
            trailing.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(trailing, 2);
            grid.Children.Add(trailing);

            // [S-655] A label that only informs ("Just now", "by CPU") gives way to the title on a
            // narrow tile; see HeaderFit. Through MaxWidth, never Visibility, which several views
            // already set on their own label for their own reasons. Commands are left alone: a
            // button is something the user needs, and the view sheds those itself.
            if (trailing is TextBlock label)
            {
                label.TextTrimming = TextTrimming.CharacterEllipsis;
                var leading = IconSize + Space2;
                grid.SizeChanged += (_, args) => label.MaxWidth = HeaderFit.TrailingLabelWidth(args.NewSize.Width, leading);
            }
        }

        return grid;
    }

    /// <summary>
    /// A list row, in the Windows 11 shape: a fixed height, the standard inset as its own padding so
    /// it spans the full width of the widget, and a subtle fill under the pointer.
    /// <para>
    /// No divider. Windows 11 lists separate rows with space and hover, not with a rule between every
    /// one — Settings, Explorer and the Widgets board all do it this way, and a ruled list is the
    /// single clearest tell of a web table.
    /// </para>
    /// </summary>
    public static Border Row(FrameworkElement content, bool compact = false)
    {
        var row = new Border
        {
            Child = content,
            MinHeight = compact ? RowHeightCompact : RowHeight,
            Padding = new Thickness(Space4, 0, Space4, 0),
            Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent),
        };

        content.VerticalAlignment = VerticalAlignment.Center;
        return row;
    }

    /// <summary>The fill a row takes under the pointer.</summary>
    public static Brush RowHover() => FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Gray);

    /// <summary>A 1px rule. Every tick, hairline and divider in the app is this thickness.</summary>
    public const double Hairline = 1;

    /// <summary>The icon size Windows pairs with body text.</summary>
    public const double IconSize = 16;

    /// <summary>Fluent draws a chart line at 2px: 1px reads as a hairline artefact, 3px as a border.</summary>
    public const double ChartStroke = 2;

    /// <summary>
    /// The alpha of an area wash under a chart line.
    /// <para>
    /// The one place an opacity is legitimate rather than a colour stand-in: it is applied to the
    /// <i>shape</i>, never to the shared accent brush, which is the same object every other accent
    /// surface in the app paints with.
    /// </para>
    /// </summary>
    public const double ChartWash = 0.16;

    // ---- breakpoints -----------------------------------------------------------------------

    /// <summary>
    /// Below this height a list uses compact rows.
    /// <para>
    /// Named because four widgets each invented their own threshold and no two agreed, which is how
    /// a desktop of widgets ends up with three different row heights on it.
    /// </para>
    /// </summary>
    public const double CompactBelow = RowHeight * 5;

    /// <summary>Below this height the header is dropped: the content is worth more than its label.</summary>
    public const double HeaderHidesBelow = 96;

    // ---- pieces ----------------------------------------------------------------------------

    /// <summary>A 16px icon in one of the named text colours.</summary>
    public static FontIcon Icon(string glyph, string colorKey = "TextFillColorSecondaryBrush", double size = IconSize)
        => UiLanguage.Mirrored(new FontIcon
        {
            Glyph = glyph,
            FontSize = size,
            Foreground = FluentLayout.Themed(colorKey, Colors.Gray),
            VerticalAlignment = VerticalAlignment.Center,
        });

    /// <summary>An accent pill, the shape Windows uses for an unread or overflow count.</summary>
    public static Border Badge(TextBlock content)
    {
        content.Apply(FluentType.Caption);
        content.Foreground = FluentLayout.Themed("TextOnAccentFillColorPrimaryBrush", Colors.White);
        content.HorizontalAlignment = HorizontalAlignment.Center;

        return new Border
        {
            Child = content,
            Background = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue),
            CornerRadius = new CornerRadius(OverlayRadius),
            Padding = new Thickness(Space2, 0, Space2, 0),
            MinWidth = Space5,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>
    /// The centred glyph-over-a-line shape Windows uses when a list has nothing in it. The icon is
    /// dropped on a tile too short to carry it, because a tile showing only an icon says less than
    /// one showing only the sentence.
    /// </summary>
    public static StackPanel EmptyState(string glyph, TextBlock message, out FontIcon icon)
    {
        icon = Icon(glyph, "TextFillColorTertiaryBrush", Space6);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.Margin = new Thickness(0, 0, 0, Space2);

        message.Apply(FluentType.Body);
        message.Foreground = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);
        message.TextAlignment = TextAlignment.Center;
        message.TextWrapping = TextWrapping.Wrap;

        // [S-642] The panel sheds the glyph, then lines, on a box too short for both; see
        // EmptyStatePanel. Still a StackPanel to every caller.
        return new EmptyStatePanel(icon, message)
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(Space4, 0, Space4, 0),
        };
    }

    /// <summary>
    /// Light a row while the pointer is over it, and reliably put it back.
    /// <para>
    /// Four events, not one: <c>PointerExited</c> alone misses the cases where the pointer leaves
    /// without one — the list scrolls under it, the widget is torn down mid-hover, a drag takes the
    /// capture — each of which strands a row lit. Dispose the result to unhook; a view's
    /// <c>Detach</c> must, or the handlers outlive the row.
    /// </para>
    /// </summary>
    public static IDisposable Hover(Border row) => new HoverHandler(row);

    private sealed class HoverHandler : IDisposable
    {
        private readonly Border _row;
        private bool _disposed;

        public HoverHandler(Border row)
        {
            _row = row;
            _row.PointerEntered += OnEntered;
            _row.PointerExited += OnGone;
            _row.PointerCaptureLost += OnGone;
            _row.PointerCanceled += OnGone;
        }

        private void OnEntered(object sender, PointerRoutedEventArgs args) => _row.Background = RowHover();

        private void OnGone(object sender, PointerRoutedEventArgs args) => _row.Background = RowRest();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _row.PointerEntered -= OnEntered;
            _row.PointerExited -= OnGone;
            _row.PointerCaptureLost -= OnGone;
            _row.PointerCanceled -= OnGone;
            _row.Background = RowRest();
        }
    }

    /// <summary>Switch a row between the standard and compact heights without rebuilding it.</summary>
    public static void SetDensity(Border row, bool compact)
        => row.MinHeight = compact ? RowHeightCompact : RowHeight;

    /// <summary>The margin a header carries when it sits above a full-bleed list.</summary>
    public static Thickness HeaderMargin { get; } = new(Space4, Space4, Space4, Space2);

    /// <summary>A list's own padding: none either side so rows reach both edges, a little below.</summary>
    public static Thickness ListPadding { get; } = new(0, 0, 0, Space2);

    /// <summary>The fill a row shows when it is not hovered: transparent, but a real brush.</summary>
    public static Brush RowRest() => FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);
}
