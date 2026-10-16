using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Run = Microsoft.UI.Xaml.Documents.Run;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>The rows: stacked, one-line and table, each with the unread bar and, from a medium tile, the envelope.</summary>
internal sealed partial class EmailView
{
    /// <summary>The leading strip of a row that carries the unread bar, inside the row's own padding.</summary>
    private const double BarLane = 12;

    /// <summary>[S-1282] The envelope's width and the gap after it.</summary>
    private const double EnvelopeLane = EnvelopeSize + Fluent.Space2;

    private const double EnvelopeSize = 14;

    /// <summary>The breathing room above and below a stacked row's lines.</summary>
    private const double RowPad = 6;

    /// <summary>The rows on screen, in list order, for the keyboard.</summary>
    private readonly List<Border> _rows = [];

    /// <summary>The messages the rows show, in the same order.</summary>
    private IReadOnlyList<MailMessage> _shown = [];

    /// <summary>
    /// A list or compact row: sender and time; then the subject with the account beside it; then,
    /// in the list view, the preview.
    /// </summary>
    private Grid BuildListRow(MailMessage message, Dictionary<string, string> labels, DateTimeOffset now)
    {
        var layout = _shape.Rows;
        var unread = !message.IsRead;

        var lines = new StackPanel { Spacing = 0 };

        var sender = Fluent.Text(unread ? FluentType.BodyStrong : FluentType.Body);
        sender.Text = message.Sender;

        if (layout.Time)
        {
            lines.Children.Add(TwoColumns(sender, TimeText(message, now, full: false)));
        }
        else
        {
            lines.Children.Add(sender);
        }

        var account = layout.Account ? AccountTag(message, labels) : null;

        if (layout.Subject || account is not null)
        {
            var subject = Fluent.Text(FluentType.Caption, unread ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
            subject.Text = layout.Subject ? message.SubjectLine : string.Empty;

            if (account is not null)
            {
                var tag = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                tag.Text = account;
                tag.MaxWidth = 120;
                tag.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
                lines.Children.Add(TwoColumns(subject, tag));
            }
            else
            {
                lines.Children.Add(subject);
            }
        }

        if (layout.Preview && !string.IsNullOrWhiteSpace(message.Preview))
        {
            var preview = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            preview.Text = message.Preview;
            preview.TextWrapping = TextWrapping.Wrap;
            preview.MaxLines = layout.PreviewLines;
            lines.Children.Add(preview);
        }

        return WithBar(layout.Envelope ? WithEnvelope(lines, unread) : lines, unread);
    }

    /// <summary>
    /// [S-1283] A one-line row for a wide strip or a big tile: the sender in a column of its own, the
    /// subject with the preview after a dash, the account, the time — so a strip lists three or four
    /// messages instead of two, and a big tile fills with them.
    /// </summary>
    private Grid BuildInlineRow(MailMessage message, Dictionary<string, string> labels, DateTimeOffset now)
    {
        var layout = _shape.Rows;
        var unread = !message.IsRead;
        var inner = InnerWidth(_area.Width, layout.Envelope);

        var grid = new Grid { ColumnSpacing = Fluent.Space3 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Clamp(inner * 0.24, 96, 200)) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var sender = Fluent.Text(unread ? FluentType.BodyStrong : FluentType.Body);
        sender.Text = message.Sender;
        sender.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(sender);

        var line = Fluent.Text(FluentType.Body, unread ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        line.VerticalAlignment = VerticalAlignment.Center;
        line.Inlines.Add(new Run { Text = layout.Subject ? message.SubjectLine : string.Empty });

        if (layout.Preview && !string.IsNullOrWhiteSpace(message.Preview))
        {
            line.Inlines.Add(new Run
            {
                Text = (layout.Subject ? " – " : string.Empty) + message.Preview,
                Foreground = FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray),
            });
        }

        Grid.SetColumn(line, 1);
        grid.Children.Add(line);

        if (layout.Account && AccountTag(message, labels) is { } account)
        {
            var tag = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            tag.Text = account;
            tag.MaxWidth = 120;
            tag.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(tag, 2);
            grid.Children.Add(tag);
        }

        var time = TimeText(message, now, full: false);
        time.MinWidth = 36;
        time.TextAlignment = TextAlignment.Right;
        var end = _shape.QuickActions ? WithQuickActions(time, message) : time;
        Grid.SetColumn(end, 3);
        grid.Children.Add(end);

        var row = WithBar(layout.Envelope ? WithEnvelope(grid, unread) : grid, unread);

        // AddRow finds the time-or-actions cell here to swap them under the pointer.
        row.Tag = end as Grid;
        return row;
    }

    /// <summary>A row-view row: one line, the chosen columns that fit.</summary>
    private Grid BuildTableRow(MailMessage message, IReadOnlyList<MailColumnFit> columns, Dictionary<string, string> labels, DateTimeOffset now)
    {
        var unread = !message.IsRead;
        var grid = ColumnGrid(columns);

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i].Column;
            var cell = column switch
            {
                MailColumn.From => Fluent.Text(unread ? FluentType.BodyStrong : FluentType.Body),
                MailColumn.Subject => Fluent.Text(FluentType.Body, unread ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush"),
                MailColumn.Date => TimeText(message, now, full: true),
                _ => Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush"),
            };

            cell.VerticalAlignment = VerticalAlignment.Center;
            switch (column)
            {
                case MailColumn.From:
                    cell.Text = message.Sender;
                    break;
                case MailColumn.Subject:
                    cell.Text = message.SubjectLine;
                    break;
                case MailColumn.Preview:
                    cell.Text = message.Preview;
                    break;
                case MailColumn.Account:
                    cell.Text = AccountTag(message, labels) ?? string.Empty;
                    break;
            }

            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        return WithBar(_shape.Rows.Envelope ? WithEnvelope(grid, unread) : grid, unread);
    }

    /// <summary>The relative time, re-labelled every minute; the accent says unread.</summary>
    private TextBlock TimeText(MailMessage message, DateTimeOffset now, bool full)
    {
        var when = Fluent.Text(FluentType.Caption, message.IsRead ? "TextFillColorTertiaryBrush" : "AccentTextFillColorPrimaryBrush");
        when.Text = full ? MailTime.Full(message.Received, now, Strings.Current) : MailTime.Short(message.Received, now, Strings.Current);
        when.VerticalAlignment = VerticalAlignment.Center;

        if (full)
        {
            when.TextAlignment = TextAlignment.Right;
        }
        else
        {
            when.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
        }

        _times.Add((when, message.Received, full));
        return when;
    }

    /// <summary>The account's name, at every size; never its provider (retired, concise widgets).</summary>
    private static string? AccountTag(MailMessage message, Dictionary<string, string> labels)
        => labels.TryGetValue(message.AccountId, out var label) ? label : null;

    /// <summary>[S-542] The row view's column titles, laid out on the same grid as its rows.</summary>
    private void BuildColumnHeader(IReadOnlyList<MailColumnFit> columns)
    {
        _columnHeader.Children.Clear();
        _columnHeader.ColumnDefinitions.Clear();

        var template = ColumnGrid(columns);
        foreach (var definition in template.ColumnDefinitions.ToList())
        {
            template.ColumnDefinitions.Remove(definition);
            _columnHeader.ColumnDefinitions.Add(definition);
        }

        _columnHeader.ColumnSpacing = template.ColumnSpacing;

        // Over the text, not over the ring and the envelope in front of it.
        var lead = Fluent.Space4 + RowFocus.Ring + (_shape.Rows.Envelope ? EnvelopeLane : 0);
        _columnHeader.Padding = new Thickness(lead, 0, Fluent.Space4 + RowFocus.Ring, Fluent.Space1);

        for (var i = 0; i < columns.Count; i++)
        {
            var title = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            title.Text = columns[i].Column.Label();
            if (columns[i].Column == MailColumn.Date)
            {
                title.TextAlignment = TextAlignment.Right;
            }

            Grid.SetColumn(title, i);
            _columnHeader.Children.Add(title);
        }

        // A header over two rows of data is a third of the tile; drop it when there is no room.
        _columnHeader.Visibility = _shape.ColumnHeader ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Grid ColumnGrid(IReadOnlyList<MailColumnFit> columns)
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

    private static Grid TwoColumns(FrameworkElement left, FrameworkElement right)
    {
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);

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
    /// [S-1282] The closed envelope for unread mail, the open one for read, in front of the row's
    /// text — the original's marker, beside the accent bar rather than instead of it. On a stacked
    /// row it sits on the sender's line.
    /// </summary>
    private static Grid WithEnvelope(FrameworkElement content, bool unread)
    {
        var icon = Fluent.Icon(unread ? Glyphs.Mail : Glyphs.MailRead, unread ? "AccentTextFillColorPrimaryBrush" : "TextFillColorTertiaryBrush", EnvelopeSize);
        icon.Margin = new Thickness(0, (FluentType.Body.LineHeight - EnvelopeSize) / 2, Fluent.Space2, 0);
        icon.VerticalAlignment = content is StackPanel ? VerticalAlignment.Top : VerticalAlignment.Center;

        Grid.SetColumn(content, 1);
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
    /// The content with the unread lane in front of it. The lane sits inside the row's leading
    /// padding (a negative margin pulls it there), so read and unread rows keep their text on the
    /// same line and the bar costs no width.
    /// </summary>
    private static Grid WithBar(FrameworkElement content, bool unread)
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

        if (unread)
        {
            var bar = new Rectangle
            {
                Width = 3,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Fill = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(Fluent.Space1, Fluent.Space1, 0, Fluent.Space1),
            };

            grid.Children.Add(bar);
        }

        Grid.SetColumn(content, 1);
        grid.Children.Add(content);
        return grid;
    }

    /// <summary>
    /// [S-1284] On the largest tile the pointer reveals a row's quick actions where its time was:
    /// mark read or unread, archive, delete — only what the account allows, and nothing on a tile
    /// that has no room for them (the menu always has them).
    /// </summary>
    private FrameworkElement WithQuickActions(TextBlock time, MailMessage message)
    {
        if (!CanChange(message))
        {
            return time;
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1, Visibility = Visibility.Collapsed };
        actions.Children.Add(QuickAction(
            message.IsRead ? Glyphs.Unread : Glyphs.MailRead,
            message.IsRead ? Str.WidgetsB_Email_MarkUnread.Text() : Str.WidgetsB_Email_MarkRead.Text(),
            () => Change(message, message.IsRead ? MailChange.MarkUnread : MailChange.MarkRead)));

        if (message.Folder != MailFolder.Junk)
        {
            actions.Children.Add(QuickAction(Glyphs.Archive, Str.CtxMenu_Archive.Text(), () => Change(message, MailChange.Archive)));
        }

        actions.Children.Add(QuickAction(Glyphs.Delete, DeleteLabel(message), () => Change(message, MailChange.Delete)));

        // Both in one cell; the row's pointer handlers swap them (see AddRow).
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        return new Grid { Children = { time, actions } };
    }

    private Border QuickAction(string glyph, string text, Action action)
    {
        var button = new Border
        {
            Child = Fluent.Icon(glyph, "TextFillColorSecondaryBrush", 14),
            Padding = new Thickness(Fluent.Space1),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            Background = Fluent.RowRest(),
            VerticalAlignment = VerticalAlignment.Center,
        };

        ToolTipService.SetToolTip(button, text);
        _hovers.Add(Fluent.Hover(button));
        button.Tapped += (_, args) =>
        {
            args.Handled = true;
            action();
        };

        // A double-click on a button must not also open the message under it.
        button.DoubleTapped += (_, args) => args.Handled = true;
        return button;
    }

    /// <summary>Show a row's quick actions while the pointer is over it, its time otherwise.</summary>
    private static void RevealQuickActions(Grid cell, bool shown)
    {
        if (cell.Children is not [UIElement time, UIElement actions])
        {
            return;
        }

        time.Visibility = shown ? Visibility.Collapsed : Visibility.Visible;
        actions.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddRow(Grid content, MailMessage message)
    {
        var row = Fluent.Row(content, _shape.Compact);
        RowFocus.Prepare(row);

        if (_config.Style == EmailStyle.Table || _shape.Rows.Inline)
        {
            row.MinHeight = Fluent.RowHeightCompact;
        }
        else
        {
            // As tall as its lines, with the breathing room a row of text needs; no taller.
            row.MinHeight = 0;
            var pad = _shape.Compact ? Fluent.Space1 : RowPad;
            content.Margin = new Thickness(-BarLane, pad - RowFocus.Ring, 0, pad - RowFocus.Ring);
        }

        _hovers.Add(Fluent.Hover(row));

        if (content.Tag is Grid quick)
        {
            row.PointerEntered += (_, _) => RevealQuickActions(quick, true);
            row.PointerExited += (_, _) => RevealQuickActions(quick, false);
            row.PointerCanceled += (_, _) => RevealQuickActions(quick, false);
        }

        // [S-1288] A click focuses the row for the keyboard; a double-click opens it.
        row.Tapped += (_, args) =>
        {
            args.Handled = true;
            FocusRow(_rows.IndexOf(row));
        };

        row.DoubleTapped += (_, args) =>
        {
            args.Handled = true;
            OpenMessage(message);
        };

        row.RightTapped += (_, args) =>
        {
            args.Handled = true;
            ShowMessageMenu(row, message, args);
        };

        ToolTipService.SetToolTip(row, Tooltip(message, _source?.Accounts.FirstOrDefault(a => a.Id == message.AccountId)));
        _rows.Add(row);
        _list.Children.Add(row);
    }

    /// <summary>Who it is from, the subject, the full date, and which account it came to.</summary>
    private static string Tooltip(MailMessage message, MailAccount? account)
    {
        var from = message.FromAddress.Length > 0 && message.FromName.Length > 0
            ? $"{message.FromName} <{message.FromAddress}>"
            : message.Sender;

        var when = message.Received is { } at ? Environment.NewLine + WhenLong(at.ToLocalTime()) : string.Empty;
        var to = account is null ? string.Empty : Environment.NewLine + account.Label;
        return from + Environment.NewLine + message.SubjectLine + when + to;
    }

    /// <summary>"Sunday, October 4, 2026, 9:05 AM" in the current language.</summary>
    private static string WhenLong(DateTimeOffset local)
        => Str.WidgetsB_Time_DateTime.Format(
            ("date", LocaleFormat.Date(Strings.Current, local.DateTime, DateStyle.Full)),
            ("time", LocaleFormat.Time(Strings.Current, local.DateTime)));
}
