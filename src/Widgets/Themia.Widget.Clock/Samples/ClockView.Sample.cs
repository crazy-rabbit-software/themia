using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2050] The clock's sample: stopped at 10:42:18 in its own zone, so every style and every
/// language is drawn at the same moment (the word style says "twenty to eleven").
/// </summary>
public sealed partial class ClockView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        // The sample moment ([S-2050]) in the clock's own zone, eighteen seconds into the minute
        // so the second hand is off twelve.
        _frozen = SampleDay.At(SampleDay.Today, SampleDay.Time.Add(TimeSpan.FromSeconds(18)), _config.ResolveTimeZone());
        ApplyConfig(_config);
        return Task.CompletedTask;
    }
}
