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
/// The General page: start with Windows and the desktop. The language is on Language &amp; region
/// ([S-3797]). [S-3833] Edit mode is entered on the desktop (and from a view's menu), the view new
/// displays show is on the Displays page, and the local network has no switch ([S-3840]).
/// </summary>
internal static partial class ConfigPages
{
    private static FrameworkElement BuildGeneral(AppHost host)
    {
        var rows = new List<FrameworkElement> { FluentLayout.Header(Str.Shell_Settings_General_System.Text()) };

        AddStartupRows(rows, host);

        // [S-930] / [S-920] The two desktop-integration switches, as in the original's General page.
        rows.Add(DesktopIconsRow(host));
        rows.Add(GameModeRow(host));

        return Page(Str.Settings_Nav_General.Text(), rows);
    }
}
