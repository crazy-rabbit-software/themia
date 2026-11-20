using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Widgets;
using Themia.App.Widgets.Details;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.BatteryFacts;

/// <summary>Debug renders only (Preview/, [S-11850]): what <c>--state unavailable</c> draws.</summary>
internal sealed partial class BatteryMetricPart
{
    /// <summary>[S-1942] A desktop's read on any machine.</summary>
    public override MetricShown? Absent()
        => MetricRules.Pick(BatteryDetailRules.Sheet(Strings.Current, new BatteryPower(false, null, true, false, null), null), key);
}
