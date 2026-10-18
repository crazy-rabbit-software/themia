using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
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
    private Size _sheetShape;
    private (Panel Parent, int Index)? _toolsHome;

    private bool IsFullScreen => _full is not null;

    private string FullScreenTip()
        => IsFullScreen ? ScratchpadStr.WidgetsC_Scratchpad_ExitFullScreen.Text() : ScratchpadStr.WidgetsC_Scratchpad_FullScreen.Text();

    private void OnFullScreenClicked(object sender, RoutedEventArgs args)
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

        if (_tools.Parent is Panel home)
        {
            _toolsHome = (home, home.Children.IndexOf(_tools));
            home.Children.Remove(_tools);
        }

        _barRow.Children.Remove(_bar);
        Children.Remove(_pad);

        _fullTools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space3,
            Children = { _bar, Divider(), _tools },
        };

        var toolbar = new Border
        {
            Child = _fullTools,
            Padding = new Thickness(Fluent.Space2, Fluent.Space1, Fluent.Space2, Fluent.Space1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
        };
        ChromeFrame.Apply(toolbar);

        _sheet = new Border
        {
            Child = _pad,
            CornerRadius = new CornerRadius(Fluent.OverlayRadius),
            BorderThickness = new Thickness(Fluent.Hairline),
            BorderBrush = ThemeBrush.Of("CardStrokeColorDefaultBrush", Colors.Gray),
            Background = ThemeBrush.Of("CardBackgroundFillColorDefaultBrush", Colors.Transparent),
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
            Padding = new Thickness(Fluent.Space6),
            RowSpacing = Fluent.Space4,
            Background = ThemeBrush.Of("SolidBackgroundFillColorBaseBrush", Colors.Black),
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

        if (_sheet is not null)
        {
            _sheet.Child = null;
            _sheet = null;
        }

        _fullLayer = null;

        // Back where each came from: the pad under the hint, the bar in its row, the tools in the header.
        Children.Insert(0, _pad);
        _pad.Width = double.NaN;
        _pad.Height = double.NaN;
        _barRow.Children.Add(_bar);

        if (_toolsHome is { } home)
        {
            home.Parent.Children.Insert(Math.Clamp(home.Index, 0, home.Parent.Children.Count), _tools);
            _toolsHome = null;
        }

        ApplyFullScreenButton();
        _tools.Visibility = _config.ShowTools ? Visibility.Visible : Visibility.Collapsed;
        ShedTools(_area.Width);
        LayBar();
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
        _fullButton.Content = Fluent.Icon(IsFullScreen ? Glyphs.BackToWindow : Glyphs.FullScreen);
        ToolTipService.SetToolTip(_fullButton, FullScreenTip());
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_fullButton, FullScreenTip());

        // Every tool shows on the toolbar; the eraser is the bar's there.
        _fullButton.Visibility = Visibility.Visible;
        _clearButton.Visibility = Visibility.Visible;
        if (IsFullScreen)
        {
            _tools.Visibility = Visibility.Visible;
            LayBar();
        }
    }

    private static Border Divider() => new()
    {
        Width = Fluent.Hairline,
        Margin = new Thickness(0, Fluent.Space1, 0, Fluent.Space1),
        Background = ThemeBrush.Of("DividerStrokeColorDefaultBrush", Colors.Gray),
    };
}
