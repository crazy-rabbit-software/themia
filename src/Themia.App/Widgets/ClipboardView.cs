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
/// The history is fed by <see cref="ClipboardWatcher"/>, which is a message-only window with
/// <c>AddClipboardFormatListener</c> rather than a poll — so the clipboard is read exactly when
/// Windows says it changed and at no other time.
/// </para>
/// <para>
/// [S-1443] The tile uses its room through <see cref="ClipboardLayout.Plan"/>: one line per row on
/// a small tile, wrapped two- and three-line previews as it grows, a detail line (when · how much)
/// on tall tiles, a time column where the width allows, and a count footer on large ones.
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
    private readonly TextBlock _footer = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");

    /// <summary>One per row, so the hover and menu handlers come off with the rows they serve.</summary>
    private readonly List<IDisposable> _rowHandles = [];

    private readonly List<Border> _rows = [];

    private readonly ClipboardHistory _history = new();

    private ClipboardConfig _config = new();
    private ClipboardPlan _plan = ClipboardLayout.Plan(default);
    private IDisposable? _watch;
    private IDisposable? _subscription;
    private Ticker? _ticker;
    private IWidgetSurface? _surface;
    private int? _cursor;
    private bool _detached;

    public ClipboardView()
    {
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

        _footer.Margin = WidgetLayout.FooterInset;
        _footer.TextAlignment = TextAlignment.Right;
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_footer, FontNumeralAlignment.Tabular);
        SetRow(_footer, 1);

        Children.Add(_scroll);
        Children.Add(_footer);
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

        // The listener is shared by every Clipboard widget on every display: subscribers are
        // counted and the message window is created with the first and destroyed with the last.
        // [S-1443] Never for a preview (a debug render): a render's PNG must not
        // carry whatever happens to be on the user's clipboard.
        if (_surface?.IsPreview != true)
        {
            _watch ??= ClipboardWatcher.Subscribe(OnClipboardText);
        }

        // The tick does not fetch anything. It only keeps the relative times ("4m", "2h") honest,
        // which is the one thing in this widget that goes stale on its own.
        _subscription ??= _ticker?.Subscribe(_config.TickInterval, Redraw);

        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ClipboardConfig ?? new ClipboardConfig();

        // Lowering the capacity drops the oldest entries immediately rather than at the next copy:
        // a shorter history is the one setting here that actually reduces exposure, and it should
        // take effect when it is asked for.
        _history.Capacity = _config.EffectiveCapacity;

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

    /// <summary>Something new was copied. Only a change to the list is worth a redraw.</summary>
    private void OnClipboardText(string text)
    {
        if (_detached)
        {
            return;
        }

        if (_history.Add(text, DateTimeOffset.Now))
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

        _clear.IsEnabled = _history.Count > 0;

        if (_history.Count == 0)
        {
            // Each state says which it is. "Nothing copied yet" and "the listener never started"
            // need very different things from the reader.
            _emptyText.Text = ClipboardWatcher.IsRunning || _surface?.IsPreview == true
                ? Str.WidgetsA_Clipboard_Empty.Text()
                : Str.WidgetsA_Clipboard_NoListener.Text();

            _empty.Visibility = Visibility.Visible;
            _scroll.Visibility = Visibility.Collapsed;
            _footer.Visibility = Visibility.Collapsed;
            _cursor = null;
            return;
        }

        _empty.Visibility = Visibility.Collapsed;
        _scroll.Visibility = Visibility.Visible;

        // One clock reading for the whole list, so a list drawn across a minute boundary cannot
        // show two different ideas of "now".
        var now = DateTimeOffset.Now;

        for (var i = 0; i < _history.Count; i++)
        {
            var row = BuildRow(_history.Entries[i], i == 0, now);
            _rows.Add(row);
            _list.Children.Add(row);
        }

        _cursor = _cursor is { } cursor && cursor < _history.Count ? cursor : null;
        ShowCursor();

        _footer.Visibility = _plan.Footer ? Visibility.Visible : Visibility.Collapsed;
        _footer.Text = ClipboardLayout.CountLine(_history.Count, _history.Capacity, Strings.Current);
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
                BorderBrush = FluentLayout.Themed("ControlStrokeColorDefaultBrush", Colors.Gray),

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

        if (!ClipboardWatcher.SetText(text))
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
        if (!_detached && _history.Remove(text))
        {
            Render();
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

        var decision = ListKeys.Decide(key, _cursor ?? -1, _history.Count);

        switch (decision.Command)
        {
            case ListCommand.Focus:
                _cursor = decision.Index;
                ShowCursor();
                return true;

            case ListCommand.Open:
                CopyBack(_history.Entries[decision.Index].Text);
                return true;

            case ListCommand.Delete:
                // Render keeps a cursor that is still in range, which is the row that moved up.
                _cursor = ListKeys.AfterRemoval(decision.Index, _history.Count - 1) is var next and >= 0 ? next : null;
                Remove(_history.Entries[decision.Index].Text);
                return true;

            case ListCommand.Menu when decision.Index < _rows.Count:
                ItemMenu.Open(_rows[decision.Index], RowMenu(_history.Entries[decision.Index].Text));
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
        var focus = FluentLayout.Themed("FocusStrokeColorOuterBrush", Colors.White);

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
    /// Ask, then clear. Awaited rather than fired and forgotten, so a dialog that could not be
    /// shown is never mistaken for the user saying yes.
    /// </summary>
    private async Task ConfirmClearAsync()
    {
        if (_detached || _history.Count == 0)
        {
            return;
        }

        var confirmed = await FluentLayout.ConfirmAsync(
            this,
            Str.WidgetsA_Clipboard_ClearConfirm_Title.Text(),
            Str.WidgetsA_Clipboard_ClearConfirm_Body.Text(),
            Str.Common_Clear.Text());

        if (!confirmed || _detached)
        {
            return;
        }

        _history.Clear();
        Render();
    }

    /// <summary>[S-1109] Rows, sizes and the footer are worded again in the new language.</summary>
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

        // Unhooks this widget from the shared listener, and takes the message window down with it
        // when this was the last Clipboard widget on the desktop.
        _watch?.Dispose();
        _watch = null;

        // In memory only, and gone the moment the widget is: there is nothing to write out and
        // nothing left behind.
        _history.Clear();
        ClearRows();

        _ticker = null;
    }
}
