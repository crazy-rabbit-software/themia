using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Notes;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Notes;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1346] A markdown note from the notes kept on this PC (<see cref="NotePool"/>).
/// <para>
/// The widget is a window onto a note, not the note: its config holds only which note
/// (<see cref="NotesConfig.NoteId"/>), so the note outlives the widget, two widgets can show one
/// note, and switching or deleting a widget never touches the text. A widget with no note yet gets a
/// new one of its own the first time it is shown. A widget whose note was deleted elsewhere says so
/// rather than quietly showing another ([S-1347]).
/// </para>
/// <para>
/// The editing itself is <see cref="MarkdownEditor"/>; this view binds it to the pool, saves on a
/// debounce and on blur and detach, and lays the parts out by the ladder in <see cref="NotesLayout"/>:
/// text, then the formatting bar, then a status line. It shows the one note at every size; another
/// note is picked from the header (<see cref="HeaderTrailing"/>), never from a list beside the text.
/// </para>
/// </summary>
internal sealed partial class NotesView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetSizeAware, IWidgetHeaderContent
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
    private readonly Grid _body = new();
    private readonly TextBlock _status;
    private readonly TextBlock _messageText;
    private readonly Border _message;
    private readonly DispatcherQueueTimer? _saveTimer;

    private IWidgetSurface? _surface;
    private NotePool? _pool;
    private NotesConfig _config = new();
    private string? _shownId;
    private NoteState _state = NoteState.Unbound;
    private WidgetArea _area;
    private NotesLayoutPlan _plan = new(0, false, false, 0, NotesLayout.MinTextLines);
    private bool _dirty;
    private bool _resolving;
    private bool _editorFocused;
    private bool _detached;

    public NotesView()
    {
        // The card no longer pads its content; a view that is not a list carries its own inset.
        Padding = WidgetLayout.Inset;

        _body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                              // bar
        _body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });         // text
        _body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                              // status

        BuildEditor();

        _status = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        _status.TextTrimming = TextTrimming.CharacterEllipsis;
        _status.TextWrapping = TextWrapping.NoWrap;
        _status.Margin = new Thickness(0, Fluent.Space2, 0, 0);
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_status, FontNumeralAlignment.Tabular);

        _messageText = Fluent.Text(FluentType.Body);
        _message = new Border
        {
            Child = Fluent.EmptyState(Glyphs.Note, _messageText, out _),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        _editor.Toolbar.Margin = new Thickness(0, 0, 0, Fluent.Space2);
        _editor.Toolbar.HorizontalAlignment = HorizontalAlignment.Left;

        SetRow(_editor.Toolbar, 0);
        SetRow(_editor.Box, 1);
        SetRow(_message, 1);
        SetRow(_status, 2);
        _body.Children.Add(_editor.Toolbar);
        _body.Children.Add(_editor.Box);
        _body.Children.Add(_message);
        _body.Children.Add(_status);

        Children.Add(_body);

        BuildPicker();

        _editor.TextEdited += OnTextEdited;
        _editor.Box.GotFocus += OnEditorGotFocus;
        _editor.Box.LostFocus += OnEditorLostFocus;

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

    /// <summary>[S-1364] The note picker, in the card's header.</summary>
    public FrameworkElement? HeaderTrailing => _picker;

    /// <summary>
    /// [S-129] A Windows 11 text field with no chrome at rest, so the note reads as writing on the
    /// card — and with the stock subtle fill under the pointer and the accent line when it has focus,
    /// so it still reads as something you can type in.
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
        var clear = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);
        var hover = FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Gray);
        var focus = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);

        box.Resources["TextControlBackground"] = clear;
        box.Resources["TextControlBackgroundPointerOver"] = hover;
        box.Resources["TextControlBackgroundFocused"] = hover;
        box.Resources["TextControlBackgroundDisabled"] = clear;
        box.Resources["TextControlBackgroundReadOnly"] = clear;

        box.Resources["TextControlBorderBrush"] = clear;
        box.Resources["TextControlBorderBrushPointerOver"] = clear;
        box.Resources["TextControlBorderBrushDisabled"] = clear;

        // The one piece of chrome that stays: the accent line under a focused field says where the
        // caret went.
        box.Resources["TextControlBorderBrushFocused"] = focus;

        box.BorderThickness = new Thickness(0);
        box.CornerRadius = new CornerRadius(Fluent.ControlRadius);

        // The fill is inset from the text by one grid unit and the field pulled back out by the same,
        // so the hover highlight has room to breathe while the words stay where the inset put them.
        box.Padding = new Thickness(Fluent.Space1);
        box.Margin = new Thickness(-Fluent.Space1);
        box.MinHeight = 0;
        box.HorizontalAlignment = HorizontalAlignment.Stretch;
        box.VerticalAlignment = VerticalAlignment.Stretch;
        box.VerticalContentAlignment = VerticalAlignment.Top;
        box.Foreground = FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White);

        ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(box, ScrollBarVisibility.Disabled);
    }

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        // Nothing to tick: a note changes when it is typed in, not when time passes.
        _pool = AppServices.Notes;

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
        UpdateMessage();
        UpdateStatus();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as NotesConfig ?? new NotesConfig();

        _editor.FontSize = _config.EffectiveFontSizePx;
        _editor.Wrap = _config.WrapText;

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
            _config.NoteId = _pool.Create(string.IsNullOrWhiteSpace(surface.Label) ? Str.Notes_NewName.Text() : surface.Label);
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

        // [S-1364] The note's title names the card; the user's own label still wins (the card's rule).
        _surface?.SetHeaderTitle(meta?.Title);
        UpdatePicker();
        UpdateStatus();
    }

    /// <summary>The note can be typed in: a real note, or a preview's draft that is never saved.</summary>
    private bool Editable => _state is NoteState.Ready or NoteState.Unbound;

    /// <summary>[S-1347] What the widget says instead of the note, when it cannot show one.</summary>
    private void UpdateMessage()
    {
        _messageText.Text = _state switch
        {
            NoteState.Deleted => Str.Notes_NoteGone.Text(),
            NoteState.Unreadable => Str.WidgetsC_Notes_Unreadable.Text(),
            NoteState.Unavailable => Str.WidgetsC_Notes_Unavailable.Text(),
            _ => string.Empty,
        };

        var showMessage = _state is NoteState.Deleted or NoteState.Unreadable or NoteState.Unavailable;
        _message.Visibility = showMessage ? Visibility.Visible : Visibility.Collapsed;
        _editor.Box.Visibility = showMessage ? Visibility.Collapsed : Visibility.Visible;
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
            // A preview's draft has nowhere to go; only its word count follows the typing.
            UpdateStatus();
            return;
        }

        _dirty = true;
        _saveTimer?.Start(); // restarts the countdown, so a run of keystrokes is one write
        UpdateStatus();
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

    /// <summary>Typing asks for the keyboard: the editor, and the picker's rename box.</summary>
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

    /// <summary>[S-1362] Words, and at wide sizes characters and whether it is on disk yet.</summary>
    private void UpdateStatus()
    {
        if (!_plan.Status || !Editable)
        {
            _status.Text = string.Empty;
            return;
        }

        var save = _dirty || _pool?.Saving == true ? NoteSaveState.Saving
            : _pool?.WriteFailed == true ? NoteSaveState.Failed
            : NoteSaveState.Saved;

        _status.Text = NoteText.Status(NoteText.Count(_editor.Text), _plan.StatusDetail, save);
    }

    // -----------------------------------------------------------------------------------------
    // Layout
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-1363] Apply the ladder. Cheap: nothing is rebuilt here.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        // A line of the note at its size: the type ramp's leading for body text, scaled.
        var lineHeight = _config.EffectiveFontSizePx * FluentType.Body.LineHeight / FluentType.Body.FontSize;
        _plan = NotesLayout.Plan(_area, WidgetLayout.ContentInset, Fluent.Space2, lineHeight, FluentType.Caption.LineHeight);

        var editable = Editable;
        _editor.FlatHeadings = _area.WidthClass == SizeClass.Compact;
        _editor.Toolbar.Visibility = _plan.Toolbar && editable ? Visibility.Visible : Visibility.Collapsed;
        _editor.VisibleButtons = _plan.ToolbarButtons;
        _status.Visibility = _plan.Status && editable ? Visibility.Visible : Visibility.Collapsed;

        UpdatePicker();
        UpdateStatus();
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
        _editor.Box.GotFocus -= OnEditorGotFocus;
        _editor.Box.LostFocus -= OnEditorLostFocus;
        _editor.Detach();
        DetachPicker();
        _surface?.RequestTextInput(false);
    }
}
