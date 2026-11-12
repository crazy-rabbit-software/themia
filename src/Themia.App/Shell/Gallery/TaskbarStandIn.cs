#if DEBUG
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Scenes;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.Samples;
using Themia.Sdk.WinUI;
using Windows.Foundation;
using Windows.UI;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-2200] Debug renders only: a stand-in for Windows 11's taskbar along the bottom of a store
/// scene's display, so a picture shows where Themia lives. A render fixture standing for Windows' own
/// chrome, as the stand-in wallpaper stands for Windows' picture ([S-2119]): it is not Themia UI,
/// nothing outside a Debug render reaches it, and its colours and sizes are Windows' rather than the
/// family's tokens, because it draws what Windows draws.
/// <para>
/// [S-2203] Laid out at Windows' 100 % (48 DIPs high) and magnified by the scene's display scale
/// ([S-1080]), so all of it scales together. Over the frosted wallpaper ([S-2119]) a translucent
/// neutral and a hairline along the top; centred the Start logo, the Search pill, Task view and the
/// sample PC's pinned apps (<see cref="SamplePc.TaskbarApps"/>) with Windows' running pills; on the
/// left the weather the sample day's widgets show; on the right the tray, the clock at the sample
/// moment and the notification bell.
/// </para>
/// </summary>
internal static class TaskbarStandIn
{
    // [S-2203] Windows 11's taskbar at 100 %, in DIPs.
    private const double Bar = SceneTaskbar.Height;
    private const double Hit = 40;
    private const double AppIcon = 24;
    private const double Between = 4;
    private const double SearchWidth = 200;
    private const double SearchHeight = 32;
    private const double Edge = 12;
    private const double TrayGlyph = 16;

    /// <summary>The most pinned apps drawn.</summary>
    private const int MostApps = 7;

    private const string Scope = "scene-render";

    /// <summary>
    /// The taskbar of <paramref name="scene"/> on a display of <paramref name="screenWidth"/> ×
    /// <paramref name="screenHeight"/> DIPs drawn at <paramref name="pixelsPerDip"/>, over
    /// <paramref name="backdrop"/> (the display's blurred wallpaper, or null for the fill alone).
    /// Its app icons are loaded before it returns. UI thread.
    /// </summary>
    public static async Task<FrameworkElement> BuildAsync(Scene scene, double screenWidth, double screenHeight, double pixelsPerDip, ImageSource? backdrop)
    {
        var scale = scene.Scale;
        var height = scene.TaskbarHeight;
        var ink = new Ink(scene.Theme == AppTheme.Dark);

        var root = new Grid
        {
            Width = screenWidth,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
            RequestedTheme = ink.Dark ? ElementTheme.Dark : ElementTheme.Light,
            Clip = new RectangleGeometry { Rect = new Rect(0, 0, screenWidth, height) },
            UseLayoutRounding = false,
        };

        // The wallpaper under the bar, blurred as under the cards: the display's backdrop moved up.
        if (backdrop is not null)
        {
            var picture = new Image { Source = backdrop, Width = screenWidth, Height = screenHeight, Stretch = Stretch.Fill };
            Canvas.SetTop(picture, height - screenHeight);
            root.Children.Add(new Canvas { Children = { picture } });
        }

        root.Children.Add(new Border { Background = Brush(ink.Fill) });
        root.Children.Add(new Border { Height = 1 / pixelsPerDip, VerticalAlignment = VerticalAlignment.Top, Background = Brush(ink.Hairline) });

        var content = new Grid
        {
            Width = screenWidth / scale,
            Height = Bar,
            RenderTransform = new ScaleTransform { ScaleX = scale, ScaleY = scale },
        };
        content.Children.Add(Weather(ink));
        content.Children.Add(await CentreAsync(ink, (int)Math.Round(AppIcon * scale * pixelsPerDip)).ConfigureAwait(true));
        content.Children.Add(Tray(ink));

        // In a canvas, so a bar laid out wider than the display at a scale below 1 is not clipped by its slot.
        root.Children.Add(new Canvas { Children = { content } });
        return root;
    }

    /// <summary>[S-2203] Start, Search, Task view and the pinned apps, centred as Windows 11 places them by default.</summary>
    private static async Task<FrameworkElement> CentreAsync(Ink ink, int iconPixels)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Between,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        row.Children.Add(Slot(StartLogo(), active: false, running: false, ink));
        row.Children.Add(SearchPill(ink));
        row.Children.Add(Slot(Glyph(Glyphs.View, 20, ink.Text), active: false, running: false, ink));

        // [S-2204] The pinned apps by the process list's loader; one missing on this PC is left out.
        var theme = ink.Dark ? LogoTheme.Dark : LogoTheme.Light;
        var shown = 0;
        foreach (var app in SamplePc.TaskbarApps)
        {
            if (shown == MostApps)
            {
                break;
            }

            if (!app.StartsWith(LaunchTargets.ShellPrefix, StringComparison.OrdinalIgnoreCase) && !File.Exists(app))
            {
                continue;
            }

            if (await AppIconSource.GetAsync(app, iconPixels, theme).ConfigureAwait(true) is not { } icon)
            {
                Log.Info(Scope, $"the stand-in taskbar has no icon for {app}; left out");
                continue;
            }

            // The first app is the one in front (the wide accent pill), two more run (the small pills).
            var image = new Image { Source = icon, Width = AppIcon, Height = AppIcon, Stretch = Stretch.Uniform };
            row.Children.Add(Slot(image, active: shown == 0, running: shown is 1 or 3, ink));
            shown++;
        }

        return row;
    }

    /// <summary>One 40 × 40 button: its picture centred, a highlight and the accent pill when in front, a small pill when running.</summary>
    private static FrameworkElement Slot(FrameworkElement picture, bool active, bool running, Ink ink)
    {
        picture.HorizontalAlignment = HorizontalAlignment.Center;
        picture.VerticalAlignment = VerticalAlignment.Center;
        var slot = new Grid
        {
            Width = Hit,
            Height = Hit,
            CornerRadius = new CornerRadius(4),
            Background = active ? Brush(ink.Highlight) : null,
            BorderBrush = active ? Brush(ink.HighlightEdge) : null,
            BorderThickness = new Thickness(active ? 1 : 0),
            Children = { picture },
        };

        if (active || running)
        {
            slot.Children.Add(new Border
            {
                Width = active ? 16 : 6,
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = Brush(active ? ink.Accent : ink.Running),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 1),
            });
        }

        return slot;
    }

    /// <summary>The Start button's logo: four squares in Windows' blue, lighter at the top as Windows 11 draws it.</summary>
    private static FrameworkElement StartLogo()
    {
        const double square = 10;
        const double gap = 1;
        var logo = new Canvas { Width = (2 * square) + gap, Height = (2 * square) + gap };
        for (var i = 0; i < 4; i++)
        {
            var (column, row) = (i % 2, i / 2);
            var tile = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = square,
                Height = square,
                RadiusX = 0.75,
                RadiusY = 0.75,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0.3, 1),
                    GradientStops =
                    {
                        new GradientStop { Color = row == 0 ? Rgb(0x4C, 0xC2, 0xFF) : Rgb(0x2B, 0x9B, 0xF4), Offset = 0 },
                        new GradientStop { Color = row == 0 ? Rgb(0x2B, 0x9B, 0xF4) : Rgb(0x0A, 0x6C, 0xD8), Offset = 1 },
                    },
                },
            };
            Canvas.SetLeft(tile, column * (square + gap));
            Canvas.SetTop(tile, row * (square + gap));
            logo.Children.Add(tile);
        }

        return logo;
    }

    /// <summary>The Search pill: the magnifier and, in the language drawn, the word.</summary>
    private static FrameworkElement SearchPill(Ink ink)
    {
        var inside = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = FamilySpace.S2,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(FamilySpace.S3, 0, FamilySpace.S3, 0),
            Children =
            {
                Glyph(Glyphs.Search, TrayGlyph, ink.Text),
                Text(Str.Menu_Filter.Text(), FamilyType.BodySize, ink.Secondary),
            },
        };

        return new Border
        {
            Width = SearchWidth,
            Height = SearchHeight,
            CornerRadius = new CornerRadius(SearchHeight / 2),
            Background = Brush(ink.Pill),
            BorderBrush = Brush(ink.PillEdge),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = inside,
        };
    }

    /// <summary>[S-2206] Windows' weather button: the sample day's condition and temperature, as the weather widgets show them.</summary>
    private static FrameworkElement Weather(Ink ink)
    {
        var reading = SampleWeather.Reading();
        var lines = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Text(WeatherReport.Temperature(reading.Temperature, RegionFormat.Current.Temperature), FamilyType.CaptionSize, ink.Text, FontWeights.SemiBold),
                Text(WeatherReport.Describe(reading.Code), FamilyType.CaptionSize, ink.Secondary),
            },
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = FamilySpace.S2,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Edge, 0, 0, 0),
            Children = { Glyph(WeatherGlyph.Of(WeatherReport.Look(reading.Code)), AppIcon, ink.Weather), lines },
        };
    }

    /// <summary>[S-2205] The tray: the chevron, Wi-Fi, volume and battery, the clock at the sample moment, the bell.</summary>
    private static FrameworkElement Tray(Ink ink)
    {
        var status = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(FamilySpace.S2, 0, FamilySpace.S2, 0),
            Children =
            {
                Glyph(Glyphs.Wifi, TrayGlyph, ink.Text),
                Glyph(Glyphs.Volume3, TrayGlyph, ink.Text),
                Glyph(Glyphs.Battery8, TrayGlyph, ink.Text),
            },
        };

        var time = Text(LocaleFormat.Time(SampleDay.Time), FamilyType.CaptionSize, ink.Text);
        var date = Text(LocaleFormat.Date(SampleDay.Today, DateStyle.Short), FamilyType.CaptionSize, ink.Text);
        time.HorizontalAlignment = HorizontalAlignment.Right;
        date.HorizontalAlignment = HorizontalAlignment.Right;
        var clock = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(FamilySpace.S2, 0, Edge, 0),
            Children = { time, date },
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, Edge, 0),
            Children =
            {
                new Border { Width = 24, Child = Glyph(Glyphs.ChevronUp, 12, ink.Text) },
                status,
                clock,
                Glyph(Glyphs.Bell, TrayGlyph, ink.Text),
            },
        };
    }

    private static TextBlock Glyph(string glyph, double size, Color colour) => new()
    {
        Text = glyph,
        FontFamily = Symbols,
        FontSize = size,
        Foreground = Brush(colour),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        IsTextScaleFactorEnabled = false,
    };

    private static TextBlock Text(string text, double size, Color colour, Windows.UI.Text.FontWeight? weight = null) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight ?? FontWeights.Normal,
        Foreground = Brush(colour),
        VerticalAlignment = VerticalAlignment.Center,
        TextLineBounds = TextLineBounds.Full,
        IsTextScaleFactorEnabled = false,
    };

    /// <summary>The app's icon font (Segoe Fluent Icons), as the theme names it.</summary>
    private static FontFamily Symbols => Application.Current.Resources.TryGetValue("SymbolThemeFontFamily", out var font) && font is FontFamily family
        ? family
        : new FontFamily("Segoe Fluent Icons");

    private static SolidColorBrush Brush(Color colour) => new(colour);

    private static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(0xFF, r, g, b);

    private static Color Argb(double alpha, byte r, byte g, byte b) => Color.FromArgb((byte)Math.Round(alpha * 255), r, g, b);

    /// <summary>
    /// [S-2203] Windows 11's taskbar colours in its dark and light mode: the neutral it lays over
    /// the desktop, the hairline along its top, the text, the Search pill, the button in front, the
    /// default accent and the running pill.
    /// </summary>
    private readonly record struct Ink(bool Dark)
    {
        public Color Fill => Dark ? Argb(0.85, 0x1C, 0x1C, 0x1C) : Argb(0.85, 0xF3, 0xF3, 0xF3);

        public Color Hairline => Dark ? Argb(0.1, 0xFF, 0xFF, 0xFF) : Argb(0.1, 0, 0, 0);

        public Color Text => Dark ? Rgb(0xFF, 0xFF, 0xFF) : Argb(0.9, 0, 0, 0);

        public Color Secondary => Dark ? Argb(0.79, 0xFF, 0xFF, 0xFF) : Argb(0.62, 0, 0, 0);

        public Color Weather => Dark ? Rgb(0xE6, 0xEA, 0xF0) : Rgb(0x6B, 0x75, 0x82);

        public Color Pill => Dark ? Argb(0.06, 0xFF, 0xFF, 0xFF) : Argb(0.7, 0xFF, 0xFF, 0xFF);

        public Color PillEdge => Dark ? Argb(0.09, 0xFF, 0xFF, 0xFF) : Argb(0.08, 0, 0, 0);

        public Color Highlight => Dark ? Argb(0.06, 0xFF, 0xFF, 0xFF) : Argb(0.7, 0xFF, 0xFF, 0xFF);

        public Color HighlightEdge => Dark ? Argb(0.07, 0xFF, 0xFF, 0xFF) : Argb(0.06, 0, 0, 0);

        public Color Accent => Dark ? Rgb(0x60, 0xCD, 0xFF) : Rgb(0x00, 0x5F, 0xB8);

        public Color Running => Dark ? Argb(0.55, 0xFF, 0xFF, 0xFF) : Argb(0.45, 0, 0, 0);
    }
}
#endif
