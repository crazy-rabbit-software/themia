using Themia.App.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.Date;

/// <summary>
/// [S-11850] The add page's picture of the date widget, in every build: the sample day ([S-2050]), held by this
/// view alone, never written into a store.
/// </summary>
internal sealed partial class DateView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        Freeze(SampleDay.Today);
        return Task.CompletedTask;
    }
}
