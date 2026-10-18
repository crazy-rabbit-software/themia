using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Widget.Ping;

namespace Themia.App.Shell.Config;

internal static class PingSettings
{
    public static void Rows(List<FrameworkElement> rows, PingConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(PingStr.Widget_Ping_Label.Text()));

        PingHostRow.Add(rows, () => config.Host, value => config.Host = value, save);

        MeterSettingsRows.Rows(rows, config, save);
    }
}
