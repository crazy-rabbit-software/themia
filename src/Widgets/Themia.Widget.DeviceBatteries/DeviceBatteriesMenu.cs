using Themia.Sdk.WinUI.Kit;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Sdk.WinUI;

namespace Themia.Widget.DeviceBatteries;

/// <summary>
/// [S-10006] The widget's options, on its properties menu ([S-3719]) after Appearance: where a battery
/// counts as low, whether a charging device is listed, and how many rows ([S-11028]). Nothing else: the
/// rows' icon and value are the widget's, and how often Windows is asked is no setting.
/// </summary>
internal static class DeviceBatteriesMenu
{
    public static void Lines(WidgetMenuLines menu, DeviceBatteriesConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        menu.Lines.Add(menu.Choice(
            Glyphs.Battery,
            DeviceBatteriesStr.DeviceBatteries_Settings_Low.Text(),
            [.. DeviceBatteriesConfig.Thresholds.Select(t => (t, LocaleFormat.Percent(Strings.Current, t)))],
            config.EffectiveThreshold,
            value => config.LowThreshold = value));

        menu.Lines.Add(menu.Toggle(Glyphs.ChargingBolt, DeviceBatteriesStr.DeviceBatteries_Settings_Charging.Text(), config.ShowCharging, on => config.ShowCharging = on));

        // [S-11028] How many rows, as every list states them ([S-2440]); a row always has its two lines.
        menu.Lines.AddRange(KitMenuLines.Rows(menu, config, offersLines: false));
    }
}
