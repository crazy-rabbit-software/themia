using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Alarm;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4628]..[S-4630] One alarm (Karpo §106) on the timers' face ([S-6739]): its time as the Readout on
/// the Hero steps, centred, its name and what it is doing ("Tomorrow", "Weekdays", "Ringing") the line
/// under, and its switch in the Controls row — Dismiss and Snooze in the switch's place while it rings.
/// What the alarm is <i>doing</i> is the app's alarm service's ([S-4623]), which rings it whether or not
/// this view exists. Several alarms are several widgets.
/// </summary>
internal sealed partial class AlarmView : Grid, IWidgetView, IWidgetOpens, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => AlarmModule.Opens;

    private readonly ReadoutBlock _time = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    // Three rows of controls, one shown: a row folds its own parts, so a part is never hidden by hand.
    private readonly ControlsBlock _switchRow = new();
    private readonly ControlsBlock _ringRow = new();
    private readonly ControlsBlock _snoozedRow = new();
    private readonly ToggleSwitch _switch;

    private Ticker? _ticker;
    private IDisposable? _minute;
    private readonly IWidgetHost _host;
    private IWidgetSurface? _surface;
    private AlarmConfig _config = new();
    private WidgetArea _area;
    private TimerPlan _plan;
    private bool _detached;

    /// <summary>Debug renders: what the alarm is doing, in place of the service's answer.</summary>
#if DEBUG
    private AlarmStatus? _previewStatus;
#else
    // Release has no renders: the field's Debug-only writer (the .Preview.cs part) is not compiled.
    private AlarmStatus? _previewStatus => null;
#endif

    public AlarmView(IWidgetHost host)
    {
        _host = host;
        Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _switch = _switchRow.Switch(false, string.Empty, Switch);
        _ringRow.Main(Str.Alarm_Dismiss.Text(), Dismiss);
        _ringRow.Button(Str.Alarm_Snooze.Text(), Snooze);
        _snoozedRow.Main(Str.Alarm_Dismiss.Text(), Dismiss);

        Children.Add(_time);
        foreach (var row in new[] { _switchRow, _ringRow, _snoozedRow })
        {
            row.HorizontalAlignment = HorizontalAlignment.Center;
            SetRow(row, 1);
            Children.Add(row);
        }
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;

        if (_host.Alarms is { } alarms)
        {
            alarms.Changed -= Render;
            alarms.Changed += Render;
        }

        // [S-4630] "Today" becomes "Tomorrow" at midnight: the minute is the slowest tick that is right.
        _minute = ticker.Subscribe(TimeSpan.FromMinutes(1), Render);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as AlarmConfig ?? new AlarmConfig();
        Lay();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    private void OnLanguage(Locale locale) => Lay();

    private string WidgetId => _surface?.WidgetId ?? "preview";

    private AlarmStatus Status()
    {
        if (_previewStatus is { } preview)
        {
            return preview;
        }

        if (_host.Alarms is { } alarms)
        {
            return alarms.Status(WidgetId);
        }

        // No service (a debug render): what the plan alone says.
        var plan = _config.Plan;
        return plan.On ? new AlarmStatus(AlarmPhase.Set, AlarmSchedule.Next(plan, DateTime.UtcNow, TimeZoneInfo.Local)) : new AlarmStatus(AlarmPhase.Off);
    }

    // ---- actions ----------------------------------------------------------------------------

    private void Switch(bool on)
    {
        if (_detached || _config.On == on || (_surface?.IsPreview ?? false))
        {
            return;
        }

        _config.On = on;
        _surface?.RequestSave();
        _host.Alarms?.Sync();
        Render();
    }

    private void Snooze() => _host.Alarms?.Snooze(WidgetId);

    private void Dismiss() => _host.Alarms?.Dismiss(WidgetId);

    /// <summary>[S-4628] The card menu answers a ring too, and a snooze (a tile too small for the Controls row).</summary>
    public IReadOnlyList<WidgetAction> ContextActions => Status().Phase switch
    {
        AlarmPhase.Ringing => [new WidgetAction(AlarmModule.Glyph, Str.Alarm_Snooze.Text(), Snooze), new WidgetAction(Glyphs.Dismiss, Str.Alarm_Dismiss.Text(), Dismiss)],
        AlarmPhase.Snoozed => [new WidgetAction(Glyphs.Dismiss, Str.Alarm_Dismiss.Text(), Dismiss)],
        _ => [],
    };

    // ---- keys ([S-4630]) ----------------------------------------------------------------------

    /// <summary>Space or Enter switch the alarm, or dismiss it while it rings or is snoozed; Escape lets go.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached || key is not (WidgetKey.Space or WidgetKey.Enter))
        {
            return false;
        }

        if (Status().Phase is AlarmPhase.Ringing or AlarmPhase.Snoozed)
        {
            Dismiss();
        }
        else
        {
            Switch(!_config.On);
        }

        return true;
    }

    /// <summary>S snoozes the alarm while it rings.</summary>
    public bool HandleText(char character)
    {
        if (_detached || char.ToUpperInvariant(character) != 'S' || Status().Phase != AlarmPhase.Ringing)
        {
            return false;
        }

        Snooze();
        return true;
    }

    // ---- the face ([S-4629]) ------------------------------------------------------------------

    /// <summary>[S-4629] The face for this size: the time's step, its line under and the controls as they fit.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var area = _area.IsMeasured ? _area : new WidgetArea(ActualWidth, ActualHeight);
        if (!area.IsMeasured)
        {
            Render();
            return;
        }

        var (_, unit) = AlarmFace.Split(AlarmWords.Time(Strings.Current, _config.Plan));
        var worst = AlarmFace.Worst(unit);
        _plan = AlarmFace.Plan(area, unit);
        _time.Size(area, area.Width - (2 * FamilySpace.Inset), _plan.Box, worst);
        RowSpacing = _plan.Controls ? FamilySpace.S2 : 0;
        Render();
    }

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        var locale = Strings.Current;
        var now = DateTime.UtcNow;
        var zone = _host.Alarms?.Zone ?? TimeZoneInfo.Local;
        var status = Status();
        var ringing = status.Phase == AlarmPhase.Ringing;
        var snoozed = status.Phase == AlarmPhase.Snoozed;
        var name = AlarmWords.Name(locale, _config);
        var time = AlarmWords.Time(locale, _config.Plan);
        var (value, unit) = AlarmFace.Split(time);

        // [S-4628] Off, the time and its words are secondary: the alarm is there, not set.
        var role = status.Phase == AlarmPhase.Off ? ColourRole.TextSecondary : ColourRole.TextPrimary;
        _time.Show(value, unit, _plan.Under ? $"{name} · {AlarmWords.Hint(locale, status, _config.Plan, now, zone)}" : null, valueRole: role);

        // [S-4628] While it rings (or waits out a snooze), the buttons that answer it stand where the switch was.
        Show(_switchRow, _plan.Controls && !ringing && !snoozed);
        Show(_ringRow, _plan.Controls && ringing);
        Show(_snoozedRow, _plan.Controls && snoozed);

        _switchRow.Set(_switch, _config.On);
        var label = (_config.On ? AlarmStr.Alarm_SwitchOff : AlarmStr.Alarm_SwitchOn).Format(("name", name));
        ToolTipService.SetToolTip(_switch, label);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_switch, $"{name} {time}");
    }

    private static void Show(UIElement element, bool shown) => element.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;

        if (_host.Alarms is { } alarms)
        {
            alarms.Changed -= Render;
        }

        _minute?.Dispose();
        _minute = null;
        _ticker = null;
    }
}
