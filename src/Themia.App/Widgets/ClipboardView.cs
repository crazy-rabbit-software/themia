using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Core.Shell;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// What has been copied recently, most recent first. Click a row to put it back on the clipboard.
/// <para>
/// This is the most sensitive widget in the app, and the design is mostly a list of things it does
/// not do. It reads <b>text only</b> — a copied file or image is never looked at. It keeps a
/// bounded history <b>in memory for the session</b> and writes nothing to disk, so closing the app
/// is the end of it; there is no persistence setting, because a clipboard history that survives a
/// restart is a credentials file nobody meant to create. Entries are <b>displayed, never
/// executed</b>: no row here is a launch target, whatever it contains — a link is labelled as a
/// link, never opened.
/// </para>
/// <para>
/// [S-1458] The history is the app's one (<see cref="IWidgetHost.ClipboardHistory"/>), kept while any view
/// holds a Clipboard widget, so it is there when this view is switched to; the widget shows its
/// newest entries, as many as its own setting keeps. It is fed by the host's clipboard listener, a
/// message-only window with <c>AddClipboardFormatListener</c> rather than a poll — so the
/// clipboard is read exactly when Windows says it changed and at no other time.
/// </para>
/// <para>
/// [S-1443] The tile uses its room through <see cref="ClipboardLayout.Plan"/>: one line per row on
/// a small tile, wrapped two- and three-line previews as it grows, a detail line (when · how much)
/// on tall tiles and a time column where the width allows; nothing but the entries.
/// </para>
/// </summary>
internal sealed partial class ClipboardView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private const string Scope = "clipboard-widget";

    /// <summary>How many characters of each line a row shows before the ellipsis.</summary>
    private const int PreviewLength = 120;

    /// <summary>What the "when" column reserves, so the times line up down the list.</summary>
    private const double TimestampWidth = 40;

    private readonly Button _clear;
    private readonly StackPanel _list = new() { Spacing = 0 };
    private readonly ScrollViewer _scroll;
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();

    /// <summary>One per row, so the hover and menu handlers come off with the rows they serve.</summary>
    private readonly List<IDisposable> _rowHandles = [];

    private readonly List<Border> _rows = [];


    private ClipboardConfig _config = new();
    private ClipboardPlan _plan = ClipboardLayout.Plan(default);
    private readonly IWidgetHost _host;
    private IClipboardHistory? _source;
    private IDisposable? _subscription;
    private Ticker? _ticker;
    private IWidgetSurface? _surface;
    private int? _cursor;
    private bool _detached;

    public ClipboardView(IWidgetHost host)
    {
        _host = host;
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _clear = new Button
        {
            Content = Fluent.Icon(Glyphs.Delete, "TextFillColorSecondaryBrush", 14),
            Padding = new Thickness(Fluent.Space2, Fluent.Space1, Fluent.Space2, Fluent.Space1),
            MinWidth = 0,
        };

        _clear.Click += OnClearClicked;
        ToolTipService.SetToolTip(_clear, Str.Clipboard_ClearHistory.Text());

        _scroll = new ScrollViewer
        {
            Content = _list,
            Padding = WidgetLayout.ListInset,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };

        _empty = Fluent.EmptyState(Glyphs.Clipboard, _emptyText, out _);
        SetRowSpan(_empty, 2);

        Children.Add(_scroll);
        Children.Add(_empty);
    }

    /// <summary>
    /// The clear-history button, in the card's header. Built in the constructor because the card
    /// reads this once, when it builds that header.
    /// </summary>
    public FrameworkElement? HeaderTrailing => _clear;

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Strings.Changed += OnLanguage;

        // [S-1458] The app's one history, shared by every Clipboard widget on every view.
        // [S-1443] Never for a preview (a debug render): a render's PNG must not
        // carry whatever happens to be on the user's clipboard.
        if (_surface?.IsPreview != true && _source is null && _host.ClipboardHistory is { } source)
        {
            _source = source;
            _source.Changed += OnHistoryChanged;
        }

        // The tick does not fetch anything. It only keeps the relative times ("4m", "2h") honest,
        // which is the one thing in this widget that goes stale on its own.
        _subscription ??= _ticker?.Subscribe(_config.TickInterval, Redraw);

        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ClipboardConfig ?? new ClipboardConfig();

        // Lowering the capacity shows fewer entries at once; the shared history drops what no
        // Clipboard widget keeps any more when the state saves the setting ([S-1458]): a shorter
        // history is the one setting here that actually reduces exposure.
        Render();
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        var plan = ClipboardLayout.Plan(area);

        if (plan == _plan)
        {
            return;
        }

        _plan = plan;
        Render();
    }

    /// <summary>The newest entries, as many as this widget keeps; none for a preview.</summary>
    private IReadOnlyList<ClipboardEntry> Entries
        => _source is not { } source ? []
            : source.Entries.Count <= _config.EffectiveCapacity ? source.Entries
            : [.. source.Entries.Take(_config.EffectiveCapacity)];

    /// <summary>The shared history changed (only a real change is raised): redraw.</summary>
    private void OnHistoryChanged()
    {
        if (!_detached)
        {
            Render();
        }
    }

    private void Redraw()
    {
        if (!_detached)
        {
            Render();
        }
    }

    // -----------------------------------------------------------------------------------------
    // Drawing
    // -----------------------------------------------------------------------------------------

    private void Render()
    {
        ClearRows();

        _clear.IsEnabled = Entries.Count > 0;

        if (Entries.Count == 0)
        {
            // Each state says which it is. "Nothing copied yet" and "the listener never started"
            // need very different things from the reader.
            _emptyText.Text = _host.ClipboardHistory?.Listening == true || _surface?.IsPreview == true
                ? Str.WidgetsA_Clipboard_Empty.Text()
                : Str.WidgetsA_Clipboard_NoListener.Text();

            _empty.Visibility = Visibility.Visible;
            _scroll.Visibility = Visibility.Collapsed;
            _cursor = null;
            return;
        }

        _empty.Visibility = Visibility.Collapsed;
        _scroll.Visibility = Visibility.Visible;

        // One clock reading for the whole list, so a list drawn across a minute boundary cannot
        // show two different ideas of "now".
        var now = DateTimeOffset.Now;

        for (var i = 0; i < Entries.Count; i++)
        {
            var row = BuildRow(Entries[i], i == 0, now);
            _rows.Add(row);
            _list.Children.Add(row);
        }

        _cursor = _cursor is { } cursor && cursor < Entries.Count ? cursor : null;
        ShowCursor();
    }

    private Border BuildRow(ClipboardEntry entry, bool current, DateTimeOffset now)
    {
        var icon = Marker(entry.Text, current);
        icon.Margin = new Thickness(0, 0, Fluent.Space3, 0);
        icon.VerticalAlignment = _plan.PreviewLines > 1 || _plan.Detail ? VerticalAlignment.Top : VerticalAlignment.Center;

        // [S-1441] Wrapped, up to the plan's lines, with the entry's own line breaks kept.
        var text = Fluent.Text(FluentType.Body);
        text.Text = ClipboardLayout.PreviewLines(entry.Text, _plan.PreviewLines, PreviewLength * _plan.PreviewLines);
        text.TextWrapping = _plan.PreviewLines > 1 ? TextWrapping.Wrap : TextWrapping.NoWrap;
        text.MaxLines = _plan.PreviewLines;
        text.TextTrimming = TextTrimming.CharacterEllipsis;

        var body = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        body.Children.Add(text);

        if (_plan.Detail)
        {
            // When · how much, under the preview: the tall tiles' replacement for the time column.
            var detail = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            var size = ClipboardFormatting.Describe(entry.Text, Strings.Current);
            detail.Text = _config.ShowTimestamp ? $"{Formatting.Ago(entry.CapturedAt, now)} · {size}" : size;
            detail.MaxLines = 1;
            Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(detail, FontNumeralAlignment.Tabular);
            body.Children.Add(detail);
        }

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        SetColumn(icon, 0);
        SetColumn(body, 1);
        grid.Children.Add(icon);
        grid.Children.Add(body);

        if (_plan.TimeColumn && _config.ShowTimestamp)
        {
            var when = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            when.Text = Formatting.Ago(entry.CapturedAt, now);
            when.TextAlignment = TextAlignment.Right;
            when.MinWidth = TimestampWidth;
            when.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
            when.VerticalAlignment = _plan.PreviewLines > 1 ? VerticalAlignment.Top : VerticalAlignment.Center;

            Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(when, FontNumeralAlignment.Tabular);

            SetColumn(when, 2);
            grid.Children.Add(when);
        }

        var row = Fluent.Row(grid, _plan.Compact);

        if (_plan.PreviewLines > 1 || _plan.Detail)
        {
            // Rows that hold more than one line pad themselves like a two-line Windows list item.
            row.Padding = new Thickness(Fluent.Space4, Fluent.Space2, Fluent.Space4, Fluent.Space2);
        }

        row.Tag = entry.Text;
        _rowHandles.Add(Fluent.Hover(row));

        // [S-1444] The row menu, read fresh on each right-tap.
        var captured = entry.Text;
        _rowHandles.Add(ItemMenu.Attach(row, () => RowMenu(captured)));

        // A single click, and deliberately so: this widget opens nothing. The worst a mis-click can
        // do is put something back on the clipboard.
        row.Tapped += OnRowTapped;
        WidgetItems.Mark(row); // [S-814]

        ToolTipService.SetToolTip(row, ClipboardFormatting.Describe(entry.Text, Strings.Current));
        return row;
    }

    /// <summary>
    /// [S-1442] The row's leading mark. The top entry is what is on the clipboard right now, said with
    /// the stock tick in the accent colour (the widget's only, event-driven, feedback that a click
    /// landed). Otherwise what the entry looks like: a swatch for a colour, a glyph for a link, an
    /// address, a number or several lines.
    /// </summary>
    private static FrameworkElement Marker(string text, bool current)
    {
        if (current)
        {
            return Fluent.Icon(Glyphs.Done, "AccentTextFillColorPrimaryBrush");
        }

        if (ClipboardLayout.TryColour(text, out var colour))
        {
            return new Border
            {
                Width = Fluent.IconSize,
                Height = Fluent.IconSize,
                CornerRadius = new CornerRadius(Fluent.ControlRadius),
                BorderThickness = new Thickness(Fluent.Hairline),
                BorderBrush = ThemeBrush.Of("ControlStrokeColorDefaultBrush", Colors.Gray),

                // The user's own colour, which is the content here, not a design choice.
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, colour.R, colour.G, colour.B)),
            };
        }

        return Fluent.Icon(ClipboardLayout.Classify(text) switch
        {
            ClipboardContent.Link => Glyphs.Link,
            ClipboardContent.Email => Glyphs.Mail,
            ClipboardContent.Number => Glyphs.Calculator,
            ClipboardContent.Lines => Glyphs.Note,
            _ => Glyphs.Copy,
        });
    }

    private IEnumerable<ItemMenuEntry> RowMenu(string text)
    {
        if (_detached)
        {
            return [];
        }

        return
        [
            new(Glyphs.Copy, Str.Common_Copy.Text(), () => CopyBack(text), Quick: QuickCommand.Copy),
            new(Glyphs.Delete, Str.WidgetsA_Clipboard_Remove.Text(), () => Remove(text), Danger: true, Quick: QuickCommand.Delete),
            ItemMenuEntry.Separator,
            new(Glyphs.Clear, Str.WidgetsA_Clipboard_ClearEllipsis.Text(), () => _ = ConfirmClearAsync(), Danger: true),
        ];
    }

    private void OnRowTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;

        if (!_detached && sender is Border { Tag: string text })
        {
            CopyBack(text);
        }
    }

    private void CopyBack(string text)
    {
        if (_detached)
        {
            return;
        }

        if (!_host.CopyText(text))
        {
            // Already logged with the Win32 reason. Nothing changed, and the list still shows what
            // is genuinely on the clipboard, so there is nothing to correct on screen.
            Log.Debug(Scope, "the entry could not be put back on the clipboard");
        }

        // No redraw here on purpose: the write raises a clipboard change, and the history redraws
        // from that. What is shown as current is then what Windows actually holds, not what this
        // widget hoped it wrote.
    }

    /// <summary>[S-1444] Forget one entry. Leaves the clipboard itself alone.</summary>
    private void Remove(string text)
    {
        if (!_detached)
        {
            // The history raises the change, and every Clipboard widget redraws from it.
            _source?.Remove(text);
        }
    }

    private void ClearRows()
    {
        foreach (var handle in _rowHandles)
        {
            handle.Dispose();
        }

        _rowHandles.Clear();

        foreach (var row in _rows)
        {
            row.Tapped -= OnRowTapped;

            // Drop the reference to the copied text with the row. A detached Border holding a
            // password in its Tag is exactly the thing this widget should not be doing.
            row.Tag = null;
        }

        _rows.Clear();
        _list.Children.Clear();
    }

    // -----------------------------------------------------------------------------------------
    // Keyboard ([S-1444])
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1444] [S-1561] A key from the shell's keyboard lease, decided by <see cref="ListKeys"/> like
    /// every list's: Up/Down, Page Up/Down, Home/End move a cursor through the entries, Enter copies
    /// the one under it back to the clipboard, Delete removes it from the history (the cursor stays
    /// on the row that took its place), the Menu key opens its menu, Escape — also sent whenever the
    /// lease ends — drops the cursor. Returns whether the key was used.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        var decision = ListKeys.Decide(key, _cursor ?? -1, Entries.Count);

        switch (decision.Command)
        {
            case ListCommand.Focus:
                _cursor = decision.Index;
                ShowCursor();
                return true;

            case ListCommand.Open:
                CopyBack(Entries[decision.Index].Text);
                return true;

            case ListCommand.Delete:
                // Render keeps a cursor that is still in range, which is the row that moved up.
                _cursor = ListKeys.AfterRemoval(decision.Index, Entries.Count - 1) is var next and >= 0 ? next : null;
                Remove(Entries[decision.Index].Text);
                return true;

            case ListCommand.Menu when decision.Index < _rows.Count:
                ItemMenu.Open(_rows[decision.Index], RowMenu(Entries[decision.Index].Text));
                return true;

            case ListCommand.Release:
                var had = _cursor is not null;
                _cursor = null;
                ShowCursor();
                return had;

            default:
                return false;
        }
    }

    /// <summary>The cursor row carries the focus outline; the others none.</summary>
    private void ShowCursor()
    {
        var focus = ThemeBrush.Of("FocusStrokeColorOuterBrush", Colors.White);

        for (var i = 0; i < _rows.Count; i++)
        {
            var on = _cursor == i;
            _rows[i].BorderBrush = on ? focus : null;
            _rows[i].BorderThickness = new Thickness(on ? Fluent.Hairline * 2 : 0);
            _rows[i].CornerRadius = new CornerRadius(on ? Fluent.ControlRadius : 0);

            if (on)
            {
                _rows[i].StartBringIntoView();
            }
        }
    }

    // -----------------------------------------------------------------------------------------
    // Clearing
    // -----------------------------------------------------------------------------------------

    private void OnClearClicked(object sender, RoutedEventArgs args) => _ = ConfirmClearAsync();

    /// <summary>
    /// Ask, then clear. Awaited rather than fired and forgotten, so a question that could not be
    /// shown is never mistaken for the user saying yes.
    /// </summary>
    private async Task ConfirmClearAsync()
    {
        if (_detached || Entries.Count == 0)
        {
            return;
        }

        var confirmed = await AskToClearAsync().ConfigureAwait(true);

        if (!confirmed || _detached)
        {
            return;
        }

        // Every Clipboard widget shows the one history, so all of them are cleared.
        _source?.Clear();
    }

    /// <summary>
    /// [S-1500] The question beside the header's clear, or under the widget when the menu asked and
    /// the header has no room for the button.
    /// </summary>
    private Task<bool> AskToClearAsync() => ConfirmFlyout.AskAsync(
        _clear.XamlRoot is not null && _clear.Visibility == Visibility.Visible && _clear.ActualWidth > 0 ? _clear : this,
        Str.WidgetsA_Clipboard_ClearConfirm_Title.Text(),
        Str.Common_Clear.Text(),
        Str.WidgetsA_Clipboard_ClearConfirm_Body.Text());

    /// <summary>[S-1109] Rows and sizes are worded again in the new language.</summary>
    private void OnLanguage(Locale locale)
    {
        ToolTipService.SetToolTip(_clear, Str.Clipboard_ClearHistory.Text());
        Render();
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;

        _clear.Click -= OnClearClicked;

        _subscription?.Dispose();
        _subscription = null;

        // [S-1458] The history stays with the app while a view holds a Clipboard widget; it is
        // forgotten, and the listener stopped, when the state holds none.
        if (_source is not null)
        {
            _source.Changed -= OnHistoryChanged;
            _source = null;
        }

        ClearRows();

        _ticker = null;
    }
}
