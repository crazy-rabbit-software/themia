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
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Themia.Core.Shell;
using Themia.Widget.Notes;

namespace Themia.App.Widgets;

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
/// Paste is plain text only ([S-1357]); the rich-text accelerators, the stock selection flyout and the
/// stock context menu (with its Bold/Italic/Underline) are gone, so no formatting the parser does
/// not know about can get in. An IME composition is never redrawn mid-way.
/// </para>
/// <para>
/// [S-14772] The formats are the text's own menus: the selection's bar, opened at the pointer when it lets go over text it
/// selected, and the text's menu (a right-click, Shift+F10, the Menu key), both through <see cref="ItemMenu"/> from the
/// rules of <see cref="NoteMenus.Text"/>.
/// </para>
/// <para>
/// A composition, not a subclass: the view places <see cref="Box"/>, and this class owns its behaviour.
/// </para>
/// </summary>
internal sealed class MarkdownEditor
{
    private const string Scope = "notes-editor";

    /// <summary>A monospace face every Windows install has.</summary>
    private const string MonoFont = "Consolas";

    /// <summary>VK_PROCESSKEY: a key an input method editor is consuming. Never intercepted.</summary>
    private const int ImeProcessKey = 0xE5;

    private readonly Dictionary<FormatKey, ITextCharacterFormat> _formats = [];

    /// <summary>[S-14772] The text's menu (a right-click, Shift+F10, the Menu key): filled from the rules each time it opens.</summary>
    private readonly CommandBarFlyout _menu = new() { AlwaysExpanded = true };

    /// <summary>[S-14772] The selection's bar while it is open, so typing or a click in the text closes it.</summary>
    private FlyoutBase? _bar;

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
    private double _fontSize = FamilyType.Body.FontSize;
    private bool _composing;
    private bool _readOnly;
    private bool _detached;

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
            PlaceholderText = NotesStr.Notes_Placeholder.Text(),
        };

        // [S-1360] One undo stack, and it is ours.
        Box.Document.UndoLimit = 0;

        Box.Document.SetText(TextSetOptions.None, "a");
        Box.Document.GetText(TextGetOptions.None, out var probe);
        _finalMark = probe.Length > 1;
        Box.Document.SetText(TextSetOptions.None, string.Empty);

        BuildMenu();

        Box.TextChanged += OnTextChanged;
        Box.SelectionChanged += OnSelectionChanged;
        Box.Paste += OnPaste;
        Box.PreviewKeyDown += OnPreviewKeyDown;
        Box.TextCompositionStarted += OnCompositionStarted;
        Box.TextCompositionEnded += OnCompositionEnded;
        Box.ActualThemeChanged += OnThemeChanged;
        Box.LostFocus += OnLostFocus;
        Box.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed), handledEventsToo: true);
        Box.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), handledEventsToo: true);
    }

    /// <summary>The text control. The view places it and gives it its chrome.</summary>
    public RichEditBox Box { get; }

    /// <summary>The note as it stands, with <c>\n</c> line endings.</summary>
    public string Text => _text;

    /// <summary>[S-14771] How tall the note's first line stands (a title's 28, a body line's 20): the pill stands over it.</summary>
    public double FirstLine => LineOf(_lines.Count > 0 ? _lines[0] : NoteMarkdown.Parse(string.Empty)[0]);

    /// <summary>Raised after every edit the user made (typing, a command, undo, paste) — never for <see cref="Load"/>.</summary>
    public event Action<string>? TextEdited;

    /// <summary>[S-14771] A key was pressed in the text (a modifier alone excepted): the user is at the keyboard.</summary>
    public event Action? Keyed;

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
            if (value)
            {
                CloseBar();
            }
        }
    }

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
    }

    /// <summary>[S-1359] Run a format on the selection, focusing the note first so the caret lands where the command left it.</summary>
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
        Box.LostFocus -= OnLostFocus;
        Box.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed));
        Box.RemoveHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnPointerReleased));
        CloseBar();
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

        if (raise)
        {
            // [S-14772] The text was typed in (or a command ran): the selection's bar has done its work.
            CloseBar();
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

    /// <summary>[S-1358] The text roles a note is drawn in: never the accent ([S-6706]).</summary>
    private enum Ink
    {
        Primary,
        Secondary,
        Tertiary,
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

        // [S-6700] The line is its step's exactly: a title's 28, a body line's 20 (RichEdit's spacing is in points).
        var paragraph = Box.Document.GetRange(at, at + line.Spans.Sum(s => s.Text.Length));
        paragraph.ParagraphFormat.SetLineSpacing(LineSpacingRule.Exactly, (float)(LineOf(line) * 0.75));

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

    /// <summary>
    /// [S-6700] A note's title (a first-level heading) is Title, 20 on its 28 line; every other line is
    /// Body, 14 on 20, a lower heading in BodyStrong. On a compact width the title is BodyStrong too
    /// ([S-1363]): there a heading half as large again wraps its marker onto a line of its own.
    /// </summary>
    private bool IsTitle(NoteLine line) => line.Kind == NoteLineKind.Heading1;

    /// <summary>The line a note's line stands on, in DIPs: the title's or the body's, at the note's text size.</summary>
    private double LineOf(NoteLine line)
        => _fontSize * (IsTitle(line) ? FamilyType.Title.LineHeight : FamilyType.Body.LineHeight) / FamilyType.Body.FontSize;

    private FormatKey KeyFor(NoteLine line, NoteSpan span)
    {
        var scale = IsTitle(line) ? FamilyType.Title.FontSize / FamilyType.Body.FontSize : 1.0;

        // [S-6700] Two weights: a heading and a bold span are 600, everything else 400.
        var heading = line.Kind is NoteLineKind.Heading1 or NoteLineKind.Heading2 or NoteLineKind.Heading3;
        var weight = span.Has(NoteSpanStyle.Bold) || heading ? FontWeights.SemiBold.Weight : FontWeights.Normal.Weight;
        var mono = span.Has(NoteSpanStyle.Code) || line.Kind is NoteLineKind.Code or NoteLineKind.Fence;

        // [S-1358] The marks (and a task's box) are tertiary, never the accent; a done task, code and a quote are secondary.
        var ink = span.Has(NoteSpanStyle.Box) || span.Has(NoteSpanStyle.Marker) ? Ink.Tertiary
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
    /// [S-6738] The text roles, as the opaque colours they show on the card: the control draws a text
    /// colour without its alpha, so the secondary and tertiary roles — white at 77 % and 53 % in the dark
    /// theme — would otherwise all come out as the primary.
    /// </summary>
    private static Color Color(Ink ink) => HexBrush.OnCard(ink switch
    {
        Ink.Secondary => ColourRole.TextSecondary,
        Ink.Tertiary => ColourRole.TextTertiary,
        _ => ColourRole.TextPrimary,
    });

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
        if (_detached)
        {
            return;
        }

        if (!IsModifier(args.Key))
        {
            // [S-14772] Back at the keyboard: the selection's bar goes (Escape closes it and nothing else), [S-14771] and the
            // view hears it (the pill leaves the text to the keys).
            if (_bar is not null && args.Key == VirtualKey.Escape)
            {
                args.Handled = true;
                CloseBar();
                return;
            }

            CloseBar();
            Keyed?.Invoke();
        }

        if (_composing || _readOnly || (int)args.Key == ImeProcessKey)
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

    private static bool IsModifier(VirtualKey key) => key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
        or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
        or VirtualKey.LeftWindows or VirtualKey.RightWindows;

    /// <summary>[S-14772] A press in the text (to place the caret, to select again, a right-click) closes the selection's bar.</summary>
    private void OnPointerPressed(object sender, PointerRoutedEventArgs args) => CloseBar();

    /// <summary>
    /// [S-1358] A click on a task's <c>[ ]</c> ticks it; length-preserving, so the caret stays put. [S-14772] Letting go over
    /// text the pointer selected opens the selection's bar there (<see cref="NoteMenus.OpensOnRelease"/>).
    /// </summary>
    private void OnPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (_readOnly || _detached)
        {
            return;
        }

        var point = args.GetCurrentPoint(Box);
        var left = point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased;
        var (start, end) = Selection();

        if (NoteMenus.OpensOnRelease(left, start, end, _readOnly))
        {
            OpenBar(point.Position);
            return;
        }

        if (!left || start != end || NoteEditing.TaskBoxAt(_text, start) is not { } line || NoteEditing.ToggleTaskLine(_text, line) is not { } toggled)
        {
            return;
        }

        Apply(new NoteEdit(toggled, start, end));
    }

    /// <summary>
    /// [S-14772] Focus that leaves the text for anything but a menu (the bar's own buttons may take it while they are
    /// pressed) closes the selection's bar.
    /// </summary>
    private void OnLostFocus(object sender, RoutedEventArgs args)
    {
        if (_bar is null || Box.XamlRoot is not { } root)
        {
            return;
        }

        if (FocusManager.GetFocusedElement(root) is DependencyObject focused && InPopup(root, focused))
        {
            return;
        }

        CloseBar();
    }

    /// <summary>Whether <paramref name="element"/> stands in one of the open popups (a menu) of <paramref name="root"/>.</summary>
    private static bool InPopup(XamlRoot root, DependencyObject element)
    {
        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(root))
        {
            for (var node = element; node is not null; node = VisualTreeHelper.GetParent(node))
            {
                if (ReferenceEquals(node, popup.Child))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // -----------------------------------------------------------------------------------------
    // The text's menus
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-14772] Open the selection's bar at <paramref name="at"/> (a point of <see cref="Box"/>: where the pointer let go):
    /// the formats alone, lit for what is in force at the selection, without taking the keyboard.
    /// </summary>
    public void OpenBar(Windows.Foundation.Point at)
    {
        CloseBar();

        // Closed comes later than a hide: a bar closed for this one must not forget this one when it reports.
        FlyoutBase? shown = null;
        shown = ItemMenu.ShowBar(Box, at, Entries(NoteMenuOpening.Selected), () =>
        {
            if (ReferenceEquals(_bar, shown))
            {
                _bar = null;
            }
        });
        _bar = shown;
    }

    private void CloseBar()
    {
        var bar = _bar;
        _bar = null;
        bar?.Hide();
    }

    /// <summary>[S-14772] The text's menu (<paramref name="opening"/>) as menu entries, from the rules (<see cref="NoteMenus.Text"/>).</summary>
    private List<ItemMenuEntry> Entries(NoteMenuOpening opening)
    {
        var (start, end) = Selection();
        var lines = NoteMenus.Text(opening, start != end, _readOnly, _history.CanUndo, _history.CanRedo, NoteEditing.ActiveMarks(_text, start, end));
        return [.. lines.Select(Entry)];
    }

    private ItemMenuEntry Entry(NoteMenuLine line)
    {
        if (line.Divider)
        {
            return ItemMenuEntry.Separator;
        }

        return line.Item switch
        {
            NoteMenuItem.Cut => new(Glyphs.Cut, NotesStr.Common_Cut.Text(), () => Box.Document.Selection.Cut(), Disabled: line.Disabled, Quick: line.Quick, Shortcut: KeyChord.Ctrl(ChordKey.X)),
            NoteMenuItem.Copy => new(Glyphs.Copy, Str.Common_Copy.Text(), () => Box.Document.Selection.Copy(), Disabled: line.Disabled, Quick: line.Quick, Shortcut: KeyChord.Ctrl(ChordKey.C)),
            NoteMenuItem.Paste => new(Glyphs.Paste, NotesStr.Common_Paste.Text(), () => _ = PasteAsync(), Disabled: line.Disabled, Quick: line.Quick, Shortcut: KeyChord.Ctrl(ChordKey.V)),
            NoteMenuItem.Undo => new(Glyphs.Undo, NotesStr.WidgetsC_Notes_Editor_Undo.Text(), () => Step(redo: false), Disabled: line.Disabled, Shortcut: KeyChord.Ctrl(ChordKey.Z)),
            NoteMenuItem.Redo => new(Glyphs.Reset, NotesStr.WidgetsC_Notes_Editor_Redo.Text(), () => Step(redo: true), Disabled: line.Disabled, Shortcut: KeyChord.Ctrl(ChordKey.Y)),
            NoteMenuItem.SelectAll => new(null, Str.Folder_SelectAll.Text(), () => Box.Document.Selection.SetRange(0, _text.Length), Shortcut: KeyChord.Ctrl(ChordKey.A)),
            _ => Format(line),
        };
    }

    /// <summary>
    /// [S-1365] A format's line: its icon (Segoe Fluent's where the font has one, checked against it; "H" and "1." where it has
    /// none), its name, its key where it has one, lit while in force ([S-1359]).
    /// </summary>
    private ItemMenuEntry Format(NoteMenuLine line)
    {
        var command = line.Format ?? NoteCommand.Bold;
        var (glyph, name, keys) = command switch
        {
            NoteCommand.Bold => (Glyphs.Bold, NotesStr.Widget_Notes_Fmt_Bold.Text(), KeyChord.Ctrl(ChordKey.B)),
            NoteCommand.Italic => (Glyphs.Italic, NotesStr.Widget_Notes_Fmt_Italic.Text(), KeyChord.Ctrl(ChordKey.I)),
            NoteCommand.Strike => (Glyphs.Strikethrough, NotesStr.Widget_Notes_Fmt_Strike.Text(), (KeyChord?)null),
            NoteCommand.Code => (Glyphs.Code, NotesStr.Widget_Notes_Fmt_Code.Text(), null),
            NoteCommand.Heading => ("H", NotesStr.WidgetsC_Notes_Editor_HeadingTip.Text(), null),
            NoteCommand.Bullet => (Glyphs.BulletList, NotesStr.Widget_Notes_Fmt_Bullet.Text(), null),
            NoteCommand.Ordered => ("1.", NotesStr.Widget_Notes_Fmt_Numbered.Text(), null),
            NoteCommand.Task => (Glyphs.Checklist, NotesStr.Widget_Notes_Fmt_Task.Text(), null),
            _ => (Glyphs.Quote, NotesStr.Widget_Notes_Fmt_Quote.Text(), null),
        };

        return new ItemMenuEntry(glyph, name, () => Run(command), Disabled: line.Disabled, Checked: line.On, Quick: line.Quick, Shortcut: keys);
    }

    /// <summary>[S-1110] Re-read the texts this editor set once (the placeholder) after the language changed; the menus are built on every open.</summary>
    public void RefreshTexts() => Box.PlaceholderText = NotesStr.Notes_Placeholder.Text();

    /// <summary>
    /// [S-14772] The text's own menu, in place of the control's: that one carries Bold, Italic and Underline, which would put
    /// formatting into the note that its markdown does not hold. Opened by a right-click, Shift+F10 or the Menu key.
    /// </summary>
    private void BuildMenu()
    {
        _menu.Opening += (_, _) =>
        {
            CloseBar();
            ItemMenu.Fill(_menu, Entries(NoteMenuOpening.Context));
        };

        Box.ContextFlyout = _menu;
    }
}
