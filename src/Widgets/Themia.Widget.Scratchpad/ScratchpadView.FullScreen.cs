using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;
using Themia.Widget.Scratchpad;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1499] Full screen: the same pad, the same drawing, over the whole display, for when the tile
/// is too small to draw in. The pad itself moves into a popup that covers the display and back
/// again, so the strokes, the undo history and a gesture's handlers are the one pad's; nothing is
/// copied. It keeps the tile's shape (the drawing is in pad units, so another shape would stretch
/// it), as large as the display holds, under a toolbar of the bar's inks, nibs and eraser and the
/// header's undo, clear and the way back.
/// <para>
/// A windowed popup rather than one inside the surface: outside edit mode the surface's region is
/// the cards' own rects ([S-851]) and would cut the pad off at the card's edge.
/// </para>
/// </summary>
internal sealed partial class ScratchpadView
{
    private Popup? _full;
    private Grid? _fullLayer;
    private Border? _sheet;
    private StackPanel? _fullTools;
    private Border? _fullUndo;
    private Border? _fullClear;
    private Size _sheetShape;

    private bool IsFullScreen => _full is not null;

    private string FullScreenTip()
        => IsFullScreen ? ScratchpadStr.WidgetsC_Scratchpad_ExitFullScreen.Text() : ScratchpadStr.WidgetsC_Scratchpad_FullScreen.Text();

    private void ToggleFullScreen()
    {
        if (IsFullScreen)
        {
            ExitFullScreen();
        }
        else
        {
            EnterFullScreen();
        }
    }

    private void EnterFullScreen()
    {
        if (_detached || IsFullScreen || XamlRoot is not { } root)
        {
            return;
        }

        // A gesture in flight was drawn on the tile; it ends there.
        CommitStroke();
        ReleaseCapture();

        _sheetShape = _pad.ActualWidth > 0 && _pad.ActualHeight > 0 ? new Size(_pad.ActualWidth, _pad.ActualHeight) : new Size(4, 3);

        _barRow.Children.Remove(_bar);
        Children.Remove(_pad);

        // The bar's inks, nibs and eraser, then the header's undo, clear and the way back, always all
        // shown whatever Show tools says: they hold the way back.
        _fullUndo = GlyphTool(Glyphs.Reset, ScratchpadStr.WidgetsC_Scratchpad_Undo.Text(), Undo);
        _fullClear = GlyphTool(Glyphs.Delete, ScratchpadStr.WidgetsC_Scratchpad_ClearDrawing.Text(), () => _ = AskToClearAsync(_fullClear!));
        var exit = GlyphTool(Glyphs.BackToWindow, ScratchpadStr.WidgetsC_Scratchpad_ExitFullScreen.Text(), ExitFullScreen);

        _fullTools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = FamilySpace.S3,
            Children =
            {
                _bar,
                Divider(),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, Children = { _fullUndo, _fullClear, exit } },
            },
        };

        var toolbar = new Border
        {
            Child = _fullTools,
            Padding = new Thickness(FamilySpace.S2, FamilySpace.S1, FamilySpace.S2, FamilySpace.S1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
        };
        ChromeFrame.Apply(toolbar);

        // The pad at the tile's shape: a hairline in the track's role round it, the family's radius
        // (the card's own brushes are the card's, not a widget's to name).
        _sheet = new Border
        {
            Child = _pad,
            CornerRadius = Family.Radius,
            BorderThickness = new Thickness(FamilyShape.Hairline),
            BorderBrush = Family.Brush(ColourRole.Track),
            Background = Family.Brush(ColourRole.Clear),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var room = new Grid { Children = { _sheet } };
        Grid.SetRow(room, 1);
        room.SizeChanged += (_, e) => FitSheet(e.NewSize);

        _fullLayer = new Grid
        {
            Width = root.Size.Width,
            Height = root.Size.Height,
            Padding = new Thickness(FamilySpace.S6),
            RowSpacing = FamilySpace.S4,

            // An opaque layer over the whole display: the solid role.
            Background = Family.Brush(ColourRole.Solid),
            RequestedTheme = ActualTheme,
            FlowDirection = FlowDirection,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
            Children = { toolbar, room },
        };

        _full = new Popup
        {
            Child = _fullLayer,
            XamlRoot = root,
            ShouldConstrainToRootBounds = false,
            IsLightDismissEnabled = false,
        };

        root.Changed += OnRootChanged;
        _full.IsOpen = true;

        ApplyFullScreenButton();
        UpdateCommands();
    }

    private void ExitFullScreen()
    {
        if (_full is not { } full)
        {
            return;
        }

        CommitStroke();
        ReleaseCapture();

        if (full.XamlRoot is { } root)
        {
            root.Changed -= OnRootChanged;
        }

        full.IsOpen = false;
        full.Child = null;
        _full = null;

        _fullTools?.Children.Clear();
        _fullTools = null;
        _fullUndo = null;
        _fullClear = null;

        if (_sheet is not null)
        {
            _sheet.Child = null;
            _sheet = null;
        }

        _fullLayer = null;

        // Back where each came from: the pad under the hint, the bar in its row.
        Children.Insert(0, _pad);
        _pad.Width = double.NaN;
        _pad.Height = double.NaN;
        _barRow.Children.Add(_bar);

        ApplyFullScreenButton();
        LayBar();
    }

    /// <summary>Full screen's undo and clear, greyed when there is nothing to undo or clear.</summary>
    private void UpdateFullScreenTools()
    {
        if (_fullUndo is not null)
        {
            Enable(_fullUndo, _undo.Count > 0);
        }

        if (_fullClear is not null)
        {
            Enable(_fullClear, _config.Strokes.Count > 0);
        }
    }

    /// <summary>The pad at the tile's shape, as large as the room under the toolbar holds.</summary>
    private void FitSheet(Size room)
    {
        if (_sheet is null || !(room.Width > 0) || !(room.Height > 0))
        {
            return;
        }

        var scale = Math.Min(room.Width / _sheetShape.Width, room.Height / _sheetShape.Height);
        _sheet.Width = Math.Floor(_sheetShape.Width * scale);
        _sheet.Height = Math.Floor(_sheetShape.Height * scale);
    }

    /// <summary>The display changed size or scale: the layer covers it again.</summary>
    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (_fullLayer is not null)
        {
            _fullLayer.Width = sender.Size.Width;
            _fullLayer.Height = sender.Size.Height;
        }
    }

    private void ApplyFullScreenButton()
    {
        if (_fullButton.Child is FontIcon glyph)
        {
            glyph.Glyph = IsFullScreen ? Glyphs.BackToWindow : Glyphs.FullScreen;
        }

        Tip(_fullButton, FullScreenTip());
        if (IsFullScreen)
        {
            LayBar();
        }
    }

    private static Border Divider() => new()
    {
        Width = FamilyShape.Hairline,
        Margin = new Thickness(0, FamilySpace.S1, 0, FamilySpace.S1),
        Background = Family.Brush(ColourRole.Track),
    };
}
