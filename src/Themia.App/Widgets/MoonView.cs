using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Astronomy;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3100] The moon where the observer is: its phase as a drawn disc, the phase's name and how much
/// of it is lit, then moonrise, moonset and the next full or new moon as list rows, computed offline.
/// <para>
/// [S-3105] The ladder is <see cref="MoonLayout"/>; this view only builds it. The disc is the hero:
/// it is never dropped and takes what the rungs leave, so a larger tile draws a larger moon rather
/// than anything new. On a wide strip the text stands beside the disc.
/// </para>
/// <para>
/// The drawing is <see cref="SkyMoonDisc"/>, the same lit-limb outline the row glyphs use, mirrored
/// for a southern observer and dimmed while the moon is below the horizon ([S-169]).
/// </para>
/// </summary>
internal sealed partial class MoonView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetSizeAware
{
    private readonly SkyMoonDisc _disc = new();
    private readonly TextBlock _name = Fluent.Text(MoonLayout.NameStep);
    private readonly TextBlock _percent = Fluent.Text(MoonLayout.PercentStep, "TextFillColorTertiaryBrush");
    private readonly StackPanel _text;

    /// <summary>The text and the rows, one column: under the disc, or beside it on a strip.</summary>
    private readonly StackPanel _stack;

    /// <summary>Everything, centred in the tile as one group.</summary>
    private readonly Grid _content = new() { VerticalAlignment = VerticalAlignment.Center };

    private readonly Grid _rowGrid = new();
    private readonly SkyMoonDisc _riseGlyph = new();
    private readonly SkyMoonDisc _setGlyph = new();
    private readonly SkyMoonDisc _majorGlyph = new();
    private readonly SkyEventRow _riseRow;
    private readonly SkyEventRow _setRow;
    private readonly SkyEventRow _majorRow;

    /// <summary>The rows in <see cref="MoonLayout.Ladder"/> order.</summary>
    private readonly SkyEventRow[] _rows;

    private MoonConfig _config = new();
    private IWidgetSurface? _surface;
    private Ticker? _ticker;
    private IDisposable? _subscription;
    private DateTimeOffset _now = DateTimeOffset.Now;
    private WidgetArea _area;
    private MoonPlan _plan;

    /// <summary>What a row has for its label and value, from the last <see cref="Lay"/>.</summary>
    private double _rowRoom = double.PositiveInfinity;

    public MoonView()
    {
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        _percent.HorizontalAlignment = HorizontalAlignment.Center;

        _text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _name, _percent },
        };

        _riseRow = new SkyEventRow(_riseGlyph.Root);
        _setRow = new SkyEventRow(_setGlyph.Root);
        _majorRow = new SkyEventRow(_majorGlyph.Root);
        _rows = [_riseRow, _setRow, _majorRow];

        for (var i = 0; i < _rows.Length; i++)
        {
            _rowGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            SetRow(_rows[i].Root, i);
            _rowGrid.Children.Add(_rows[i].Root);
        }

        _stack = new StackPanel { Children = { _text, _rowGrid } };

        _content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _content.Children.Add(_disc.Root);
        _content.Children.Add(_stack);

        Children.Add(_content);
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;

        // [S-1110] The texts follow the app's language. A static event: Detach unsubscribes.
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    private void OnLanguage(Locale locale) => Lay();

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MoonConfig ?? new MoonConfig();
        PublishTitle();

        // One cadence for good: the config cannot change it, so subscribe once.
        _subscription ??= _ticker?.Subscribe(_config.TickInterval, Tick);
        Refresh();
    }

    /// <summary>[S-663] The card's word on how much room there is.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    public void Detach()
    {
        Strings.Changed -= OnLanguage;
        _subscription?.Dispose();
        _subscription = null;
    }

    private void Tick()
    {
        _now = DateTimeOffset.Now;
        Refresh();
    }

    /// <summary>[S-3105] Place the parts for this size, then fill them in.</summary>
    private void Lay()
    {
        var compact = _area.WidthClass == SizeClass.Compact || _area.HeightClass <= SizeClass.Regular;
        var rowHeight = compact ? Fluent.RowHeightCompact : Fluent.RowHeight;
        _plan = MoonLayout.Plan(_area, rowHeight, LongestPhaseName());

        _disc.Resize(_plan.Disc);
        _name.Visibility = Show(_plan.Name);
        _percent.Visibility = Show(_plan.Percent);
        _text.Visibility = Show(_plan.Name || _plan.Percent);
        _rowGrid.Visibility = Show(_plan.Rows > 0);

        var side = _plan.Side;
        var discRoot = _disc.Root;
        discRoot.VerticalAlignment = VerticalAlignment.Center;
        _stack.VerticalAlignment = VerticalAlignment.Center;

        // Disc left and the text beside it on a strip; else the disc over the text, both centred.
        Grid.SetRow(discRoot, 0);
        Grid.SetRowSpan(discRoot, side ? 2 : 1);
        Grid.SetColumn(discRoot, 0);
        Grid.SetColumnSpan(discRoot, side ? 1 : 2);
        discRoot.HorizontalAlignment = side ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        discRoot.Margin = side
            ? new Thickness(Fluent.Space4, 0, MoonLayout.Gap, 0)
            : new Thickness(Fluent.Space4, 0, Fluent.Space4, _text.Visibility == Visibility.Visible || _plan.Rows > 0 ? MoonLayout.Gap : 0);

        Grid.SetRow(_stack, side ? 0 : 1);
        Grid.SetRowSpan(_stack, side ? 2 : 1);
        Grid.SetColumn(_stack, side ? 1 : 0);
        Grid.SetColumnSpan(_stack, side ? 1 : 2);

        // On a strip the rows run to the card's right edge; the text keeps the inset where it sits.
        _text.Margin = side ? new Thickness(0, 0, Fluent.Space4, 0) : new Thickness(Fluent.Space4, 0, Fluent.Space4, 0);
        _name.HorizontalAlignment = side ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        _percent.HorizontalAlignment = _name.HorizontalAlignment;
        _rowGrid.Margin = side ? new Thickness(-Fluent.Space4, 0, 0, 0) : new Thickness(0);

        // The rows' label, note and value share the row less its padding and glyph.
        var rowWidth = side ? _area.Width - (2 * Fluent.Space4) - _plan.Disc - MoonLayout.Gap : _area.Width;
        var rowRoom = rowWidth - (2 * Fluent.Space4) - Fluent.IconSize - Fluent.Space3;
        _rowRoom = rowRoom;

        for (var i = 0; i < _rows.Length; i++)
        {
            var row = _rows[i];
            row.Root.Visibility = Show(i < _plan.Rows);
            row.SetDensity(compact);
            row.Room = rowRoom;
            row.AllowCountdown = true;
            row.AllowNote = true;
        }

        Refresh();
    }

    /// <summary>Recompute the readout. Called on a tick, a config change, a resize and a language change.</summary>
    private void Refresh()
    {
        var latitude = _config.EffectiveLatitude;
        var longitude = _config.EffectiveLongitude;
        var southern = latitude < 0;
        var locale = Strings.Current;

        var moon = MoonPhase.At(_now);
        var up = SkyPath.PositionOf(MoonPhase.SublunarPoint(_now), latitude, longitude).Up;

        _disc.Update(moon.Phase, southern, up);
        _riseGlyph.Update(moon.Phase, southern, up);
        _setGlyph.Update(moon.Phase, southern, up);
        _name.Text = MoonPhase.Label(moon.Name);
        _percent.Text = LocaleFormat.Percent(locale, Math.Round(moon.Illumination * 100));

        var times = MoonPhase.NextRiseSet(_now, latitude, longitude);
        var next = times.Next;
        var countdown = next is { } evt ? SkyText.Countdown(evt.At - _now) : null;
        var riseNext = next is { IsRise: true };

        var riseLabel = (Str.WidgetsC_Moon_Rise.Text(), Str.WidgetsC_Moon_Short_Rise.Text());
        var setLabel = (Str.WidgetsC_Moon_Set.Text(), Str.WidgetsC_Moon_Short_Set.Text());
        var riseValue = SkyText.EventTime(locale, times.Rise, _now);
        var setValue = SkyText.EventTime(locale, times.Set, _now);

        // [S-3105] With one row, it is the next of the two; with both, rise then set, the next one
        // carrying its countdown.
        if (_plan.Rows == 1 && next is { IsRise: false })
        {
            _riseRow.SetLabel(setLabel.Item1, setLabel.Item2);
            _riseRow.Set(setValue, countdown);
        }
        else
        {
            _riseRow.SetLabel(riseLabel.Item1, riseLabel.Item2);
            _riseRow.Set(riseValue, riseNext ? countdown : null);
        }

        _setRow.SetLabel(setLabel.Item1, setLabel.Item2);
        _setRow.Set(setValue, next is { IsRise: false } ? countdown : null);

        // [S-1491] The next full or new moon, in the machine's zone like every other time here.
        var major = MoonPhase.NextMajor(_now);
        _majorRow.SetLabel(MoonPhase.Label(major.Name));
        // The weekday goes first where the row cannot hold it beside the phase's name ("Oct 26").
        var majorLabel = MoonPhase.Label(major.Name);
        var majorDate = SkyText.ShortDate(locale, major.At.ToLocalTime());
        if (FluentType.EstimateWidth(FluentType.Body, majorLabel.Length)
            + FluentType.EstimateWidth(FluentType.BodyStrong, majorDate.Length) + Fluent.Space2 > _rowRoom)
        {
            majorDate = LocaleFormat.Date(locale, DateOnly.FromDateTime(major.At.ToLocalTime().DateTime), DateStyle.MonthDayShort);
        }

        _majorRow.SetPlain(majorDate, null);
        _majorGlyph.Update(major.Name == MoonPhaseName.FullMoon ? 0.5 : 0, southern, up: true);
    }

    /// <summary>The longest phase name in the current language, so the name never trims at a phase change.</summary>
    private static int LongestPhaseName()
    {
        var longest = 0;

        foreach (var name in Enum.GetValues<MoonPhaseName>())
        {
            longest = Math.Max(longest, MoonPhase.Label(name).Length);
        }

        return longest;
    }

    /// <summary>The place names which moon this is: rise and set are that place's.</summary>
    private void PublishTitle()
    {
        var title = _config.Place;
        _surface?.SetHeaderTitle(string.IsNullOrWhiteSpace(title) ? null : title);
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
