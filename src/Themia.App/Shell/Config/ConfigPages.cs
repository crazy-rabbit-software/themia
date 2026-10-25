using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>
/// The bodies of the config window.
/// <para>
/// Every page edits the live state and saves as it goes, rather than holding a draft behind an OK
/// button: the desktop is visible behind this window, so a setting the user cannot see take effect
/// is a setting they cannot judge. A widget's own options are not here: they are on its properties
/// menu in edit mode ([S-6536]).
/// </para>
/// </summary>
internal static partial class ConfigPages
{
    private const string Scope = "config";

    /// <summary>
    /// [S-701] The app pages, in menu order within their slot. A page is in the navigation, and can
    /// be opened, once it has a line here. [S-3584] None is left: every page is the edit bar's
    /// settings menu now ([S-3524]: General, Appearance, Accounts, Language &amp; region, Online
    /// updates, Themia Pro, Back up &amp; reset, About Themia), and Displays and Views its Display and
    /// View menus ([S-3540]). Nothing opens the window any more; it goes with the next step.
    /// </summary>
    public static IReadOnlyList<AppPageEntry> AppPages { get; } = [];

    public static FrameworkElement Build(PageContext context, ConfigTarget target) => target switch
    {
        ConfigTarget.App app => BuildApp(context, app.Page),
        _ => Page(Str.Common_Settings.Text(), []),
    };

    private static FrameworkElement BuildApp(PageContext context, AppPage page)
    {
        foreach (var entry in AppPages)
        {
            if (entry.Page == page)
            {
                return entry.Build(context);
            }
        }

        // A reserved page nobody has built yet: say so in the log rather than throw from the window.
        Log.Warn(Scope, $"no settings page is registered for {page}");
        return Page(Str.Common_Settings.Text(), []);
    }

    // -----------------------------------------------------------------------------------------
    // Shared pieces
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1541] A page: its rows under the window's breadcrumb title. The title is handed to the
    /// window rather than drawn here, so it stays put while the rows scroll, as in Windows Settings.
    /// </summary>
    private static SettingsPage Page(string title, IReadOnlyList<FrameworkElement> rows, string? subtitle = null)
        => new SettingsPage(title, subtitle).With(rows);

    private static TextBlock Note(string text) => FluentLayout.Note(text);
}
