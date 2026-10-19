using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Color = Windows.UI.Color;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6627] The one look of a selectable item, the Windows 11 desktop's icon selection, drawn from
/// <see cref="SelectionLook"/>'s rule over the highlight colour Windows reports
/// (<see cref="UIElementType.Highlight"/>, <c>COLOR_HIGHLIGHT</c>; the high-contrast highlight in a
/// contrast theme). An item's content goes inside a chrome border (<see cref="Wrap"/>), which sits
/// inside the item and over its item fill ([S-6513]): the user's item background stays the
/// item's rest, and the selection is laid over it, behind the text, as the desktop draws it.
/// <para>
/// The brushes are this class's own, one per tone and opacity, recoloured in place when Windows'
/// colours change (the next paint after <see cref="UISettings.ColorValuesChanged"/>). UI thread only.
/// </para>
/// </summary>
public static class SelectionChrome
{
    private const string Scope = "selection";

    private static readonly Dictionary<(SelectionTone Tone, double Alpha), SolidColorBrush> Brushes = [];

    private static UISettings? _ui;
    private static RgbColor? _highlight;
    private static volatile bool _stale = true;

    /// <summary>The chrome an item's <paramref name="content"/> is drawn in: rounded as Windows 11's, a 1-DIP border, clear at rest.</summary>
    public static Border Wrap(UIElement content)
    {
        var chrome = new Border
        {
            Child = content,
            BorderThickness = new Thickness(Fluent.Hairline),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
        };

        Paint(chrome, new SelectionCue(Selected: false, Hovered: false));
        return chrome;
    }

    /// <summary>Draw <paramref name="chrome"/> (a border from <see cref="Wrap"/>, or any border) for <paramref name="cue"/>.</summary>
    public static void Paint(Border chrome, SelectionCue cue)
    {
        ArgumentNullException.ThrowIfNull(chrome);
        var ink = SelectionLook.Ink(cue);

        // Clear, not null: the empty part of an item still takes its presses.
        chrome.Background = ink.Fill > 0 ? Brush(ink.Tone, ink.Fill) : Fluent.Clear();
        chrome.BorderBrush = ink.Stroke > 0 ? Brush(ink.Tone, ink.Stroke) : Fluent.Clear();
    }

    /// <summary>
    /// Hear the pointer enter and leave <paramref name="item"/> (true while over it), for a view that
    /// paints the hover through <see cref="Paint"/>. Dispose to unhook.
    /// </summary>
    public static IDisposable Hover(UIElement item, Action<bool> hovered)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(hovered);
        return new HoverHook(item, hovered);
    }

    private sealed class HoverHook : IDisposable
    {
        private readonly UIElement _item;
        private readonly Action<bool> _hovered;
        private bool _disposed;

        public HoverHook(UIElement item, Action<bool> hovered)
        {
            _item = item;
            _hovered = hovered;
            _item.PointerEntered += OnEntered;
            _item.PointerExited += OnGone;
            _item.PointerCaptureLost += OnGone;
            _item.PointerCanceled += OnGone;
        }

        private void OnEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args) => _hovered(true);

        private void OnGone(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args) => _hovered(false);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _item.PointerEntered -= OnEntered;
            _item.PointerExited -= OnGone;
            _item.PointerCaptureLost -= OnGone;
            _item.PointerCanceled -= OnGone;
        }
    }

    /// <summary>[S-6624] Draw <paramref name="band"/> as the desktop's rubber band: the highlight's fill under a solid border of it.</summary>
    public static void PaintBand(Border band)
    {
        ArgumentNullException.ThrowIfNull(band);
        var ink = SelectionLook.Band;
        band.BorderThickness = new Thickness(Fluent.Hairline);
        band.Background = Brush(ink.Tone, ink.Fill);
        band.BorderBrush = Brush(ink.Tone, ink.Stroke);
    }

    /// <summary>The brush of <paramref name="tone"/> at <paramref name="alpha"/>, in the current highlight.</summary>
    private static SolidColorBrush Brush(SelectionTone tone, double alpha)
    {
        Refresh();

        if (!Brushes.TryGetValue((tone, alpha), out var brush))
        {
            brush = new SolidColorBrush(ColourOf(tone, alpha));
            Brushes[(tone, alpha)] = brush;
        }

        return brush;
    }

    private static Color ColourOf(SelectionTone tone, double alpha)
    {
        var c = SelectionLook.Colour(tone, _highlight ?? SelectionLook.DefaultHighlight);
        return Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), c.R, c.G, c.B);
    }

    /// <summary>Read Windows' highlight again when it may have changed, and recolour every brush handed out.</summary>
    private static void Refresh()
    {
        if (!_stale)
        {
            return;
        }

        _stale = false;

        try
        {
            if (_ui is null)
            {
                _ui = new UISettings();

                // Raised on a worker thread: only mark, the next paint (on the UI thread) reads.
                _ui.ColorValuesChanged += (_, _) => _stale = true;
            }

            var h = _ui.UIElementColor(UIElementType.Highlight);
            _highlight = new RgbColor(h.R, h.G, h.B);
        }
        catch (Exception ex)
        {
            // Windows' default highlight still draws a selection that reads as one.
            Log.Warn(Scope, "could not read the system highlight colour; using Windows' default", ex);
            _highlight = SelectionLook.DefaultHighlight;
        }

        foreach (var ((tone, alpha), brush) in Brushes)
        {
            brush.Color = ColourOf(tone, alpha);
        }
    }
}

/// <summary>
/// [S-6629] The full name of a selected tile, laid over what is below it as the desktop shows the
/// label of its focused, selected icon: in an overlay layer above the items, so no tile moves. It
/// stands on an opaque base, the default card's colour, so it reads over the tile below, with the
/// selection's mark on top, and
/// takes no presses (a click there reaches the tile under it). Shown only when the tile's name is
/// cut short (<see cref="SelectionLook.ShowsFullName"/>); placed after every layout pass while shown.
/// </summary>
public sealed class SelectionLabel
{
    private readonly Canvas _layer;
    private readonly Border _base;
    private readonly Border _chrome;
    private readonly TextBlock _text;

    private FrameworkElement? _tile;
    private TextBlock? _name;
    private bool _listening;

    /// <param name="layer">A canvas over the items, the same size and origin as their panel's content.</param>
    public SelectionLabel(Canvas layer)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _text = new TextBlock { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        _chrome = new Border
        {
            Child = _text,
            BorderThickness = new Thickness(Fluent.Hairline, 0, Fluent.Hairline, Fluent.Hairline),
            CornerRadius = new CornerRadius(0, 0, Fluent.ControlRadius, Fluent.ControlRadius),
        };
        // The default card's own colour (its fill over the solid base, [S-835]), so the label reads as
        // part of the tile and not as a patch.
        var card = new Border
        {
            Child = _chrome,
            Background = ThemeBrush.Of("CardBackgroundFillColorDefaultBrush", Colors.Transparent),
            CornerRadius = new CornerRadius(0, 0, Fluent.ControlRadius, Fluent.ControlRadius),
        };
        _base = new Border
        {
            Child = card,
            Background = ThemeBrush.Of("SolidBackgroundFillColorBaseBrush", Colors.Black),
            CornerRadius = new CornerRadius(0, 0, Fluent.ControlRadius, Fluent.ControlRadius),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        // Under the band, which is drawn last.
        _layer.Children.Insert(0, _base);
    }

    /// <summary>
    /// The item now drawn as <paramref name="cue"/>: its tile (the chrome from
    /// <see cref="SelectionChrome.Wrap"/>) and its name. Shows the full name when the rule says so,
    /// hides it otherwise.
    /// </summary>
    public void Update(FrameworkElement tile, TextBlock name, SelectionCue cue, bool renaming)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(name);

        // Trimmed or not is known after layout; the placing pass checks it again.
        if (!SelectionLook.ShowsFullName(cue.Selected, cue.Focused, cue.Active, trimmed: true, renaming)
            || name.Visibility != Visibility.Visible)
        {
            Hide();
            return;
        }

        _tile = tile;
        _name = name;
        _text.Text = name.Text;
        _text.FontSize = name.FontSize;
        _text.FontFamily = name.FontFamily;
        _text.FontWeight = name.FontWeight;
        _text.LineHeight = name.LineHeight;
        _text.Foreground = name.Foreground;
        SelectionChrome.Paint(_chrome, cue);

        if (!_listening)
        {
            _listening = true;
            _layer.LayoutUpdated += OnLayoutUpdated;
        }

        Place();
    }

    /// <summary>No full name shown.</summary>
    public void Hide()
    {
        _tile = null;
        _name = null;
        _base.Visibility = Visibility.Collapsed;

        if (_listening)
        {
            _listening = false;
            _layer.LayoutUpdated -= OnLayoutUpdated;
        }
    }

    private void OnLayoutUpdated(object? sender, object args)
    {
        try
        {
            Place();
        }
        catch (Exception ex)
        {
            // A layout callback: a label that cannot be placed is hidden, never thrown into layout.
            Log.Error("selection", "the full name could not be placed", ex);
            Hide();
        }
    }

    private void Place()
    {
        if (_tile is not { } tile || _name is not { } name)
        {
            return;
        }

        if (tile.ActualWidth <= 0 || name.ActualWidth <= 0 || tile.XamlRoot is null || _layer.XamlRoot is null)
        {
            _base.Visibility = Visibility.Collapsed;
            return;
        }

        // Not cut short: nothing to show. (The name under the overlay stays as it is, so this holds while shown.)
        if (!name.IsTextTrimmed)
        {
            _base.Visibility = Visibility.Collapsed;
            return;
        }

        var box = tile.TransformToVisual(_layer).TransformBounds(new Rect(0, 0, tile.ActualWidth, tile.ActualHeight));
        var at = name.TransformToVisual(_layer).TransformPoint(new Point(0, 0));
        var inset = Math.Max(0, at.X - box.X);

        _text.Width = name.ActualWidth;
        _text.Margin = new Thickness(inset, 0, Math.Max(0, box.Width - inset - name.ActualWidth), Fluent.Space1);

        var left = box.X;
        var top = at.Y;
        var width = box.Width;

        if (Canvas.GetLeft(_base) != left)
        {
            Canvas.SetLeft(_base, left);
        }

        if (Canvas.GetTop(_base) != top)
        {
            Canvas.SetTop(_base, top);
        }

        if (_base.Width != width)
        {
            _base.Width = width;
        }

        _base.Visibility = Visibility.Visible;
    }
}

/// <summary>
/// [S-6623] The modifier the selection keys read as they are pressed: the lease hands Shift+Up as Up
/// (Shift only changes the meaning of Left, Right, Tab and F10 there), so a widget that extends a
/// selection with Shift+Up asks whether Shift is down now.
/// </summary>
public static class SelectionInput
{
    /// <summary>Whether Shift is held now, on the UI thread's keyboard state.</summary>
    public static bool ShiftHeld()
        => Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
}
