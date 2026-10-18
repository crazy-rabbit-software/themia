using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

internal static partial class WidgetSettings
{
    public static void Ping(List<FrameworkElement> rows, PingConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.Widget_Ping_Label.Text()));

        PingHostRow.Add(rows, () => config.Host, value => config.Host = value, save);

        MeterSettingsRows.Rows(rows, config, save);
    }
}
