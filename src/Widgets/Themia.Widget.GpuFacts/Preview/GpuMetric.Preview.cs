using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.App.Widgets.Details;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.GpuFacts;

/// <summary>Debug renders only (Preview/, [S-11850]): what <c>--state unavailable</c> draws.</summary>
internal sealed partial class GpuMetricPart
{
    /// <summary>[S-1942] No adapter, on any machine.</summary>
    public override MetricShown? Absent() => MetricRules.Pick(GpuDetailRules.Absent(Strings.Current, GpuFactsStr.Details_Gpu_None.Text()), key);
}
