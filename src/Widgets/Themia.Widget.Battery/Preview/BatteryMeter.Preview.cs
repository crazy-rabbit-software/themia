using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Battery;

namespace Themia.App.Widgets.Meters;

/// <summary>Debug renders only (Preview/, [S-11850]): the reading <c>--state unavailable</c> draws.</summary>
internal sealed partial class BatteryMeter
{
    /// <summary>[S-1940] A desktop's reading, on any machine.</summary>
    public override MeterReading? AbsentReading => MeterReading.Missing(Str.WidgetsB_Battery_None.Text());
}
