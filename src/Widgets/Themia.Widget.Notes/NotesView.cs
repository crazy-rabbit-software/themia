using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Shell.Motion;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Notes;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Notes;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1346] A markdown note from the notes kept on this PC (<see cref="IWidgetHost.Notes"/>).
/// <para>
/// The widget is a window onto a note, not the note: its config holds only which note
/// (<see cref="NotesConfig.NoteId"/>), so the note outlives the widget, two widgets can show one
/// note, and switching or deleting a widget never touches the text. A widget with no note yet gets a
/// new one of its own the first time it is shown. A widget whose note was deleted elsewhere says so
/// rather than quietly showing another ([S-1347]).
/// </para>
/// <para>
/// The editing itself is <see cref="MarkdownEditor"/> (its formats are the text's own menus, [S-14772]); this view binds it to
/// the pool, saves on a debounce and on blur and detach, and draws the text alone from the card's top inset
/// (<see cref="NotesLayout"/>, [S-14770]) with the pill over its top end ([S-14771]: the note's name with the picker, its
/// "…", the problem glyph while a write failed), shown while the pointer is over the card. A note it cannot show is the
/// Status row. It shows the one note at every size; another note is picked from the pill, never from a list beside the text.
/// </para>
/// </summary>
internal sealed partial class NotesView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetSizeAware, IWidgetTopEdge
{
    private const string Scope = "notes";

    /// <summary>
    /// [S-128] How long typing pauses before the note is handed to the pool. A write-rate limit, not
    /// a correctness path: <see cref="Flush"/> on blur and on <see cref="Detach"/> is what keeps the
    /// last sentence.
    /// </summary>
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(400);

    /// <summary>What the widget can show.</summary>
    private enum NoteState
    {
        /// <summary>The note, editable.</summary>
        Ready,

        /// <summary>A preview card (a debug render) with no note: the empty editor, nothing is created.</summary>
        Unbound,

        /// <summary>[S-1347] The note this widget shows was deleted.</summary>
        Deleted,

        /// <summary>The note exists but its file could not be read this time.</summary>
        Unreadable,

        /// <summary>No note pool at all.</summary>
        Unavailable,
    }

    private readonly MarkdownEditor _editor = new();

    /// <summary>[S-1347] [S-6714] What the widget says instead of the note it cannot show: the Status row at the top.</summary>
    private readonly StatusBlock _message = new();

    /// <summary>[S-1362] The pill's problem glyph while the note's last write failed.</summary>
    private readonly FontIcon _notSavedGlyph;

    /// <summary>[S-14771] Over the text's top end: the problem glyph, the picker (the note's name and its chevron) and "…".</summary>
    private readonly Border _pill;
    private readonly Border _more;
    private readonly DispatcherQueueTimer? _saveTimer;

    private IWidgetSurface? _surface;
    private readonly IWidgetHost _host;
    private INotes? _pool;
    private NotesConfig _config = new();
    private string? _shownId;
    private NoteState _state = NoteState.Unbound;
    private WidgetArea _area;
    private bool _dirty;
    private bool _resolving;
    private bool _editorFocused;

    /// <summary>[S-14771] When the pill shows: the pointer over the card, typing, its menus, a failed write.</summary>
    private NotesPill _shows;

    private bool _detached;

    public NotesView(IWidgetHost host)
    {
        _host = host;

        BuildEditor();

        // [S-14770] The text from the card's top edge, the inset its own padding on all four sides (the view owns the
        // edge, IWidgetTopEdge); [S-12815] without a note the Status row stands on the card's top edge in its place.
        _message.Visibility = Visibility.Collapsed;
        _message.Margin = default;
        Children.Add(_editor.Box);
        Children.Add(_message);

        // [S-14771] Pointer anywhere over the card: the view takes it over its whole box (a clear fill), however the text
        // and the pill handle it.
        Background = Family.Brush(ColourRole.Clear);

        _notSavedGlyph = ToolLine.Problem(NotesStr.WidgetsC_Notes_NotSaved.Text());
        _notSavedGlyph.Margin = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0);
        _notSavedGlyph.Visibility = Visibility.Collapsed;

        BuildPicker();

        _more = ToolLine.Button(Glyphs.More, NotesStr.WidgetsC_Notes_MoreOptions.Text(), OnMoreClick);

        _pill = BuildPill();
        Children.Add(_pill);

        _editor.TextEdited += OnTextEdited;
        _editor.TextEdited += OnFirstLine;

        // [S-14773] Whole lines at the foot: measured once the text or the box is laid out again.
        _editor.Box.TextChanged += (_, _) => FitLinesSoon();
        _editor.Box.SizeChanged += (_, _) => FitLinesSoon();
        _editor.Keyed += OnKeyed;
        _editor.Box.GotFocus += OnEditorGotFocus;
        _editor.Box.LostFocus += OnEditorLostFocus;
        AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerMoved), handledEventsToo: true);
        AddHandler(PointerExitedEvent, new PointerEventHandler(OnPointerExited), handledEventsToo: true);

        // [S-12831] A render's --hover over the note stands for the pointer over the card: the pill shows.
        PointerPreview.Register(_editor.Box, _ => Pill(_shows.Moved()));

        // The dispatcher timer is the debounce. Where there is no dispatcher queue (a design-time or
        // test host) the note is still handed over on blur and on detach; only save-as-you-type is lost.
        _saveTimer = DispatcherQueue.GetForCurrentThread()?.CreateTimer();

        if (_saveTimer is null)
        {
            Log.Warn(Scope, "no dispatcher queue on this thread; the note saves on blur and on close only");
        }
        else
        {
            _saveTimer.Interval = SaveDebounce;
            _saveTimer.IsRepeating = false;
            _saveTimer.Tick += OnSaveTick;
        }

        Lay();
    }

    /// <summary>[S-14770] The editor pads all four sides itself: the card gives its top no inset.</summary>
    public bool OwnsTopEdge => true;

    /// <summary>
    /// [S-129] A Windows 11 text field with no chrome at rest, so the note reads as writing on the
    /// card — and [S-3962] none under the pointer either: the field does not light up as the pointer
    /// crosses it, nor when it is clicked into: focus draws no fill, accent frame or line either (user
    /// requirement); the caret alone says where the keys go.
    /// <para>
    /// The brushes are replaced through the control's own resource keys, not through its
    /// <c>Background</c>: the stock template's visual states assign the pointer-over and focused
    /// backgrounds straight onto the border element, which outranks any local value, and the field
    /// would light up as a solid rectangle the moment the pointer crossed it. A
    /// <see cref="RichEditBox"/> shares these keys with the text box.
    /// </para>
    /// </summary>
    private void BuildEditor()
    {
        var box = _editor.Box;

        // [S-14303] The editor is the card's free field: the note's text stands from its top as written.
        FieldMark.Mark(box);
        var clear = Family.Brush(ColourRole.Clear);

        box.Resources["TextControlBackground"] = clear;
        box.Resources["TextControlBackgroundPointerOver"] = clear;
        box.Resources["TextControlBackgroundFocused"] = clear;
        box.Resources["TextControlBackgroundDisabled"] = clear;
        box.Resources["TextControlBackgroundReadOnly"] = clear;

        // No frame at rest, under the pointer or on focus: the border is no thickness in every state
        // (the focused state's own thickness, which draws the accent line past the box's, included),
        // so no brush of it is ever drawn and none needs replacing.
        box.Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(0);
        box.UseSystemFocusVisuals = false;

        box.BorderThickness = new Thickness(0);
        box.CornerRadius = Family.Radius;

        // [S-14770] [S-14303] The field is the card's whole box, edge to edge and top to foot (WR-3: "the inset is padding
        // inside the item, never a margin round the items"); the words stand in the inset on all four sides, so the
        // text starts at the card's top inset with no row over it (WR-1).
        box.Padding = new Thickness(FamilySpace.Inset);
        box.Margin = default;
        box.MinHeight = 0;
        box.HorizontalAlignment = HorizontalAlignment.Stretch;
        box.VerticalAlignment = VerticalAlignment.Stretch;
        box.VerticalContentAlignment = VerticalAlignment.Top;
        box.Foreground = Family.Brush(ColourRole.TextPrimary);

        ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(box, ScrollBarVisibility.Disabled);
    }

    /// <summary>
    /// [S-14771] The pill over the text's top end (owner, 2026-11-20: "the edit row should bascially be overlayed on the
    /// topright with the text"): the first line's box at the line's end, its buttons standing 2 over and under it as a tool
    /// line's do, its last glyph on the inset, in the card's own opaque base so the text under it is hidden and it reads as
    /// the card, the one radius. The parts are the tool line's (the problem glyph, the choice, a line button); it is shown
    /// by <see cref="NotesPill"/> and fades, taking no pointer while gone.
    /// </summary>
    private Border BuildPill()
    {
        var pill = new Border
        {
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _notSavedGlyph, _picker, _more },
            },
            Height = FamilySpace.HeaderLine,
            Padding = new Thickness(FamilySpace.S1, 0, 0, 0),
            CornerRadius = Family.Radius,
            Background = ThemeBrush.Of(ColourRoles.CardBase, default),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, NotesLayout.PillTop, NotesLayout.PillEnd, 0),
            Opacity = 0,
            IsHitTestVisible = false,
        };

        Canvas.SetZIndex(pill, 1);
        return pill;
    }

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        // Nothing to tick: a note changes when it is typed in, not when time passes.
        _pool = _host.Notes;

        if (_pool is not null)
        {
            _pool.Changed += OnPoolChanged;
            _pool.SaveStateChanged += OnSaveStateChanged;
        }

        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1110] Re-read every text this view set; the note itself is the user's and stays.</summary>
    private void OnLanguage(Locale locale)
    {
        _editor.RefreshTexts();
        ApplyPickerTexts();
        ToolTipService.SetToolTip(_more, NotesStr.WidgetsC_Notes_MoreOptions.Text());
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_more, NotesStr.WidgetsC_Notes_MoreOptions.Text());
        UpdateMessage();
        UpdateStatus();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as NotesConfig ?? new NotesConfig();

        // [S-6700] The note is Body (its title Title) at the text size every widget follows: no size of its own.
        _editor.FontSize = FamilyType.Body.FontSize;
        _editor.Wrap = true;

        Resolve();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    // -----------------------------------------------------------------------------------------
    // Which note
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1346] Find the note this widget shows and put it in the editor. With none chosen, a desktop
    /// widget gets a new note of its own and remembers it; a preview card does not, since nothing
    /// could ever reach that note again. A note that was deleted is not replaced ([S-1347]).
    /// </summary>
    private void Resolve()
    {
        if (_detached || _resolving)
        {
            return;
        }

        _resolving = true;

        try
        {
            var next = ResolveId();

            // Leaving a note: whatever was typed into it goes to it first, never to the next one.
            if (!string.Equals(next, _shownId, StringComparison.Ordinal))
            {
                Flush();
                _shownId = next;
                Show(newDocument: true);
            }
            else
            {
                Show(newDocument: false);
            }
        }
        finally
        {
            _resolving = false;
        }

        Lay();
    }

    private string? ResolveId()
    {
        if (_pool is null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(_config.NoteId) && _surface is { IsPreview: false } surface)
        {
            _config.NoteId = _pool.Create(NotesStr.Notes_NewName.Text());
            surface.RequestSave();
        }

        return _config.NoteId;
    }

    /// <summary>Put the shown note in the editor and decide the state. An unflushed local edit outranks the store.</summary>
    private void Show(bool newDocument)
    {
        var meta = _pool?.Find(_shownId);

        _state = _pool is null ? NoteState.Unavailable
            : _shownId is null ? NoteState.Unbound
            : meta is null ? NoteState.Deleted
            : NoteState.Ready;

        string? text = null;

        if (_state == NoteState.Ready)
        {
            var read = _pool!.Read(_shownId);
            if (read.Status == NoteReadStatus.Found)
            {
                text = read.Text;
            }
            else
            {
                _state = NoteState.Unreadable;
            }
        }

        if (_state != NoteState.Ready)
        {
            // [S-1342] Nothing to write into. A pending edit is dropped rather than resurrecting the note.
            _dirty = false;
            _saveTimer?.Stop();
        }

        _editor.IsReadOnly = !Editable;

        if (newDocument)
        {
            _editor.Reset(text ?? string.Empty);
        }
        else if (!_dirty && text is not null)
        {
            // [S-1348] A change made elsewhere — another widget on this note, "Clear" in settings —
            // is taken only when nothing typed here is waiting: ours has not landed yet, and last
            // write wins.
            _editor.Load(text);
        }

        UpdateMessage();
        UpdateStatus();
    }

    /// <summary>The note can be typed in: a real note, or a preview's draft that is never saved.</summary>
    private bool Editable => _state is NoteState.Ready or NoteState.Unbound;

    /// <summary>
    /// [S-1347] [S-6714] What the widget says instead of the note, when it cannot show one: the Status
    /// row at the top, the note's glyph for a note that is gone or a PC with no notes, the warning for
    /// a note whose file could not be read.
    /// </summary>
    private void UpdateMessage()
    {
        var sentence = _state switch
        {
            NoteState.Deleted => NotesStr.Notes_NoteGone.Text(),
            NoteState.Unreadable => NotesStr.WidgetsC_Notes_Unreadable.Text(),
            NoteState.Unavailable => NotesStr.WidgetsC_Notes_Unavailable.Text(),
            _ => null,
        };

        if (sentence is null)
        {
            _message.Hide();
            _editor.Box.Visibility = Visibility.Visible;
            return;
        }

        _message.Show(_state == NoteState.Unreadable ? StatusKind.Problem : StatusKind.Empty, Glyphs.Note, sentence);
        _editor.Box.Visibility = Visibility.Collapsed;
    }

    private void OnPoolChanged(string? id)
    {
        if (_detached || _resolving)
        {
            return;
        }

        if (id is null || string.Equals(id, _shownId, StringComparison.Ordinal))
        {
            Show(newDocument: false);
            Lay();
        }
    }

    // -----------------------------------------------------------------------------------------
    // Typing and saving
    // -----------------------------------------------------------------------------------------

    private void OnTextEdited(string text)
    {
        if (_detached || _state != NoteState.Ready)
        {
            // A preview's draft has nowhere to go.
            return;
        }

        _dirty = true;
        _saveTimer?.Start(); // restarts the countdown, so a run of keystrokes is one write
    }

    /// <summary>[S-14771] The first line may have become a title or stopped being one: the pill covers it whole.</summary>
    private void OnFirstLine(string text) => _pill.Height = NotesLayout.PillHeight(_editor.FirstLine);

    /// <summary>[S-14773] A fit of the foot is asked: one, after the layout the change asks for.</summary>
    private bool _fitPosted;

    private void FitLinesSoon()
    {
        if (_fitPosted || _detached)
        {
            return;
        }

        _fitPosted = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, FitLines);
    }

    /// <summary>
    /// [S-14773] No line cut mid-height at the foot: the line the inset's foot would cut is not drawn, the foot's padding
    /// taking the room down to where the last whole line ends (<see cref="NotesLayout.CutLine"/>); scrolled, the lines
    /// under the room are the text's, as any field's.
    /// </summary>
    private void FitLines()
    {
        _fitPosted = false;
        var box = _editor.Box;
        if (_detached || !(box.ActualHeight > 0))
        {
            return;
        }

        var inset = FamilySpace.Inset;
        var room = box.ActualHeight - inset - inset;
        var foot = inset + NotesLayout.CutLine(room, _editor.LineBottoms(room));
        if (Math.Abs(box.Padding.Bottom - foot) > 0.01)
        {
            box.Padding = new Thickness(inset, inset, inset, foot);
        }
    }

    /// <summary>
    /// Ask the surface for the keyboard. Outside edit mode it is unfocusable, so without this the
    /// caret blinks here while the letters go to whatever window actually has focus.
    /// </summary>
    private void OnEditorGotFocus(object sender, RoutedEventArgs args)
    {
        _editorFocused = true;
        UpdateKeyboard();
    }

    private void OnEditorLostFocus(object sender, RoutedEventArgs args)
    {
        _editorFocused = false;
        Flush();
        UpdateKeyboard();
    }

    /// <summary>Typing asks for the keyboard: the editor, and the pill's rename box.</summary>
    private void UpdateKeyboard() => _surface?.RequestTextInput(!_detached && (_editorFocused || _renaming is not null));

    private void OnSaveTick(DispatcherQueueTimer sender, object args) => Flush();

    /// <summary>
    /// Hand the editor's text to the pool. Idempotent: with nothing pending it does nothing, so
    /// calling it from four places costs nothing.
    /// </summary>
    private void Flush()
    {
        _saveTimer?.Stop();

        if (!_dirty)
        {
            return;
        }

        _dirty = false;

        if (_state == NoteState.Ready && _shownId is not null)
        {
            _pool?.Write(_shownId, _editor.Text);
        }

        UpdateStatus();
    }

    private void OnSaveStateChanged()
    {
        if (!_detached)
        {
            UpdateStatus();
        }
    }

    /// <summary>
    /// [S-1362] "Not saved" while the note's last write failed, as the pill's problem glyph (its tooltip says it; the pill
    /// stands while it does, [S-14771]), and nothing at all otherwise: no word or character count, no "Saved" (user
    /// requirement). The text keeps all its room.
    /// </summary>
    private void UpdateStatus()
    {
        var failed = _state == NoteState.Ready && _pool?.WriteFailed == true;
        _notSavedGlyph.Visibility = failed ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(_notSavedGlyph, NotesStr.WidgetsC_Notes_NotSaved.Text());
        UpdatePicker();
        Pill(_shows.WithProblem(failed));
    }

    // -----------------------------------------------------------------------------------------
    // The pill
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-14771] The pill's parts and its name's room for the card's width. Cheap: nothing is rebuilt here.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        UpdatePicker();
        _more.Visibility = NoteMenus.More(_state is NoteState.Ready or NoteState.Unbound).Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _pill.Height = NotesLayout.PillHeight(_editor.FirstLine);
        Pill(_shows);
    }

    /// <summary>[S-14771] The pointer moved over the card (or came onto it): the pill shows again, if a key had sent it away.</summary>
    private void OnPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (!_shows.Pointer || _shows.Typing)
        {
            Pill(_shows.Moved());
        }
    }

    /// <summary>
    /// [S-14771] The pointer left the card. Read by where it is, not by which part it left: the exits of the text and of the
    /// pill's buttons bubble here too while the pointer is still over the card.
    /// </summary>
    private void OnPointerExited(object sender, PointerRoutedEventArgs args)
    {
        var at = args.GetCurrentPoint(this).Position;
        if (at.X < 0 || at.Y < 0 || at.X >= ActualWidth || at.Y >= ActualHeight)
        {
            Pill(_shows.Left());
        }
    }

    /// <summary>[S-14771] A key in the text: the pill leaves the text to the keys until the pointer moves again.</summary>
    private void OnKeyed()
    {
        if (!_shows.Typing)
        {
            Pill(_shows.Typed());
        }
    }

    /// <summary>[S-14771] The pill as <paramref name="shows"/> says: faded in or out (opacity alone, WR-12), taking the pointer only while shown.</summary>
    private void Pill(NotesPill shows)
    {
        _shows = shows;
        if (_detached)
        {
            return;
        }

        var parts = _picker.Visibility == Visibility.Visible || _more.Visibility == Visibility.Visible || _notSavedGlyph.Visibility == Visibility.Visible;
        var shown = shows.Shown && parts;
        if (shown == _pill.IsHitTestVisible && _pill.Visibility == (parts ? Visibility.Visible : Visibility.Collapsed))
        {
            return;
        }

        var fades = SystemMotion.AnimationsEnabled;
        if ((_pill.OpacityTransition is not null) != fades)
        {
            _pill.OpacityTransition = fades ? new ScalarTransition { Duration = NotesLayout.Fade } : null;
        }

        _pill.Opacity = shown ? 1 : 0;
        _pill.IsHitTestVisible = shown;
        _pill.Visibility = parts ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>A menu of the pill opened or closed: it stands while one is open (its anchor stays).</summary>
    private void PillMenu(bool open) => Pill(_shows.WithMenu(open));

    /// <summary>[S-14771] "…": what acts on the note shown (<see cref="NoteMenus.More"/>), as a menu under it.</summary>
    private void OnMoreClick()
    {
        if (_detached || _state != NoteState.Ready || _shownId is not { } shown)
        {
            // A picture's "…" opens nothing: there is no note behind it.
            return;
        }

        OpenMore(shown);
    }

    private void OpenMore(string shown)
    {
        var entries = NoteMenus.More(noteShown: true).Select(item => item switch
        {
            NoteMenuItem.Rename => new ItemMenuEntry(Glyphs.Rename, NotesStr.WidgetsC_Notes_RenameEllipsis.Text(), () => RenameInFlyout(shown)),
            _ => new ItemMenuEntry(Glyphs.Delete, NotesStr.WidgetsC_Notes_DeleteNoteEllipsis.Text(), () => _ = DeleteAsync(shown), Danger: true),
        });

        PillMenu(true);
        if (!ItemMenu.Open(_more, entries, () => PillMenu(false)))
        {
            PillMenu(false);
        }
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        // Before anything is unhooked: this is the last chance the last sentence has.
        Flush();
        _detached = true;
        Strings.Changed -= OnLanguage;

        if (_saveTimer is not null)
        {
            _saveTimer.Stop();
            _saveTimer.Tick -= OnSaveTick;
        }

        if (_pool is not null)
        {
            _pool.Changed -= OnPoolChanged;
            _pool.SaveStateChanged -= OnSaveStateChanged;
        }

        _editor.TextEdited -= OnTextEdited;
        _editor.TextEdited -= OnFirstLine;
        _editor.Keyed -= OnKeyed;
        _editor.Box.GotFocus -= OnEditorGotFocus;
        _editor.Box.LostFocus -= OnEditorLostFocus;
        RemoveHandler(PointerMovedEvent, new PointerEventHandler(OnPointerMoved));
        RemoveHandler(PointerExitedEvent, new PointerEventHandler(OnPointerExited));
        PointerPreview.Forget(_editor.Box);
        _editor.Detach();
        _surface?.RequestTextInput(false);
    }
}
