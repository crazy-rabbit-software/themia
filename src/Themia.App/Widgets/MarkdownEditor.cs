using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Notes;
using Themia.Core.Widgets;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Themia.Core.Shell;

namespace Themia.App.Widgets;

/// <summary>One button of the formatting bar.</summary>
internal enum NoteCommand
{
    Heading,
    Bold,
    Italic,
    Strike,
    Code,
    Bullet,
    Ordered,
    Task,
    Quote,
}

/// <summary>
/// [S-1366] The note editor: markdown edited in place over a <see cref="RichEditBox"/>, with the
/// markdown source as the one truth. Syntax markers stay on screen, dimmed; their effect (weight,
/// slant, strike, size, a monospace face) is applied to the text around them as character
/// formatting. Nothing is ever read back from the control's formatting — the text is read, parsed
/// (<see cref="NoteMarkdown"/>) and drawn again, so what is saved is exactly what was typed.
/// <para>
/// Every key the original intercepted goes through the pure rules in <see cref="NoteEditing"/>
/// ([S-1354]..[S-1359]); the control only applies the returned text as the smallest replacement
/// (<see cref="NoteText.Diff"/>) and places the caret. Undo is <see cref="NoteHistory"/>, with the
/// control's own stack switched off: that stack would also hold every formatting pass ([S-1360]).
/// Paste is plain text only ([S-1357]); the rich-text accelerators, the selection flyout and the
/// stock context menu (with its Bold/Italic/Underline) are gone, so no formatting the parser does
/// not know about can get in. An IME composition is never redrawn mid-way.
/// </para>
/// <para>
/// A composition, not a subclass: the view places <see cref="Box"/> and <see cref="Toolbar"/> where
/// its ladder says, and this class owns their behaviour.
/// </para>
/// </summary>
internal sealed class MarkdownEditor
{
    private const string Scope = "notes-editor";

    /// <summary>A monospace face every Windows install has.</summary>
    private const string MonoFont = "Consolas";

    /// <summary>VK_PROCESSKEY: a key an input method editor is consuming. Never intercepted.</summary>
    private const int ImeProcessKey = 0xE5;

    /// <summary>The bar in display order, with the order buttons are shed in as it narrows (0 = last to go).</summary>
    private static readonly (NoteCommand Command, string? Glyph, string? Label, Func<string> Tip, int Rank)[] BarButtons =
    [
        (NoteCommand.Heading, null, "H", () => Str.WidgetsC_Notes_Editor_HeadingTip.Text(), 4),
        (NoteCommand.Bold, Glyphs.Bold, null, () => WithShortcut(Str.Widget_Notes_Fmt_Bold, KeyChord.Ctrl(ChordKey.B)), 0),
        (NoteCommand.Italic, Glyphs.Italic, null, () => WithShortcut(Str.Widget_Notes_Fmt_Italic, KeyChord.Ctrl(ChordKey.I)), 1),
        (NoteCommand.Strike, Glyphs.Strikethrough, null, () => Str.Widget_Notes_Fmt_Strike.Text(), 5),
        (NoteCommand.Code, Glyphs.Code, null, () => Str.Widget_Notes_Fmt_Code.Text(), 6),
        (NoteCommand.Bullet, Glyphs.BulletList, null, () => Str.Widget_Notes_Fmt_Bullet.Text(), 3),
        (NoteCommand.Ordered, null, "1.", () => Str.Widget_Notes_Fmt_Numbered.Text(), 7),
        (NoteCommand.Task, Glyphs.Checklist, null, () => Str.Widget_Notes_Fmt_Task.Text(), 2),
        (NoteCommand.Quote, Glyphs.Quote, null, () => Str.Widget_Notes_Fmt_Quote.Text(), 8),
    ];

    private readonly Dictionary<FormatKey, ITextCharacterFormat> _formats = [];
    private readonly List<(NoteCommand Command, Button Button, int Rank)> _buttons = [];
    private readonly CommandBarFlyout _menu = new() { AlwaysExpanded = true };

    private string _text = string.Empty;
    private List<NoteLine> _lines = NoteMarkdown.Parse(string.Empty);
    private int[] _lineStarts = [0];
    private NoteHistory _history = new(string.Empty);

    /// <summary>
    /// Whether the control reports the story's final paragraph mark as text. Probed once rather than
    /// assumed, because a wrong guess either loses a trailing newline or invents one on every read.
    /// </summary>
    private readonly bool _finalMark;

    private string? _baseFont;
    private double _fontSize = 13;
    private bool _composing;
    private bool _flatHeadings;
    private bool _readOnly;
    private bool _detached;
    private int _visibleButtons = NotesLayout.AllButtons;

    public MarkdownEditor()
    {
        Box = new RichEditBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
            DisabledFormattingAccelerators = DisabledFormattingAccelerators.All,
            ClipboardCopyFormat = RichEditClipboardFormat.PlainText,
            SelectionFlyout = null,
            PlaceholderText = Str.Notes_Placeholder.Text(),
        };

        // [S-1360] One undo stack, and it is ours.
        Box.Document.UndoLimit = 0;

        Box.Document.SetText(TextSetOptions.None, "a");
        Box.Document.GetText(TextGetOptions.None, out var probe);
        _finalMark = probe.Length > 1;
        Box.Document.SetText(TextSetOptions.None, string.Empty);

        Toolbar = BuildToolbar();
        BuildMenu();

        Box.TextChanged += OnTextChanged;
        Box.SelectionChanged += OnSelectionChanged;
        Box.Paste += OnPaste;
        Box.PreviewKeyDown += OnPreviewKeyDown;
        Box.TextCompositionStarted += OnCompositionStarted;
        Box.TextCompositionEnded += OnCompositionEnded;
        Box.ActualThemeChanged += OnThemeChanged;
        Box.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), handledEventsToo: true);
    }

    /// <summary>The text control. The view places it and gives it its chrome.</summary>
    public RichEditBox Box { get; }

    /// <summary>[S-1365] The formatting bar. The view places it (the card's header, [S-3963]); <see cref="VisibleButtons"/> sheds it.</summary>
    public StackPanel Toolbar { get; }

    /// <summary>The note as it stands, with <c>\n</c> line endings.</summary>
    public string Text => _text;

    /// <summary>Raised after every edit the user made (typing, a command, undo, paste) — never for <see cref="Load"/>.</summary>
    public event Action<string>? TextEdited;

    /// <summary>Base text size in DIPs; headings are drawn larger than this.</summary>
    public double FontSize
    {
        get => _fontSize;
        set
        {
            if (Math.Abs(value - _fontSize) < 0.01)
            {
                return;
            }

            _fontSize = value;
            Box.FontSize = value;
            Restyle();
        }
    }

    public bool Wrap
    {
        set
        {
            Box.TextWrapping = value ? TextWrapping.Wrap : TextWrapping.NoWrap;
            ScrollViewer.SetHorizontalScrollBarVisibility(Box, value ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
        }
    }

    /// <summary>A note that is gone, or no note at all: shown, not editable.</summary>
    public bool IsReadOnly
    {
        get => _readOnly;
        set
        {
            _readOnly = value;
            Box.IsReadOnly = value;
            foreach (var (_, button, _) in _buttons)
            {
                button.IsEnabled = !value;
            }
        }
    }

    /// <summary>
    /// [S-1363] Headings at the body size (still semibold): on a tile this narrow a heading drawn
    /// half as large again wraps its marker onto a line of its own.
    /// </summary>
    public bool FlatHeadings
    {
        set
        {
            if (value != _flatHeadings)
            {
                _flatHeadings = value;
                Restyle();
            }
        }
    }

    /// <summary>[S-1365] How many buttons the bar shows; the rest are shed by rank, most used kept.</summary>
    public int VisibleButtons
    {
        get => _visibleButtons;
        set
        {
            _visibleButtons = value;
            foreach (var (_, button, rank) in _buttons)
            {
                button.Visibility = rank < value ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    /// <summary>
    /// [S-3963] The commands the bar has shed (rank at or past <see cref="VisibleButtons"/>), in the
    /// bar's order, as menu entries for its "More" button: each runs on the note's selection.
    /// </summary>
    public IReadOnlyList<ItemMenuEntry> ShedCommands()
    {
        var (start, end) = Selection();
        var marks = NoteEditing.ActiveMarks(_text, start, end);
        var entries = new List<ItemMenuEntry>();

        foreach (var (command, glyph, label, tip, rank) in BarButtons)
        {
            if (rank < _visibleButtons)
            {
                continue;
            }

            var run = command;
            entries.Add(new ItemMenuEntry(glyph, tip(), () => Run(run), Disabled: _readOnly, Checked: IsOn(command, marks)));
        }

        return entries;
    }

    /// <summary>[S-1359] Light the bar for the caret now: it was hidden while the marks moved.</summary>
    public void RefreshMarks() => UpdateMarks();

    /// <summary>
    /// [S-1348] Show <paramref name="text"/> as it is in the store — another note, or this note
    /// changed elsewhere. Starts a new undo history; raises no <see cref="TextEdited"/>.
    /// </summary>
    public void Load(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var clean = NoteEditing.NormalizeNewlines(text);
        if (string.Equals(clean, _text, StringComparison.Ordinal))
        {
            return;
        }

        var (start, end) = Selection();
        _history.Reset(clean);

        WithWritable(() => Replace(clean));
        Box.Document.Selection.SetRange(Math.Min(start, clean.Length), Math.Min(end, clean.Length));
        Commit(record: false, raise: false);
    }

    /// <summary>
    /// Show another note: always a new undo history (undo must never bring the previous note's text
    /// into this one), the caret at the start.
    /// </summary>
    public void Reset(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var clean = NoteEditing.NormalizeNewlines(text);
        _history.Reset(clean);

        WithWritable(() => Replace(clean));
        Box.Document.Selection.SetRange(0, 0);
        Commit(record: false, raise: false);
        UpdateMarks();
    }

    /// <summary>[S-1359] Run a bar command on the selection, focusing the note first so the caret lands where the command left it.</summary>
    public void Run(NoteCommand command)
    {
        if (_readOnly || _detached)
        {
            return;
        }

        if (Box.FocusState == FocusState.Unfocused)
        {
            Box.Focus(FocusState.Programmatic);
        }

        var (start, end) = Selection();
        Apply(command switch
        {
            NoteCommand.Heading => NoteEditing.CycleHeading(_text, start, end),
            NoteCommand.Bold => NoteEditing.ToggleWrap(_text, start, end, "**"),
            NoteCommand.Italic => NoteEditing.ToggleWrap(_text, start, end, "*"),
            NoteCommand.Strike => NoteEditing.ToggleWrap(_text, start, end, "~~"),
            NoteCommand.Code => NoteEditing.ToggleWrap(_text, start, end, "`"),
            NoteCommand.Bullet => NoteEditing.ToggleBlock(_text, start, end, NoteBlock.Bullet),
            NoteCommand.Ordered => NoteEditing.ToggleBlock(_text, start, end, NoteBlock.Ordered),
            NoteCommand.Task => NoteEditing.ToggleBlock(_text, start, end, NoteBlock.Task),
            _ => NoteEditing.ToggleBlock(_text, start, end, NoteBlock.Quote),
        });
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Box.TextChanged -= OnTextChanged;
        Box.SelectionChanged -= OnSelectionChanged;
        Box.Paste -= OnPaste;
        Box.PreviewKeyDown -= OnPreviewKeyDown;
        Box.TextCompositionStarted -= OnCompositionStarted;
        Box.TextCompositionEnded -= OnCompositionEnded;
        Box.ActualThemeChanged -= OnThemeChanged;
        Box.RemoveHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnPointerReleased));
    }

    // -----------------------------------------------------------------------------------------
    // Text in, text out
    // -----------------------------------------------------------------------------------------

    /// <summary>The control's text in the note's terms: <c>\n</c> for its paragraph marks, no final mark.</summary>
    private string ReadControl()
    {
        Box.Document.GetText(TextGetOptions.None, out var raw);
        raw = raw.Replace('\r', '\n');
        return _finalMark && raw.EndsWith('\n') ? raw[..^1] : raw;
    }

    private (int Start, int End) Selection()
    {
        var selection = Box.Document.Selection;
        var start = Math.Clamp(selection.StartPosition, 0, _text.Length);
        var end = Math.Clamp(selection.EndPosition, 0, _text.Length);
        return start <= end ? (start, end) : (end, start);
    }

    /// <summary>Swap only what changed, so the scroll position and every untouched line's formatting stay.</summary>
    private void Replace(string next)
    {
        var change = NoteText.Diff(ReadControl(), next);
        if (change.IsEmpty)
        {
            return;
        }

        var range = Box.Document.GetRange(change.Start, change.Start + change.RemoveLength);
        range.SetText(TextSetOptions.None, change.Insert.Replace('\n', '\r'));
    }

    private void WithWritable(Action action)
    {
        var locked = Box.IsReadOnly;
        Box.IsReadOnly = false;

        try
        {
            action();
        }
        finally
        {
            Box.IsReadOnly = locked;
        }
    }

    /// <summary>Apply an edit from <see cref="NoteEditing"/>: the text, then the caret, then one commit.</summary>
    private void Apply(NoteEdit edit)
    {
        Replace(edit.Text);
        Box.Document.Selection.SetRange(edit.Start, edit.End);
        Box.Document.Selection.ScrollIntoView(PointOptions.None);
        Commit(record: true, raise: true);
    }

    /// <summary>
    /// The one path from the control's text to everything else: parse, draw the lines that changed,
    /// record the step, tell the view. A change that left the text as it was (a formatting pass, or
    /// our own edit echoed back) does nothing.
    /// </summary>
    private void Commit(bool record, bool raise)
    {
        var now = ReadControl();
        if (string.Equals(now, _text, StringComparison.Ordinal))
        {
            return;
        }

        var before = _text;
        var beforeLines = _lines;

        _text = now;
        _lines = NoteMarkdown.Parse(now);
        _lineStarts = LineStarts(now);

        Draw(NoteText.LinesToRedraw(before, beforeLines, now, _lines));

        if (record)
        {
            var (start, end) = Selection();
            _history.Record(now, start, end);
        }

        UpdateMarks();

        if (raise)
        {
            TextEdited?.Invoke(now);
        }
    }

    private static int[] LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return [.. starts];
    }

    // -----------------------------------------------------------------------------------------
    // Drawing
    // -----------------------------------------------------------------------------------------

    private enum Ink
    {
        Primary,
        Secondary,
        Tertiary,
        Accent,
    }

    private readonly record struct FormatKey(float Points, int Weight, bool Italic, bool Strike, bool Mono, Ink Ink);

    /// <summary>Forget every cached format and draw every line: the size or the theme changed.</summary>
    private void Restyle()
    {
        _formats.Clear();
        Draw(Enumerable.Range(0, _lines.Count));
    }

    private void Draw(IEnumerable<int> lines)
    {
        var document = Box.Document;
        document.BatchDisplayUpdates();

        try
        {
            foreach (var index in lines)
            {
                DrawLine(index);
            }
        }
        catch (Exception ex)
        {
            // A formatting call that fails leaves a line plain; the text is untouched either way.
            Log.Error(Scope, "could not draw the note's formatting", ex);
        }
        finally
        {
            document.ApplyDisplayUpdates();
        }
    }

    /// <summary>
    /// [S-1352] Every span gets a whole format — the spans cover the line exactly, so this also
    /// resets whatever a drop or an inherited format left on it.
    /// </summary>
    private void DrawLine(int index)
    {
        if (index < 0 || index >= _lines.Count || index >= _lineStarts.Length)
        {
            return;
        }

        var line = _lines[index];
        var at = _lineStarts[index];

        foreach (var span in line.Spans)
        {
            var range = Box.Document.GetRange(at, at + span.Text.Length);
            var key = KeyFor(line, span);
            range.CharacterFormat = Format(key);

            // The face is set on the range itself as well: a whole-format assignment carries the
            // weight, slant and colour, but the control keeps the run's face.
            range.CharacterFormat.Name = key.Mono ? MonoFont : _baseFont ?? "Segoe UI";
            at += span.Text.Length;
        }
    }

    private FormatKey KeyFor(NoteLine line, NoteSpan span)
    {
        var scale = _flatHeadings ? 1.0 : line.Kind switch
        {
            NoteLineKind.Heading1 => 1.5,
            NoteLineKind.Heading2 => 1.3,
            NoteLineKind.Heading3 => 1.15,
            _ => 1.0,
        };

        var heading = line.Kind is NoteLineKind.Heading1 or NoteLineKind.Heading2 or NoteLineKind.Heading3;
        var weight = span.Has(NoteSpanStyle.Bold) ? 700 : heading ? 600 : 400;
        var mono = span.Has(NoteSpanStyle.Code) || line.Kind is NoteLineKind.Code or NoteLineKind.Fence;

        var ink = span.Has(NoteSpanStyle.Box) ? Ink.Accent
            : span.Has(NoteSpanStyle.Marker) ? Ink.Tertiary
            : line.Kind is NoteLineKind.Code or NoteLineKind.Quote || (line.Kind == NoteLineKind.Task && line.Checked) ? Ink.Secondary
            : Ink.Primary;

        // RichEdit sizes are points; the note's size is DIPs (96 per inch, 72 points per inch).
        return new FormatKey((float)(_fontSize * scale * 0.75), weight, span.Has(NoteSpanStyle.Italic), span.Has(NoteSpanStyle.Strike), mono, ink);
    }

    private ITextCharacterFormat Format(FormatKey key)
    {
        if (_formats.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var format = Box.Document.GetDefaultCharacterFormat().GetClone();
        _baseFont ??= string.IsNullOrWhiteSpace(format.Name) ? "Segoe UI" : format.Name;

        format.Name = key.Mono ? MonoFont : _baseFont;
        format.Size = key.Points;
        format.Weight = key.Weight;
        format.Italic = key.Italic ? FormatEffect.On : FormatEffect.Off;
        format.Strikethrough = key.Strike ? FormatEffect.On : FormatEffect.Off;
        format.Underline = UnderlineType.None;
        format.ForegroundColor = Color(key.Ink);

        _formats[key] = format;
        return format;
    }

    /// <summary>
    /// The theme's text colours, made opaque over the card's colour: the control draws a text colour
    /// without its alpha, so the secondary and tertiary fills — white at 60 % and 40 % in the dark
    /// theme — would otherwise all come out as the primary.
    /// </summary>
    private static Color Color(Ink ink)
    {
        var key = ink switch
        {
            Ink.Secondary => "TextFillColorSecondaryBrush",
            Ink.Tertiary => "TextFillColorTertiaryBrush",
            Ink.Accent => "AccentTextFillColorPrimaryBrush",
            _ => "TextFillColorPrimaryBrush",
        };

        var fill = (FluentLayout.Themed(key, Colors.Gray) as SolidColorBrush)?.Color ?? Colors.Gray;
        var under = (FluentLayout.Themed("SolidBackgroundFillColorBaseBrush", Colors.Black) as SolidColorBrush)?.Color ?? Colors.Black;
        return Blend(fill, under);
    }

    private static Color Blend(Color over, Color under)
    {
        var a = over.A / 255.0;
        return Windows.UI.Color.FromArgb(
            255,
            (byte)Math.Round((over.R * a) + (under.R * (1 - a))),
            (byte)Math.Round((over.G * a) + (under.G * (1 - a))),
            (byte)Math.Round((over.B * a) + (under.B * (1 - a))));
    }

    private void OnThemeChanged(FrameworkElement sender, object args) => Restyle();

    // -----------------------------------------------------------------------------------------
    // Input
    // -----------------------------------------------------------------------------------------

    private void OnTextChanged(object sender, RoutedEventArgs args)
    {
        if (_composing || _detached)
        {
            return;
        }

        Commit(record: true, raise: true);
    }

    private void OnCompositionStarted(RichEditBox sender, TextCompositionStartedEventArgs args) => _composing = true;

    /// <summary>The composition is text now; draw it, record it, save it.</summary>
    private void OnCompositionEnded(RichEditBox sender, TextCompositionEndedEventArgs args)
    {
        _composing = false;

        if (!_detached)
        {
            Commit(record: true, raise: true);
        }
    }

    private void OnSelectionChanged(object sender, RoutedEventArgs args)
    {
        if (_composing || _detached)
        {
            return;
        }

        var (start, end) = Selection();
        _history.Record(_text, start, end);
        UpdateMarks();
    }

    private static bool Down(VirtualKey key) => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);

    /// <summary>
    /// [S-1354]..[S-1356], [S-1360] The keys the note answers itself, caught before the control acts on
    /// them. Ctrl with anything other than clipboard, selection and caret keys is swallowed: RichEdit
    /// binds alignment, spacing, sub/superscript and list numbering to Ctrl chords, and each would put
    /// formatting into the note that the markdown does not hold. Ctrl+Alt is AltGr — how many layouts
    /// type @, € or { — so it always passes.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (_composing || _readOnly || _detached || (int)args.Key == ImeProcessKey)
        {
            return;
        }

        var ctrl = Down(VirtualKey.Control);
        var shift = Down(VirtualKey.Shift);
        var alt = Down(VirtualKey.Menu);
        var (start, end) = Selection();

        if (ctrl && !alt)
        {
            args.Handled = true;

            switch (args.Key)
            {
                case VirtualKey.B:
                    Apply(NoteEditing.ToggleWrap(_text, start, end, "**"));
                    return;

                case VirtualKey.I:
                    Apply(NoteEditing.ToggleWrap(_text, start, end, "*"));
                    return;

                case VirtualKey.Z:
                    Step(redo: shift);
                    return;

                case VirtualKey.Y:
                    Step(redo: true);
                    return;

                case VirtualKey.A or VirtualKey.C or VirtualKey.X or VirtualKey.V or VirtualKey.Insert
                    or VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down
                    or VirtualKey.Home or VirtualKey.End or VirtualKey.PageUp or VirtualKey.PageDown
                    or VirtualKey.Back or VirtualKey.Delete
                    or VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
                    or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift:
                    args.Handled = false;
                    return;

                default:
                    return;
            }
        }

        switch (args.Key)
        {
            case VirtualKey.Enter when !alt:
                args.Handled = true;

                // Shift+Enter is a plain break: no list marker carried over.
                Apply(shift ? NoteEditing.InsertText(_text, start, end, "\n") : NoteEditing.PressEnter(_text, start, end));
                return;

            case VirtualKey.Tab when !ctrl && !alt:
                args.Handled = true;
                Apply(NoteEditing.ShiftIndent(_text, start, end, outdent: shift));
                return;
        }
    }

    /// <summary>[S-1360] Undo or redo: the snapshot's text and selection, without recording a new step.</summary>
    private void Step(bool redo)
    {
        if ((redo ? _history.Redo() : _history.Undo()) is not { } snapshot)
        {
            return;
        }

        Replace(snapshot.Text);
        Box.Document.Selection.SetRange(snapshot.Start, snapshot.End);
        Box.Document.Selection.ScrollIntoView(PointOptions.None);
        Commit(record: false, raise: true);
    }

    /// <summary>[S-1357] The control's own paste is cancelled; the clipboard's plain text goes in through the rules.</summary>
    private void OnPaste(object sender, TextControlPasteEventArgs args)
    {
        args.Handled = true;

        if (!_readOnly && !_detached)
        {
            _ = PasteAsync();
        }
    }

    private async Task PasteAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Text))
            {
                return;
            }

            var text = await content.GetTextAsync();
            if (_detached || _readOnly || string.IsNullOrEmpty(text))
            {
                return;
            }

            // Read after the await: the caret may have moved while the clipboard answered.
            var (start, end) = Selection();
            Apply(NoteEditing.InsertText(_text, start, end, text));
        }
        catch (Exception ex)
        {
            // The clipboard is another process's: busy, or holding something that is not text.
            Log.Warn(Scope, "could not paste", ex);
        }
    }

    /// <summary>[S-1358] A click on a task's <c>[ ]</c> ticks it; length-preserving, so the caret stays put.</summary>
    private void OnPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (_readOnly || _detached || args.GetCurrentPoint(Box).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased)
        {
            return;
        }

        var (start, end) = Selection();
        if (start != end || NoteEditing.TaskBoxAt(_text, start) is not { } line || NoteEditing.ToggleTaskLine(_text, line) is not { } toggled)
        {
            return;
        }

        Apply(new NoteEdit(toggled, start, end));
    }

    // -----------------------------------------------------------------------------------------
    // Toolbar and menu
    // -----------------------------------------------------------------------------------------

    private StackPanel BuildToolbar()
    {
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = NotesLayout.ButtonGap,
            Height = NotesLayout.ButtonSize,
            VerticalAlignment = VerticalAlignment.Center,
        };

        foreach (var (command, glyph, label, tip, rank) in BarButtons)
        {
            object content = glyph is not null
                ? new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize }
                : LabelFor(label ?? string.Empty);

            var button = new Button
            {
                Content = content,
                Width = NotesLayout.ButtonSize,
                Height = NotesLayout.ButtonSize,
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(Fluent.ControlRadius),
                Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent),

                // [S-1365] The note keeps the focus and its selection; the command runs on that.
                IsTabStop = false,
                AllowFocusOnInteraction = false,
            };

            ToolTipService.SetToolTip(button, tip());
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tip());
            button.Click += (_, _) => Run(command);

            _buttons.Add((command, button, rank));
            bar.Children.Add(button);
        }

        return bar;
    }

    /// <summary>"Bold (Ctrl+B)": a command's name with its key, in the language's word order.</summary>
    private static string WithShortcut(Str name, KeyChord keys)
        => Str.WidgetsC_Notes_Editor_WithShortcut.Format(("name", name.Text()), ("keys", KeyChordText.Format(keys)));

    /// <summary>
    /// [S-1110] Re-read the texts this editor set once (the bar's tooltips, the placeholder) after
    /// the language changed. The context menu is built on every open, so it follows by itself.
    /// </summary>
    public void RefreshTexts()
    {
        Box.PlaceholderText = Str.Notes_Placeholder.Text();
        foreach (var (command, button, _) in _buttons)
        {
            foreach (var entry in BarButtons)
            {
                if (entry.Command == command)
                {
                    var tip = entry.Tip();
                    ToolTipService.SetToolTip(button, tip);
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tip);
                }
            }
        }
    }

    private static TextBlock LabelFor(string label)
    {
        var text = Fluent.Text(FluentType.BodyStrong, "TextFillColorSecondaryBrush");
        text.Text = label;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        return text;
    }

    /// <summary>[S-1359] Light the buttons for what is in force at the caret.</summary>
    private void UpdateMarks()
    {
        if (Toolbar.Visibility != Visibility.Visible)
        {
            return;
        }

        var (start, end) = Selection();
        var marks = NoteEditing.ActiveMarks(_text, start, end);

        foreach (var (command, button, _) in _buttons)
        {
            var on = IsOn(command, marks);

            button.Background = FluentLayout.Themed(on ? "SubtleFillColorSecondaryBrush" : "SubtleFillColorTransparentBrush", Colors.Transparent);
            button.Foreground = FluentLayout.Themed(on ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush", Colors.Gray);
        }
    }

    private static bool IsOn(NoteCommand command, NoteMarks marks) => command switch
    {
        NoteCommand.Heading => marks.Heading > 0,
        NoteCommand.Bold => marks.Bold,
        NoteCommand.Italic => marks.Italic,
        NoteCommand.Strike => marks.Strike,
        NoteCommand.Code => marks.Code,
        NoteCommand.Bullet => marks.Block == NoteBlock.Bullet,
        NoteCommand.Ordered => marks.Block == NoteBlock.Ordered,
        NoteCommand.Task => marks.Block == NoteBlock.Task,
        _ => marks.Block == NoteBlock.Quote,
    };

    /// <summary>
    /// The note's own right-click menu, in place of the control's: that one carries Bold, Italic and
    /// Underline, which would put formatting into the note that its markdown does not hold.
    /// </summary>
    private void BuildMenu()
    {
        _menu.Opening += (_, _) =>
        {
            var (start, end) = Selection();
            var selected = start != end;

            ItemMenu.Fill(_menu,
            [
                new ItemMenuEntry(Glyphs.Undo, Str.WidgetsC_Notes_Editor_Undo.Text(), () => Step(redo: false), Disabled: _readOnly || !_history.CanUndo),
                new ItemMenuEntry(Glyphs.Reset, Str.WidgetsC_Notes_Editor_Redo.Text(), () => Step(redo: true), Disabled: _readOnly || !_history.CanRedo),
                ItemMenuEntry.Separator,
                new ItemMenuEntry(Glyphs.Cut, Str.Common_Cut.Text(), () => Box.Document.Selection.Cut(), Disabled: _readOnly || !selected, Quick: QuickCommand.Cut),
                new ItemMenuEntry(Glyphs.Copy, Str.Common_Copy.Text(), () => Box.Document.Selection.Copy(), Disabled: !selected, Quick: QuickCommand.Copy),
                new ItemMenuEntry(Glyphs.Paste, Str.Common_Paste.Text(), () => _ = PasteAsync(), Disabled: _readOnly, Quick: QuickCommand.Paste),
                ItemMenuEntry.Separator,
                new ItemMenuEntry(null, Str.Folder_SelectAll.Text(), () => Box.Document.Selection.SetRange(0, _text.Length)),
            ]);
        };

        Box.ContextFlyout = _menu;
    }
}
