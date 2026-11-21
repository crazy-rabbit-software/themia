using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Calendar;

/// <summary>
/// [S-14900] The calendar header: the period the view's calendars show (its style's: a month, a week, a day, a year) as
/// one tool line, its title and back and forward (owner, 2026-11-21: "split up the calendar into the month with the
/// arrows and the body of the calendar too, share the scrolling via the global state that we have"). Paging it (the
/// arrows, the card menu's Today, the keys, the wheel) says the new period on the view's links, so every calendar there
/// follows ([S-3626]); a calendar paged there moves it the same way ([S-3628]). It reads no account: it draws its
/// period alone, the same with or without one.
/// </summary>
internal sealed partial class CalendarHeaderView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetContextActions, IWidgetKeys
{
    /// <summary>The day changes at midnight: the header following today turns with it.</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

    private static readonly string ChevronLeft = Glyphs.ChevronLeft;
    private static readonly string ChevronRight = Glyphs.ChevronRight;

    /// <summary>[S-14901] The period's title and its two arrows: the card's one line.</summary>
    private readonly ToolLine _line = new();
    private readonly WidgetLinks _links = new();
    private readonly PointerEventHandler _wheel;

    private IWidgetSurface? _surface;
    private IDisposable? _tick;
    private CalendarHeaderConfig _config = new();
    private DateOnly _anchor = DateOnly.FromDateTime(DateTime.Now);
    private bool _followToday = true;

    /// <summary>[S-3628] The view's selected day: a period that holds it lands on it.</summary>
    private DateOnly? _selected;

    /// <summary>[S-3629] The wheel's part of a notch not yet paged.</summary>
    private int _wheelCarry;
    private bool _detached = true;

    /// <summary>[S-2050] The clock the view reads: the real one, or a sample's moment.</summary>
    private Func<DateTimeOffset> _clock = static () => DateTimeOffset.Now;

    public CalendarHeaderView()
    {
        _line.Margin = Family.SideInset;
        _line.Parts.Add(LinePartKind.Button, ToolLine.Button(ChevronLeft, CalendarStr.WidgetsA_Calendar_Previous.Text(), () => Navigate(-1)));
        _line.Parts.Add(LinePartKind.Button, ToolLine.Button(ChevronRight, CalendarStr.Common_Next.Text(), () => Navigate(1)));
        _line.VerticalAlignment = VerticalAlignment.Top;
        Children.Add(_line);

        // [S-3628] Followed once; joined again on every Attach, each told what the view holds at once.
        _links.Follow(LinkTopics.SelectedDate, OnSelected);
        _links.Follow(LinkTopics.VisibleRange, OnPeriod);
        _wheel = OnWheel;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock().LocalDateTime);

    private CalendarStyle Unit => _config.Unit;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        _detached = false;
        _config = config as CalendarHeaderConfig ?? new CalendarHeaderConfig();
        Strings.Changed += OnLanguage;
        AddHandler(PointerWheelChangedEvent, _wheel, false);
        if (_followToday)
        {
            _anchor = Today;
        }

        _links.Join(_surface?.Links);
        _tick = ticker.Subscribe(Tick, OnTick);
        Render();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        // [S-14902] Another period to page by: the day it is anchored on stays, its period is the new style's.
        _config = config as CalendarHeaderConfig ?? new CalendarHeaderConfig();
        _followToday = InView(Today);
        Render();
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        Strings.Changed -= OnLanguage;
        RemoveHandler(PointerWheelChangedEvent, _wheel);
        _wheelCarry = 0;
        _tick?.Dispose();
        _tick = null;
        _links.Leave();
    }

    /// <summary>[S-569] The card menu: Today while the period on screen does not hold it (its key is Home).</summary>
    public IReadOnlyList<WidgetAction> ContextActions
        => InView(Today) ? [] : [new(Glyphs.Calendar, CalendarStr.Calendar_Today.Text(), GoToday)];

    /// <summary>[S-2710] Its keys, the calendar's: Left / Page Up back, Right / Page Down forward, Home today.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        switch (CalendarFit.Key(key, CalendarStyle.Year))
        {
            case CalendarKeyCommand.Back:
                Navigate(-1);
                return true;

            case CalendarKeyCommand.Forward:
                Navigate(1);
                return true;

            case CalendarKeyCommand.Today:
                GoToday();
                return true;

            default:
                return false;
        }
    }

    /// <summary>[S-569] Whether <paramref name="day"/> is in the period on screen: a month counts its own days only.</summary>
    private bool InView(DateOnly day) => CalendarRules.InView(Unit, _anchor, Today, _config.Period, day);

    /// <summary>[S-3626] Page <paramref name="steps"/> periods back (negative) or forward, and say the new period on the view.</summary>
    private void Navigate(int steps)
    {
        if (steps == 0)
        {
            return;
        }

        for (var i = 0; i < Math.Abs(steps); i++)
        {
            _anchor = CalendarRules.Step(Unit, _anchor, Math.Sign(steps), _config.Period);
        }

        _followToday = InView(Today);
        Publish();
        Render();
    }

    private void GoToday()
    {
        _anchor = Today;
        _followToday = true;
        Publish();
        Render();
    }

    /// <summary>[S-3627] The period on screen, said on the view's links: every calendar there shows it.</summary>
    private void Publish() => _links.Set(LinkTopics.VisibleRange, CalendarShare.Period(Unit, _anchor, Today, _config.Period));

    /// <summary>[S-3628] A calendar of the view paged: show its period's own of the header's (a month, a week, ...).</summary>
    private void OnPeriod(RangeLink? period)
    {
        if (_detached || period is null)
        {
            return;
        }

        _anchor = CalendarShare.AnchorIn(period, Today, _selected);
        _followToday = InView(Today);
        Render();
    }

    /// <summary>[S-3621] A day selected on the view: the header shows its period, as every calendar does.</summary>
    private void OnSelected(DateLink? value)
    {
        if (_detached || value?.Date == _selected)
        {
            return;
        }

        _selected = value?.Date;
        if (value is { } link)
        {
            _anchor = CalendarShare.AnchorFor(Unit, _anchor, link.Date);
            _followToday = InView(Today);
            Render();
        }
    }

    /// <summary>[S-3629] The wheel pages the header, a period a notch; Ctrl with it is the desktop's.</summary>
    private void OnWheel(object sender, PointerRoutedEventArgs args)
    {
        if (_detached || args.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Control))
        {
            return;
        }

        var properties = args.GetCurrentPoint(this).Properties;
        if (properties.IsHorizontalMouseWheel)
        {
            return;
        }

        (var steps, _wheelCarry) = CalendarShare.Wheel(_wheelCarry, properties.MouseWheelDelta);
        Navigate(steps);
        args.Handled = true;
    }

    /// <summary>Across midnight a header on today's period stays on today's.</summary>
    private void OnTick()
    {
        if (_followToday && !InView(Today))
        {
            _anchor = Today;
            Render();
        }
    }

    private void OnLanguage(Locale locale) => Render();

    /// <summary>[S-568] The period's title, in the language and order of the user's ([S-1106]).</summary>
    private void Render()
    {
        if (_detached)
        {
            return;
        }

        _line.Label = CalendarRules.Title(Unit, _anchor, Today, _config.Period, Strings.Current);
    }
}
