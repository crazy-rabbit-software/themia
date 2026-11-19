using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-13950] The one inline text box (owner, 2026-11-19: "layout shifts for these kind of inputs should never happen"):
/// the box a widget types into in place of one of its texts (the add row's line, a row's title, a habit's name, a
/// fence's title, a folder item's name). It <b>stands in exactly the box of the words it replaces</b>
/// (<see cref="StandIn"/>): the same panel, place and cell, margin and alignment, the words' type (size, family, weight,
/// tracking, alignment) at their place, as tall as their line (<see cref="InlineFit"/>), so opening it, typing in it,
/// committing or cancelling it moves and resizes nothing else.
/// <para>
/// What WinUI's text box would add is taken out, in every state: its 32 minimum and 64 minimum width, its padding and
/// border (the words stand where the text stood), its fill and frame under the pointer and on focus (the caret, the
/// selection and the placeholder say where the keys go, as the note's field, [S-3962]), the clear button (a 30-wide
/// button that also asks for its own height) and the header. It never wraps taller: long words scroll in it, as in any
/// one-line box.
/// </para>
/// </summary>
public sealed partial class InlineBox : TextBox
{
    /// <summary>Where the box stands in for its words.</summary>
    private readonly WordsPlace _place;

    /// <param name="words">The words the box stands in for: it takes their type, their line and, open, their place.</param>
    /// <param name="text">What the box holds when it opens.</param>
    public InlineBox(TextBlock words, string text)
    {
        ArgumentNullException.ThrowIfNull(words);

        _place = new WordsPlace(this, words);
        Text = text ?? string.Empty;
        AcceptsReturn = false;
        TextWrapping = TextWrapping.NoWrap;

        // The words' type, exactly: the box's text is theirs, at their place.
        FontSize = words.FontSize;
        FontFamily = words.FontFamily;
        FontWeight = words.FontWeight;
        FontStretch = words.FontStretch;
        CharacterSpacing = words.CharacterSpacing;
        TextAlignment = words.TextAlignment;
        Foreground = Family.Brush(ColourRole.TextPrimary);
        PlaceholderForeground = Family.Brush(ColourRole.TextSecondary);

        // What the platform's box adds around its text, gone: the minimums (32 tall, 64 wide), the padding, the frame;
        // its height is the words' line (WinUI's own line of Body is 19.3, inside it).
        var fit = InlineFit.Of(words.LineHeight > 0 ? words.LineHeight : FamilyType.LineOf(words.FontSize));
        MinHeight = 0;
        MinWidth = 0;
        if (fit.Height > 0)
        {
            Height = fit.Height;
        }

        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        Background = Family.Brush(ColourRole.Clear);

        // ... in every state: the template's states set their own fill, frame and the focused frame's thickness straight on
        // its border, outranking a local value, so they are replaced through the control's own resource keys.
        var clear = Family.Brush(ColourRole.Clear);
        foreach (var key in ClearKeys)
        {
            Resources[key] = clear;
        }

        Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(0);
        UseSystemFocusVisuals = false;
        ScrollViewer.SetVerticalScrollBarVisibility(this, ScrollBarVisibility.Hidden);
        ScrollViewer.SetHorizontalScrollBarVisibility(this, ScrollBarVisibility.Hidden);

        // Its caret scrolls the words inside it, never the list round it: the box stands where its words stood, which the
        // row they are in has brought into sight (its ring, [S-1288]); the platform's request on focus and on every key
        // scrolled a folder's tiles by 2.
        BringIntoViewRequested += OnBringIntoViewRequested;
    }

    private static void OnBringIntoViewRequested(UIElement sender, BringIntoViewRequestedEventArgs args) => args.Handled = true;

    /// <summary>
    /// The template's fills that would light the box up: at rest, under the pointer, focused, disabled, read only. Its frame
    /// is no thickness in every state (the focused state's own thickness included), so no brush of it is ever drawn.
    /// </summary>
    private static readonly string[] ClearKeys =
    [
        "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBackgroundDisabled",
        "TextControlBackgroundReadOnly",
    ];

    /// <summary>Whether the box stands in for its words now.</summary>
    public bool Standing => _place.Standing;

    /// <summary>The keys were asked for before the box could take them: it takes them once it loads.</summary>
    private bool _keysWanted;

    /// <summary>
    /// Stand in for the words: the box takes their panel, their place in it, their grid cell and span, margin and
    /// alignment; the words are collapsed (<see cref="WordsPlace"/>).
    /// </summary>
    public void StandIn() => _place.In();

    /// <summary>Step out: the box leaves its panel and the words come back where they stood.</summary>
    public void StandOut()
    {
        _keysWanted = false;
        Loaded -= OnLoadedTakeKeys;
        _place.Out();
    }

    /// <summary>
    /// The keys in the box, the caret at the end of its words: now, or as soon as it stands in its words' place and
    /// loads there (a box asked for before its row was in the live tree).
    /// </summary>
    public void TakeKeys()
    {
        if (_place.Standing && Focus(FocusState.Programmatic))
        {
            SelectionStart = Text.Length;
            return;
        }

        if (!_keysWanted)
        {
            _keysWanted = true;
            Loaded += OnLoadedTakeKeys;
        }
    }

    private void OnLoadedTakeKeys(object sender, RoutedEventArgs args)
    {
        Loaded -= OnLoadedTakeKeys;
        if (_keysWanted && _place.Standing)
        {
            _keysWanted = false;
            Focus(FocusState.Programmatic);
            SelectionStart = Text.Length;
        }
    }

    /// <summary>The clear button out of the template: it is 30 wide and as tall as its glyph's room, more than a line of Caption.</summary>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("DeleteButton") is ButtonBase clear)
        {
            clear.MaxWidth = 0;
            clear.MaxHeight = 0;
            clear.IsHitTestVisible = false;
        }
    }
}
