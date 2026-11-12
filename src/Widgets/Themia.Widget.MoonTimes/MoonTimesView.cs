using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Astronomy;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.MoonTimes;

/// <summary>
/// [S-11125] The moon's times as fact rows (<see cref="SkyEventRow"/>, [S-6735]): exactly the picked
/// rows, in their order, one line each, each led by the moon drawn at 16 (at its phase now for the rise
/// and set, full or new for those dates; a role lower while the moon is down); the next of rise and set
/// emphasised. A name takes its short form, a date drops its weekday, where the row cannot hold both.
/// </summary>
internal sealed partial class MoonTimesView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSizeAware
{
    private readonly StackPanel _stack = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly List<(SkyEventRow Row, SkyMoonDisc Lead)> _rows = [];
    private readonly HeaderParts _about = new();
    private MoonTimesConfig _config = new();
    private IDisposable? _subscription;
    private Ticker? _ticker;
    private DateTimeOffset _now = DateTimeOffset.Now;
    private double _room = double.PositiveInfinity;

    public MoonTimesView()
    {
        Children.Add(_stack);
    }

    /// <summary>[S-2260] The place as the header's label; none once it is cleared ([S-2848]).</summary>
    public FrameworkElement? HeaderTrailing => _about;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MoonTimesConfig ?? new MoonTimesConfig();
        _about.SetAbout(_config.PlaceLabel);
        _subscription ??= _ticker?.Subscribe(_config.TickInterval, () =>
        {
            _now = DateTimeOffset.Now;
            Render();
        });
        Render();
    }

    /// <summary>The box's width only decides a name's short form and a date's weekday.</summary>
    public void ApplySize(WidgetArea area)
    {
        _room = area.IsMeasured ? SkyRows.Room(area.Width) : double.PositiveInfinity;
        Render();
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
    }

    private void OnLanguage(Locale locale) => Render();

    private void Render()
    {
        var latitude = _config.EffectiveLatitude;
        var times = MoonTimeRules.For(_config.Times, _now, latitude, _config.EffectiveLongitude);
        var up = SkyPath.PositionOf(MoonPhase.SublunarPoint(_now), latitude, _config.EffectiveLongitude).Up;
        while (_rows.Count < times.Count)
        {
            var lead = new SkyMoonDisc();
            var row = new SkyEventRow(lead.Root) { Lines = 1 };
            _rows.Add((row, lead));
            _stack.Children.Add(row.Root);
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            var (row, lead) = _rows[i];
            row.Root.Visibility = i < times.Count ? Visibility.Visible : Visibility.Collapsed;
            if (i >= times.Count)
            {
                continue;
            }

            var time = times[i];
            var value = time.ShortValue is { } shorter && !SkyRows.Fits(_room, time.Name, time.Value) ? shorter : time.Value;
            row.Room = _room;
            row.SetLabel(time.Name, time.ShortName);
            if (time.Next)
            {
                row.Set(value, string.Empty);
            }
            else
            {
                row.SetPlain(value, null);
            }

            var rising = time.Kind is MoonTimeKind.Rise or MoonTimeKind.Set;
            lead.Update(time.LeadPhase, latitude < 0, up: !rising || up);
        }
    }
}
