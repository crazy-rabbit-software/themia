using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1531]/[S-1533] The few things the Help and Themia widgets offer to do on a large tile: turn
/// edit mode on, open Settings, and open one of the app's own pages in the browser. One place, so
/// both widgets say and do the same thing.
/// <para>
/// The buttons call through <see cref="AppServices"/>, which the host fills; in a debug render there is
/// no host, so they draw and do nothing. Links go through <see cref="ShellLauncher"/>, the app's one
/// launch path, not a <see cref="HyperlinkButton.NavigateUri"/> that would bypass it.
/// </para>
/// </summary>
internal static class AppActions
{
    /// <summary>The project's public home, where its releases and issues live.</summary>
    public const string SourceUrl = "https://github.com/crazy-rabbit-software/themia";

    public const string ReleaseNotesUrl = SourceUrl + "/releases";

    public const string FeedbackUrl = SourceUrl + "/issues";

    public const string MakerUrl = "https://lometics.com/";

    /// <summary>"Edit widgets": the same edit mode as Ctrl+Alt+W.</summary>
    public static Button EditLayout() => Command(Glyphs.Edit, Str.WidgetsA_AppActions_EditWidgets.Text(), () => AppServices.EditLayout?.Invoke());

    /// <summary>"Settings": the settings window, on its first page.</summary>
    public static Button Settings() => Command(Glyphs.Settings, Str.Common_Settings.Text(), () => AppServices.ShowSettings?.Invoke());

    /// <summary>A text link that opens <paramref name="url"/> in the browser.</summary>
    public static HyperlinkButton Link(string text, string url)
    {
        var link = new HyperlinkButton
        {
            Content = text,
            Padding = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0),
            FontSize = FluentType.Caption.FontSize,
            MinHeight = 0,
        };

        ToolTipService.SetToolTip(link, url);
        link.Click += (_, _) => ShellLauncher.Open(url);
        return link;
    }

    private static Button Command(string glyph, string text, Action action)
    {
        var button = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Fluent.Space2,
                Children =
                {
                    new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize },
                    new TextBlock { Text = text },
                },
            },
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
        };

        button.Click += (_, _) => action();
        return button;
    }
}
