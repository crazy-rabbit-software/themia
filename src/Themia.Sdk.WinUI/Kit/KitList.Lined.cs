using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Run = Microsoft.UI.Xaml.Documents.Run;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7032]..[S-7034] The lined looks: stacked rows, one-line rows with inline actions, the table with
/// its column titles. Each row is a list row (<see cref="Fluent.Row"/>) with the tone's bar in its
/// leading inset and the lead in front of its text; the time is re-labelled on <see cref="Retime"/>
/// without rebuilding a row.
/// </summary>
public sealed partial class KitList
{
    /// <summary>The leading strip of a row that carries the tone's bar, inside the row's own padding.</summary>
    private const double BarLane = 12;

    /// <summary>The breathing room above and below a stacked row's lines.</summary>
    private const double StackedPad = 6;

    /// <summary>The widest a badge is drawn: an account's name, not a sentence.</summary>
    private const double BadgeMaxWidth = 120;

    /// <summary>[S-7033] An inline action's glyph, and its button: the glyph and its padding, inside a compact row.</summary>
    private const double InlineGlyph = 14;

    private const double InlineButton = InlineGlyph + (2 * Fluent.Space1);

    /// <summary>[S-7034] The table's column titles, laid out on the same grid as its rows.</summary>
    private readonly Grid _columnHeader = new();

    /// <summary>Every time on screen, re-labelled by <see cref="Retime"/>.</summary>
    private readonly List<(TextBlock Text, KitWhen? When, KitTime Time)> _times = [];

    /// <summary>[S-7033] The rows that carry inline actions, by place, and what reveals them.</summary>
    private readonly Dictionary<int, Reveal> _reveals = [];

    /// <summary>[S-7032] Re-label the times without rebuilding a row: "4m" an hour later is wrong, rebuilding to learn that is waste.</summary>
    public void Retime(DateTimeOffset now)
    {
        foreach (var (text, when, time) in _times)
        {
            text.Text = KitText.When(Strings.Current, when, time, now);
        }
    }

#if DEBUG
    /// <summary>Debug renders only: the row at <paramref name="index"/> as if the pointer were over it, so its inline actions show.</summary>
    public void PreviewHover(int index)
    {
        if (_reveals.TryGetValue(index, out var reveal))
        {
            reveal.Hovered = true;
        }
    }
#endif

    /// <summary>The keyboard ring on a row, and with it the row's inline actions ([S-7033]).</summary>
    private void Mark(int index, bool focused)
    {
        RowFocus.Mark(_rowElements[index], focused);

        if (_reveals.TryGetValue(index, out var reveal))
        {
            reveal.Focused = focused;
        }
    }

    private Border BuildLinedRow(KitItem item, int index, DateTimeOffset now)
    {
        Reveal? reveal = null;
        var content = _plan.Look switch
        {
            KitRowLook.Table => TableContent(item, now),
            KitRowLook.Line => LineContent(item, now, out reveal),
            _ => StackedContent(item, now),
        };

        var row = Fluent.Row(content, _plan.Compact);
        RowFocus.Prepare(row);
        row.Tag = index;

        if (_plan.Look is KitRowLook.Table or KitRowLook.Line)
        {
            row.MinHeight = Fluent.RowHeightCompact;
        }
        else
        {
            // As tall as its lines, with the breathing room a row of text needs; no taller.
            row.MinHeight = 0;
            var pad = _plan.Compact ? Fluent.Space1 : StackedPad;
            content.Margin = new Thickness(-BarLane, pad - RowFocus.Ring, 0, pad - RowFocus.Ring);
        }

        _rowHooks.Add(Fluent.Hover(row));
        _rowHooks.Add(ItemMenu.Attach(row, () => Entries(item)));

        if (reveal is not null)
        {
            _reveals[index] = reveal;
            _rowHooks.Add(reveal.Hook(row));
        }

        if ((item.Tooltip ?? item.Link?.OriginalString) is { } tip)
        {
            ToolTipService.SetToolTip(row, tip);
        }

        row.Tapped += OnRowTapped;
        row.DoubleTapped += OnRowDoubleTapped;
        return row;
    }

    private string TitleText(KitItem item) => item.Title.Length > 0 ? item.Title : _spec.Untitled();

    private static string? BadgeText(KitItem item) => item.Badges is [{ Text: { Length: > 0 } text }, ..] ? text : null;

    /// <summary>
    /// [S-7032] A stacked row: the title and its time; then the subtitle with the badge beside it;
    /// then the preview lines.
    /// </summary>
    private Grid StackedContent(KitItem item, DateTimeOffset now)
    {
        var lines = new StackPanel { Spacing = 0 };

        var title = Fluent.Text(item.Emphasis ? FluentType.BodyStrong : FluentType.Body);
        title.Text = TitleText(item);
        lines.Children.Add(_plan.When ? TwoColumns(title, TimeText(item, _spec.Time, now, column: false)) : title);

        var badge = _plan.Badges ? BadgeText(item) : null;

        if (_plan.Subtitle || badge is not null)
        {
            var subtitle = Fluent.Text(FluentType.Caption, item.Emphasis ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
            subtitle.Text = _plan.Subtitle ? item.Subtitle ?? string.Empty : string.Empty;

            if (badge is not null)
            {
                var tag = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                tag.Text = badge;
                tag.MaxWidth = BadgeMaxWidth;
                tag.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
                lines.Children.Add(TwoColumns(subtitle, tag));
            }
            else
            {
                lines.Children.Add(subtitle);
            }
        }

        if (_plan.PreviewLines > 0 && !string.IsNullOrWhiteSpace(item.Preview))
        {
            var preview = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            preview.Text = item.Preview;
            preview.TextWrapping = TextWrapping.Wrap;
            preview.MaxLines = _plan.PreviewLines;
            lines.Children.Add(preview);
        }

        return WithBar(WithLead(lines, item), item.Tone);
    }

    /// <summary>
    /// [S-7032] [S-7033] A one-line row: the title in a column of its own, the subtitle with the
    /// preview after a dash, the badge, the time — whose slot the inline actions take on the hovered
    /// and the focused row, at the slot's one width, so the swap moves nothing.
    /// </summary>
    private Grid LineContent(KitItem item, DateTimeOffset now, out Reveal? reveal)
    {
        var inner = KitLadder.InnerWidth(_area.Width, _plan.Lead);
        var actions = _plan.InlineActions ? InlineActions(item) : null;
        var boxes = KitLadder.LineBoxes(inner, Fluent.Space3, 0, actions is null ? 0 : KitLadder.InlineActionsWidth(actions.Children.Count, InlineButton, Fluent.Space1));

        var grid = new Grid { ColumnSpacing = Fluent.Space3 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(boxes.Title) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = Fluent.Text(item.Emphasis ? FluentType.BodyStrong : FluentType.Body);
        title.Text = TitleText(item);
        title.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(title);

        var line = Fluent.Text(FluentType.Body, item.Emphasis ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        line.VerticalAlignment = VerticalAlignment.Center;
        line.Inlines.Add(new Run { Text = _plan.Subtitle ? item.Subtitle ?? string.Empty : string.Empty });

        if (_plan.PreviewLines > 0 && !string.IsNullOrWhiteSpace(item.Preview))
        {
            line.Inlines.Add(new Run
            {
                Text = (_plan.Subtitle ? " – " : string.Empty) + item.Preview,
                Foreground = Fluent.Brush("TextFillColorTertiaryBrush"),
            });
        }

        SetColumn(line, 1);
        grid.Children.Add(line);

        if (_plan.Badges && BadgeText(item) is { } badge)
        {
            var tag = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            tag.Text = badge;
            tag.MaxWidth = BadgeMaxWidth;
            tag.VerticalAlignment = VerticalAlignment.Center;
            SetColumn(tag, 2);
            grid.Children.Add(tag);
        }

        reveal = null;

        if (_plan.When)
        {
            var time = TimeText(item, _spec.Time, now, column: false);
            time.MinWidth = KitLadder.LineTimeWidth;
            time.TextAlignment = TextAlignment.Right;

            FrameworkElement end = time;
            if (actions is not null)
            {
                var cell = new Grid { MinWidth = boxes.Slot, Children = { time, actions } };
                reveal = new Reveal(time, actions);
                end = cell;
            }

            SetColumn(end, 3);
            grid.Children.Add(end);
        }

        return WithBar(WithLead(grid, item), item.Tone);
    }

    /// <summary>[S-7034] A table row: one line, the plan's columns.</summary>
    private Grid TableContent(KitItem item, DateTimeOffset now)
    {
        var columns = _plan.Table ?? [];
        var grid = ColumnGrid(columns);

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i].Column;
            var cell = column switch
            {
                KitColumn.Title => Fluent.Text(item.Emphasis ? FluentType.BodyStrong : FluentType.Body),
                KitColumn.Subtitle => Fluent.Text(FluentType.Body, item.Emphasis ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush"),
                KitColumn.When => TimeText(item, _spec.ColumnTime, now, column: true),
                _ => Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush"),
            };

            cell.VerticalAlignment = VerticalAlignment.Center;
            switch (column)
            {
                case KitColumn.Title:
                    cell.Text = TitleText(item);
                    break;
                case KitColumn.Subtitle:
                    cell.Text = item.Subtitle ?? string.Empty;
                    break;
                case KitColumn.Preview:
                    cell.Text = item.Preview ?? string.Empty;
                    break;
                case KitColumn.Badge:
                    cell.Text = BadgeText(item) ?? string.Empty;
                    break;
            }

            SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        return WithBar(WithLead(grid, item), item.Tone);
    }

    /// <summary>[S-7034] The column titles over the table, on the rows' grid and over their text, not over the ring and the lead.</summary>
    private void BuildColumnHeader()
    {
        _columnHeader.Children.Clear();
        _columnHeader.ColumnDefinitions.Clear();

        var columns = _plan.Table ?? [];
        var template = ColumnGrid(columns);
        foreach (var definition in template.ColumnDefinitions.ToList())
        {
            template.ColumnDefinitions.Remove(definition);
            _columnHeader.ColumnDefinitions.Add(definition);
        }

        _columnHeader.ColumnSpacing = template.ColumnSpacing;

        var lead = Fluent.Space4 + RowFocus.Ring + (_plan.Lead ? KitLadder.LeadLane : 0);
        _columnHeader.Padding = new Thickness(lead, 0, Fluent.Space4 + RowFocus.Ring, Fluent.Space1);

        for (var i = 0; i < columns.Count; i++)
        {
            var title = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            title.Text = _spec.ColumnHeading?.Invoke(columns[i].Column) ?? string.Empty;
            if (columns[i].Column == KitColumn.When)
            {
                title.TextAlignment = TextAlignment.Right;
            }

            SetColumn(title, i);
            _columnHeader.Children.Add(title);
        }

        // Titles over two rows of data are a third of the tile: the plan drops them where there is no room.
        _columnHeader.Visibility = _plan.ColumnHeader ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Grid ColumnGrid(IReadOnlyList<KitColumnFit> columns)
    {
        var grid = new Grid { ColumnSpacing = Fluent.Space2 };

        foreach (var column in columns)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = column.Star > 0 ? new GridLength(column.Star, GridUnitType.Star) : new GridLength(column.Width),
            });
        }

        return grid;
    }

    /// <summary>The time, in the tone's text colour; re-labelled by <see cref="Retime"/>.</summary>
    private TextBlock TimeText(KitItem item, KitTime time, DateTimeOffset now, bool column)
    {
        var when = Fluent.Text(FluentType.Caption, ToneText(item.Tone));
        when.Text = KitText.When(Strings.Current, item.When, time, now);
        when.VerticalAlignment = VerticalAlignment.Center;

        if (column)
        {
            when.TextAlignment = TextAlignment.Right;
        }
        else
        {
            when.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
        }

        _times.Add((when, item.When, time));
        return when;
    }

    private static Grid TwoColumns(FrameworkElement left, FrameworkElement right)
    {
        SetColumn(left, 0);
        SetColumn(right, 1);

        return new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Children = { left, right },
        };
    }

    /// <summary>
    /// [S-7032] The item's lead glyph in front of the row's text, in its tone (tertiary when neutral);
    /// on a stacked row it sits on the title's line. The content as it is when the plan or the item
    /// has no lead.
    /// </summary>
    private FrameworkElement WithLead(FrameworkElement content, KitItem item)
    {
        if (!_plan.Lead || item.Lead.Kind != KitLeadKind.Icon || !Enum.TryParse<KitIcon>(item.Lead.Value, out var glyph))
        {
            return content;
        }

        var icon = Fluent.Icon(KitIcons.Glyph(glyph), item.Lead.Tone == KitTone.Neutral ? "TextFillColorTertiaryBrush" : ToneText(item.Lead.Tone), KitLadder.LeadSize);
        icon.Margin = new Thickness(0, (FluentType.Body.LineHeight - KitLadder.LeadSize) / 2, Fluent.Space2, 0);
        icon.VerticalAlignment = content is StackPanel ? VerticalAlignment.Top : VerticalAlignment.Center;

        SetColumn(content, 1);
        return new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Children = { icon, content },
        };
    }

    /// <summary>
    /// [S-7032] The content with the tone's lane in front of it: a bar in the tone's fill for an item
    /// that carries one (unread). The lane sits inside the row's leading padding (a negative margin
    /// pulls it there), so rows with and without a bar keep their text on one line.
    /// </summary>
    private static Grid WithBar(FrameworkElement content, KitTone tone)
    {
        var grid = new Grid
        {
            Margin = new Thickness(-BarLane, 0, 0, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(BarLane) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };

        if (tone != KitTone.Neutral)
        {
            grid.Children.Add(new Rectangle
            {
                Width = 3,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Fill = Fluent.Brush(ToneFill(tone)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(Fluent.Space1, Fluent.Space1, 0, Fluent.Space1),
            });
        }

        SetColumn(content, 1);
        grid.Children.Add(content);
        return grid;
    }

    /// <summary>A tone's text brush: the time of an unread message, an overdue date.</summary>
    private static string ToneText(KitTone tone) => tone switch
    {
        KitTone.Accent => "AccentTextFillColorPrimaryBrush",
        KitTone.Success => "SystemFillColorSuccessBrush",
        KitTone.Caution => "SystemFillColorCautionBrush",
        KitTone.Critical => "SystemFillColorCriticalBrush",
        _ => "TextFillColorTertiaryBrush",
    };

    /// <summary>A tone's fill brush: the bar.</summary>
    private static string ToneFill(KitTone tone) => tone switch
    {
        KitTone.Accent => "AccentFillColorDefaultBrush",
        KitTone.Success => "SystemFillColorSuccessBrush",
        KitTone.Caution => "SystemFillColorCautionBrush",
        KitTone.Critical => "SystemFillColorCriticalBrush",
        _ => "SubtleFillColorTransparentBrush",
    };

    /// <summary>
    /// [S-7033] The item's inline actions where its time was, for the plan's widest rows: only those
    /// that can run; null when it has none (the menu always has them).
    /// </summary>
    private StackPanel? InlineActions(KitItem item)
    {
        var inline = _actions(item).Where(a => a.Inline && a.Runnable).ToList();
        if (inline.Count == 0)
        {
            return null;
        }

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1, Visibility = Visibility.Collapsed };
        foreach (var action in inline)
        {
            panel.Children.Add(InlineAction(item, action));
        }

        // At the slot's trailing end. The buttons are taller than the line they sit on; they overhang
        // it rather than grow it, so the row keeps its height when they show.
        panel.HorizontalAlignment = HorizontalAlignment.Right;
        panel.VerticalAlignment = VerticalAlignment.Center;
        panel.Margin = new Thickness(0, -Fluent.Space1, 0, -Fluent.Space1);
        return panel;
    }

    private Border InlineAction(KitItem item, KitAction action)
    {
        var button = new Border
        {
            Child = Fluent.Icon(KitIcons.Glyph(action), "TextFillColorSecondaryBrush", InlineGlyph),
            Padding = new Thickness(Fluent.Space1),
            Width = InlineButton,
            Height = InlineButton,
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            VerticalAlignment = VerticalAlignment.Center,
        };

        ItemFill.Paint(button, ItemRole.Row);
        ToolTipService.SetToolTip(button, action.Label);
        _rowHooks.Add(Fluent.Hover(button));

        void OnTapped(object sender, TappedRoutedEventArgs args)
        {
            args.Handled = true;
            Run(item, action);
        }

        // A double-click on a button must not also open the item under it.
        static void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs args) => args.Handled = true;

        button.Tapped += OnTapped;
        button.DoubleTapped += OnDoubleTapped;
        _rowHooks.Add(new Unhook(() =>
        {
            button.Tapped -= OnTapped;
            button.DoubleTapped -= OnDoubleTapped;
        }));

        return button;
    }

    /// <summary>
    /// [S-7033] A line row's inline actions show while the pointer is over the row or the keyboard is
    /// on it, its time otherwise; the time keeps its box, so the slot measures the same either way.
    /// </summary>
    private sealed class Reveal(UIElement time, UIElement actions)
    {
        private bool _hovered;
        private bool _focused;

        public bool Hovered
        {
            get => _hovered;
            set
            {
                _hovered = value;
                Apply();
            }
        }

        public bool Focused
        {
            get => _focused;
            set
            {
                _focused = value;
                Apply();
            }
        }

        /// <summary>Follow the pointer over <paramref name="row"/>; disposing lets go.</summary>
        public IDisposable Hook(Border row)
        {
            void Entered(object sender, PointerRoutedEventArgs args) => Hovered = true;
            void Exited(object sender, PointerRoutedEventArgs args) => Hovered = false;

            row.PointerEntered += Entered;
            row.PointerExited += Exited;
            row.PointerCanceled += Exited;
            return new Unhook(() =>
            {
                row.PointerEntered -= Entered;
                row.PointerExited -= Exited;
                row.PointerCanceled -= Exited;
            });
        }

        private void Apply()
        {
            var shown = _hovered || _focused;
            time.Opacity = shown ? 0 : 1;
            actions.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private sealed class Unhook(Action undo) : IDisposable
    {
        private Action? _undo = undo;

        public void Dispose()
        {
            _undo?.Invoke();
            _undo = null;
        }
    }
}
