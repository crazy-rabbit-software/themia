using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Core.Shell;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Clipboard;

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
/// [S-1443] Each entry is one of the family's rows ([S-6711], <see cref="ClipboardLayout.Plan"/>): its
/// preview on one line, how much it is under it on a regular tile, its marker and when it was
/// copied from 180 wide; the clear is the header's button; nothing but the entries.
/// </para>
/// </summary>
internal sealed partial class ClipboardView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private const string Scope = "clipboard-widget";

    /// <summary>[S-1444] The header's clear: a header button ([S-6710]), there only while there is something to clear.</summary>
    /// <summary>[S-2050] The clock the ages read: the real one, or a preview's sample moment.</summary>
    private Func<DateTimeOffset> _clock = static () => DateTimeOffset.Now;

    private readonly HeaderParts _trailing = new();
    private readonly Border _clear;
    private readonly StackPanel _list = new();
    private readonly ScrollViewer _scroll;
    private readonly ScrollFade _fade;
    private readonly KitStatePanel _state;

    /// <summary>One per row, so the hover and menu handlers come off with the rows they serve.</summary>
    private readonly List<IDisposable> _rowHandles = [];

    /// <summary>The rows in order, for the keyboard cursor ([S-1444]).</summary>
    private readonly List<RowBlock> _rows = [];

    private ClipboardConfig _config = new();
    private ClipboardPlan _plan = ClipboardLayout.Plan(default);
    private WidgetArea _area;
    private readonly IWidgetHost _host;
    private IClipboardHistory? _source;
    private IDisposable? _subscription;
    private Ticker? _ticker;
    private IWidgetSurface? _surface;
    private int? _cursor;
    private bool _detached;

#if DEBUG
    /// <summary>A Debug render's fixed entries in place of the history ([S-1917]); never the user's clipboard.</summary>
    private IReadOnlyList<ClipboardEntry>? _sample;
#else
    private static IReadOnlyList<ClipboardEntry>? _sample => null;
#endif

    public ClipboardView(IWidgetHost host)
    {
        _host = host;
        _clear = _trailing.Add(HeaderPartKind.Button, HeaderBlock.Button(Glyphs.Delete, ClipboardStr.Clipboard_ClearHistory.Text(), () => _ = ConfirmClearAsync()));

        _scroll = new ScrollViewer
        {
            Content = _list,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _fade = new ScrollFade(_scroll, _list);
        _state = new KitStatePanel(ClipboardModule.Glyph, host) { Visibility = Visibility.Collapsed };

        Children.Add(_scroll);
        Children.Add(_state);
    }

    public FrameworkElement? HeaderTrailing => _trailing;

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

    /// <summary>[S-1443] [S-663] A new size: draw again only when the ladder lands elsewhere or the rows that fit change.</summary>
    public void ApplySize(WidgetArea area)
    {
        var plan = ClipboardLayout.Plan(area);
        var moved = plan != _plan || ClipboardLayout.Rows(area, plan) != ClipboardLayout.Rows(_area, _plan);
        _area = area;
        if (!moved)
        {
            return;
        }

        _plan = plan;
        Render();
    }

    /// <summary>The entries this widget shows: the newest, as many as its own setting keeps.</summary>
    private IReadOnlyList<ClipboardEntry> Entries
    {
        get
        {
            var all = _sample ?? _source?.Entries ?? [];
            return all.Count <= _config.EffectiveCapacity ? all : [.. all.Take(_config.EffectiveCapacity)];
        }
    }

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

        var entries = Entries;
        _trailing.Want(_clear, entries.Count > 0);

        if (entries.Count == 0)
        {
            // [S-6714] Each state says which it is, in one row where the first entry would be.
            // "Nothing copied yet" and "the listener never started" need very different things.
            var listening = _host.ClipboardHistory?.Listening == true || _surface?.IsPreview == true;
            _state.Show((listening ? ClipboardStr.WidgetsA_Clipboard_Empty : ClipboardStr.WidgetsA_Clipboard_NoListener).Text(), offerAccounts: false, problem: !listening);
            _scroll.Visibility = Visibility.Collapsed;
            _cursor = null;
            return;
        }

        _state.Hide();
        _scroll.Visibility = Visibility.Visible;

        // One clock reading for the whole list, so a list drawn across a minute boundary cannot
        // show two different ideas of "now".
        var now = _clock();

        for (var i = 0; i < entries.Count; i++)
        {
            var row = BuildRow(entries[i], i == 0, now);
            _rows.Add(row);
            _list.Children.Add(row);
        }

        // [S-6712] As tall as its whole rows: never a part of one at its foot.
        _scroll.Height = _area.IsMeasured ? Math.Min(_rows.Count, ClipboardLayout.Rows(_area, _plan)) * _plan.RowHeight : double.NaN;

        _cursor = _cursor is { } cursor && cursor < entries.Count ? cursor : null;
        ShowCursor();
        _fade.Refresh();
    }

    /// <summary>
    /// [S-1441] [S-1442] One entry: its marker, its preview on one line (BodyStrong for what is on the
    /// clipboard now), when it was copied, how much it is on the second line.
    /// </summary>
    private RowBlock BuildRow(ClipboardEntry entry, bool current, DateTimeOffset now)
    {
        var row = new RowBlock(RowKind.List, _plan.Lead ? FamilyShape.Icon : 0) { Lines = _plan.Lines, Strong = current, Interactive = true };
        row.Show(
            ClipboardFormatting.Preview(entry.Text, ClipboardLayout.PreviewLength),
            _plan.Time && _config.ShowTimestamp ? Formatting.Ago(entry.CapturedAt, now) : null,
            ClipboardFormatting.Describe(entry.Text, Strings.Current));

        if (_plan.Lead)
        {
            row.Lead(Marker(entry.Text, current));
        }

        row.Tag = entry.Text;

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
    /// [S-1442] What the entry looks like it is: the tick for what is on the clipboard now, a dot of a
    /// colour's own colour (a data colour, [S-6705]), else its kind's glyph, all 16.
    /// </summary>
    private static FrameworkElement Marker(string text, bool current)
    {
        if (current)
        {
            return Family.Glyph(Glyphs.Done);
        }

        if (ClipboardLayout.TryColour(text, out var colour))
        {
            return new Border
            {
                Width = FamilyShape.Icon,
                Height = FamilyShape.Icon,
                CornerRadius = Family.Round(FamilyShape.Icon, FamilyShape.Icon),

                // The user's own colour, which is the content here, not a design choice.
                Background = Family.Data($"#{colour.R:X2}{colour.G:X2}{colour.B:X2}"),
            };
        }

        return Family.Glyph(ClipboardLayout.Classify(text) switch
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
            new(Glyphs.Delete, ClipboardStr.WidgetsA_Clipboard_Remove.Text(), () => Remove(text), Danger: true, Quick: QuickCommand.Delete),
            ItemMenuEntry.Separator,
            new(Glyphs.Clear, ClipboardStr.WidgetsA_Clipboard_ClearEllipsis.Text(), () => _ = ConfirmClearAsync(), Danger: true),
        ];
    }

    private void OnRowTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;

        if (!_detached && sender is RowBlock { Tag: string text })
        {
            CopyBack(text);
        }
    }

    private void CopyBack(string text)
    {
        if (_detached || _sample is not null)
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
            row.Detach();

            // Drop the reference to the copied text with the row. A detached row holding a
            // password in its Tag is exactly the thing this widget should not be doing.
            row.Tag = null;
        }

        _rows.Clear();
        _list.Children.Clear();
    }

    // -----------------------------------------------------------------------------------------
    // Keyboard ([S-1444])
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-1561] Up/Down/Page/Home/End move a cursor, Enter copies back, Delete removes, Menu opens the row's menu, Escape drops it.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        var entries = Entries;
        var decision = ListKeys.Decide(key, _cursor ?? -1, entries.Count);

        switch (decision.Command)
        {
            case ListCommand.Focus:
                _cursor = decision.Index;
                ShowCursor();
                return true;

            case ListCommand.Open:
                CopyBack(entries[decision.Index].Text);
                return true;

            case ListCommand.Delete:
                // Render keeps a cursor that is still in range, which is the row that moved up.
                _cursor = ListKeys.AfterRemoval(decision.Index, entries.Count - 1) is var next and >= 0 ? next : null;
                Remove(entries[decision.Index].Text);
                return true;

            case ListCommand.Menu when decision.Index < _rows.Count:
                ItemMenu.Open(_rows[decision.Index], RowMenu(entries[decision.Index].Text));
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

    /// <summary>[S-1288] The keyboard's ring: the Row block's own, over the whole row.</summary>
    private void ShowCursor()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Focused = _cursor == i;
        }
    }

    // -----------------------------------------------------------------------------------------
    // Clearing
    // -----------------------------------------------------------------------------------------

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

    /// <summary>[S-1500] The widgets' one confirmation, beside the header's clear (or the widget when it is folded away).</summary>
    private Task<bool> AskToClearAsync() => ConfirmFlyout.AskAsync(
        _clear.XamlRoot is not null && _clear.Visibility == Visibility.Visible && _clear.ActualWidth > 0 ? _clear : this,
        ClipboardStr.WidgetsA_Clipboard_ClearConfirm_Title.Text(),
        Str.Common_Clear.Text(),
        ClipboardStr.WidgetsA_Clipboard_ClearConfirm_Body.Text());

    private void OnLanguage(Locale locale)
    {
        ToolTipService.SetToolTip(_clear, ClipboardStr.Clipboard_ClearHistory.Text());
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
        _fade.Dispose();
        _state.Detach();
        _ticker = null;
    }
}
