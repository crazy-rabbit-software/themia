using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Astronomy;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.SunTimes;

/// <summary>
/// [S-11115] The sun's times as fact rows (<see cref="SkyEventRow"/>, [S-6735]): exactly the picked rows,
/// in their order, one line each, from the content's top; the next of sunrise and sunset emphasised. A
/// name takes its short form where the long one and the value do not fit side by side.
/// </summary>
internal sealed partial class SunTimesView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSizeAware
{
    private readonly StackPanel _stack = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly List<SkyEventRow> _rows = [];
    private readonly HeaderParts _about = new();
    private SunTimesConfig _config = new();
    private IDisposable? _subscription;
    private Ticker? _ticker;
    private DateTimeOffset _now = DateTimeOffset.Now;
    private double _room = double.PositiveInfinity;

    public SunTimesView()
    {
        Children.Add(_stack);
    }

    /// <summary>[S-2260] The place as the header's label beside the widget's name; none once it is cleared ([S-2848]).</summary>
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
        _config = config as SunTimesConfig ?? new SunTimesConfig();
        _about.SetAbout(_config.PlaceLabel);
        _subscription ??= _ticker?.Subscribe(_config.TickInterval, () =>
        {
            _now = DateTimeOffset.Now;
            Render();
        });
        Render();
    }

    /// <summary>The box's width only decides a name's short form.</summary>
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
        var times = SunTimeRules.For(_config.Times, _now, _config.EffectiveLatitude, _config.EffectiveLongitude);
        for (var i = 0; i < times.Count; i++)
        {
            if (i >= _rows.Count || _rows[i].Root.Tag is not SunTimeKind kind || kind != times[i].Kind)
            {
                var row = new SkyEventRow(Glyph(times[i].Kind)) { Lines = 1 };
                row.Root.Tag = times[i].Kind;
                if (i < _rows.Count)
                {
                    _stack.Children[i] = row.Root;
                    _rows[i] = row;
                }
                else
                {
                    _stack.Children.Add(row.Root);
                    _rows.Add(row);
                }
            }

            var shown = _rows[i];
            shown.Room = _room;
            shown.SetLabel(times[i].Name, times[i].ShortName);
            if (times[i].Next)
            {
                shown.Set(times[i].Value, string.Empty);
            }
            else
            {
                shown.SetPlain(times[i].Value, null);
            }
        }

        while (_rows.Count > times.Count)
        {
            _rows.RemoveAt(_rows.Count - 1);
            _stack.Children.RemoveAt(_stack.Children.Count - 1);
        }
    }

    /// <summary>A row's 16 lead, checked against the font ([S-733]).</summary>
    internal static string Glyph(SunTimeKind kind) => kind switch
    {
        SunTimeKind.Daylight => Glyphs.Clock,
        SunTimeKind.Dusk => Glyphs.Moon,
        _ => Glyphs.Sun,
    };
}
