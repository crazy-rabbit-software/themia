using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using P = Themia.Sdk.Kit.ShellMetrics.Page;

namespace Themia.App.Shell;

/// <summary>
/// [S-2345] [S-11700] A tab of a strip, the one tab of every page (the add page's, the selection page's),
/// as WinUI's SelectorBar draws its items and Karpo's tab strip (§592) after it: 40 tall, its words 12 in
/// from either side, no fill until pointed at, its words in Body and secondary; the chosen one's in
/// BodyStrong and primary over the accent's 16 × 3 pill.
/// </summary>
internal sealed partial class PageTabButton : Button
{
    private readonly TextBlock _words;

    private readonly Rectangle _pill = new()
    {
        Width = ShellMetrics.Bar.PillWidth,
        Height = ShellMetrics.Bar.PillHeight,
        RadiusX = ShellMetrics.Bar.PillRadius,
        RadiusY = ShellMetrics.Bar.PillRadius,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Bottom,
        Fill = Family.Brush(ColourRole.Accent),
        IsHitTestVisible = false,
    };

    public PageTabButton(string key, string title, Action choose)
    {
        ArgumentNullException.ThrowIfNull(choose);
        Key = key;
        _words = Fluent.Text(FamilyType.Body);
        _words.Text = title;
        _words.VerticalAlignment = VerticalAlignment.Center;
        _words.HorizontalAlignment = HorizontalAlignment.Center;
        Content = new Grid { Children = { _words, _pill } };
        Height = P.Tabs;
        Padding = new Thickness(P.TabPad, 0, P.TabPad, 0);
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Tag = "tab:" + key;
        Subtle(this);
        AutomationProperties.SetName(this, title);
        Click += (_, _) => choose();
        Chosen = false;
    }

    /// <summary>The tab's key.</summary>
    public string Key { get; }

    /// <summary>The tab shown: its words strong over the pill.</summary>
    public bool Chosen
    {
        get => _pill.Visibility == Visibility.Visible;
        set
        {
            _pill.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            _words.FontWeight = value ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
            _words.Foreground = ThemeBrush.Of(value ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush", Colors.Gray);
            AutomationProperties.SetItemStatus(this, value ? "selected" : string.Empty);
        }
    }

    /// <summary>
    /// [S-2345] A strip of <paramref name="tabs"/> (key, words), <paramref name="chosen"/> marked, 4 apart from
    /// the cards' start; a tab's click chooses it.
    /// </summary>
    public static StackPanel Strip(IEnumerable<(string Key, string Title)> tabs, string chosen, Action<string> choose)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(choose);
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = P.TabGap };
        foreach (var (key, title) in tabs)
        {
            strip.Children.Add(new PageTabButton(key, title, () => choose(key)) { Chosen = key == chosen });
        }

        return strip;
    }

    /// <summary>No fill and no rim at rest, the subtle fills under the pointer and pressed.</summary>
    internal static void Subtle(Button button)
    {
        button.MinWidth = 0;
        button.MinHeight = 0;
        var clear = ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);
        button.Resources["ButtonBackground"] = clear;
        button.Resources["ButtonBackgroundPointerOver"] = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);
        button.Resources["ButtonBackgroundPressed"] = ThemeBrush.Of("SubtleFillColorTertiaryBrush", Colors.DimGray);
        button.Resources["ButtonBackgroundDisabled"] = clear;
        var rim = "Button" + nameof(Control.BorderBrush);
        button.Resources[rim] = clear;
        button.Resources[rim + "PointerOver"] = clear;
        button.Resources[rim + "Pressed"] = clear;
        button.Resources[rim + "Disabled"] = clear;
        var text = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White);
        button.Resources["ButtonForeground"] = text;
        button.Resources["ButtonForegroundPointerOver"] = text;
        button.Resources["ButtonForegroundPressed"] = text;
    }
}
