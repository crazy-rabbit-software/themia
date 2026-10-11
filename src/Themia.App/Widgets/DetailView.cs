using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets.Details;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-2400] The face every system detail widget shares: a hero (the main figure), a caption, the
/// facts and, for some kinds, a list. Where the figures come from is the kind's
/// <see cref="DetailPart"/> (<c>Details/&lt;Kind&gt;DetailPart.cs</c>); this file is the layout, so
/// the detail widgets grow and shrink the same way.
/// <para>
/// [S-2402] The ladder is <see cref="DetailFace"/>: a tiny tile is the hero alone; a strip puts the
/// hero on the left and the facts beside it; otherwise hero, caption and facts stack, the facts
/// growing from lines into tiles and large tiles when they all fit, and a list takes the rest.
/// Elements are kept and re-styled between reads, never rebuilt per tick.
/// </para>
/// </summary>
internal sealed partial class DetailView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetContextActions, IDetailHost
{
    private const string LogScope = "details";

    private readonly DetailPart _part;
    private readonly WidgetKind _kind;

    private readonly StackPanel _heroBlock = new() { Spacing = 0 };
    private readonly TextBlock _hero = Fluent.Text(FluentType.Subtitle);
    private readonly TextBlock _caption = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
    private readonly Grid _facts = new();
    private readonly List<FactCell> _cells = [];
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();

    private readonly Brush _primaryBrush = FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White);
    private readonly Brush _secondaryBrush = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);
    private readonly Brush _disabledBrush = FluentLayout.Themed("TextFillColorDisabledBrush", Colors.Gray);

    private Ticker? _ticker;
    private WidgetConfig _config;
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval;
    private IWidgetSurface? _surface;
    private string? _title;
    private WidgetArea _area;
    private DetailSheet _sheet = DetailSheet.Waiting;
    private DetailFacePlan? _plan;
    private bool _detached;

    // The longest fact value the plan was made for: a longer one re-plans, so no value is ever cut.
    private int _valueChars;

    public DetailView(WidgetKind kind)
    {
        _kind = kind;
        _part = DetailPart.For(kind);
        _config = WidgetConfig.CreateDefault(kind);

        _hero.Text = "--";
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_hero, FontNumeralAlignment.Tabular);
        _caption.Visibility = Visibility.Collapsed;
        _heroBlock.Children.Add(_hero);
        _heroBlock.Children.Add(_caption);
        _heroBlock.Margin = WidgetLayout.SideInset;
        _facts.Margin = WidgetLayout.SideInset;

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });

        Children.Add(_heroBlock);
        Children.Add(_facts);

        BuildList();

        _empty = Fluent.EmptyState(WidgetCatalog.Definition(kind).Glyph, _emptyText, out _);
        _empty.Visibility = Visibility.Collapsed;
        SetRowSpan(_empty, 3);
        SetColumnSpan(_empty, 2);
        Children.Add(_empty);
    }

    // ---- IWidgetView ----------------------------------------------------------------------------

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;

        // [S-1881] Leases are taken here, never in the constructor, so a parked view holds nothing.
        _part.Attach();
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config.Kind == _kind ? config : WidgetConfig.CreateDefault(_kind);

        var sampleNow = _part.ApplyConfig(_config, this);
        var resubscribed = Resubscribe();

        if (sampleNow && !resubscribed)
        {
            Sample();
        }
    }

    /// <summary>True when the subscription was rebuilt, which fires a tick of its own.</summary>
    private bool Resubscribe()
    {
        var interval = (_config as DetailConfig)?.TickInterval ?? TimeSpan.FromSeconds(2);

        if (_subscription is not null && interval == _subscribedInterval)
        {
            return false;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, Sample);
        return _subscription is not null;
    }

    private void Sample()
    {
        if (_detached)
        {
            return;
        }

        try
        {
            _part.Sample(this);
        }
        catch (Exception ex)
        {
            // A part that cannot read says so on its tile rather than taking the surface down.
            Log.Error(LogScope, $"could not read {_kind}", ex);
            Present(DetailSheet.Waiting with { Note = Str.WidgetsB_Meters_Error.Text() });
        }
    }

    public void Detach()
    {
        _detached = true;
        _subscription?.Dispose();
        _subscription = null;
        _part.Detach();
        DetachList();
    }

    // ---- IDetailHost ----------------------------------------------------------------------------

    public void Present(DetailSheet sheet)
    {
        if (_detached)
        {
            return;
        }

        var reshaped = _sheet.Facts.Count != sheet.Facts.Count
            || _sheet.Rows.Count != sheet.Rows.Count
            || (_sheet.Caption is null) != (sheet.Caption is null)
            || _sheet.Absent != sheet.Absent
            || (_sheet.Hero is null) != (sheet.Hero is null)
            || LongestValue(sheet) > _valueChars;

        _sheet = sheet;

        if (reshaped || _plan is null)
        {
            Lay();
        }
        else
        {
            Fill();
        }
    }

    public void SetTitle(string? title)
    {
        if (title == _title)
        {
            return;
        }

        _title = title;
        _surface?.SetHeaderTitle(title);
    }

    public void SampleNow()
    {
        if (!_detached && _subscription is not null)
        {
            Sample();
        }
    }

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;

        if (_title is not null)
        {
            surface.SetHeaderTitle(_title);
        }
    }

    public IReadOnlyList<WidgetAction> ContextActions => _part.ContextActions(this);

    // ---- layout ---------------------------------------------------------------------------------

    /// <summary>[S-663] The card's word on how much room the widget has.</summary>
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    private static int LongestValue(DetailSheet sheet) => sheet.Facts.Count == 0 ? 0 : sheet.Facts.Max(f => f.Value.Length);

    private bool ShowsEmpty => _sheet.Absent
        || (_sheet.Hero is null && _sheet.Facts.Count == 0 && _sheet.Rows.Count == 0 && _sheet.Note is not null);

    /// <summary>[S-2402] Plan the face for the area and the sheet's shape, then fill it.</summary>
    private void Lay()
    {
        if (ShowsEmpty)
        {
            _emptyText.Text = _sheet.Note ?? string.Empty;
            _empty.Visibility = Visibility.Visible;
            _heroBlock.Visibility = Visibility.Collapsed;
            _facts.Visibility = Visibility.Collapsed;
            ShowList(0);
            _plan = null;
            return;
        }

        _empty.Visibility = Visibility.Collapsed;

        var hasHero = _part.HasHero && _sheet.Hero is not null;
        var rows = _part.MaxRows > 0 ? Math.Min(_part.MaxRows, _sheet.Rows.Count) : _sheet.Rows.Count;
        _valueChars = LongestValue(_sheet);
        var labels = _sheet.Facts.Count == 0 ? 0 : _sheet.Facts.Max(f => f.Label.Length);
        var shape = new DetailShape(hasHero, _part.HeroCharacters, _sheet.Caption is not null, _sheet.Facts.Count, Math.Max(rows, _part.HasHero ? 0 : 1), _part.ListRowHeight, _valueChars, labels);
        var plan = DetailFace.Plan(_area, shape, WidgetLayout.ContentInset);
        _plan = plan;

        var side = plan.Arrangement == DetailArrangement.Side;
        var alone = plan.Arrangement == DetailArrangement.HeroOnly;

        ColumnDefinitions[0].Width = side ? new GridLength(plan.HeroColumn) : new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        _heroBlock.Visibility = hasHero ? Visibility.Visible : Visibility.Collapsed;
        SetRow(_heroBlock, 0);
        SetRowSpan(_heroBlock, alone || side ? 3 : 1);
        _heroBlock.VerticalAlignment = alone || side ? VerticalAlignment.Center : VerticalAlignment.Top;
        _hero.Apply(plan.Hero);
        _caption.Visibility = plan.Caption ? Visibility.Visible : Visibility.Collapsed;

        _facts.Visibility = plan.Facts > 0 ? Visibility.Visible : Visibility.Collapsed;
        SetRow(_facts, side ? 0 : 1);
        SetRowSpan(_facts, side ? 3 : 1);
        SetColumn(_facts, side ? 1 : 0);
        _facts.VerticalAlignment = side ? VerticalAlignment.Center : VerticalAlignment.Top;
        _facts.Margin = side
            ? new Thickness(0, 0, WidgetLayout.ContentInset, WidgetLayout.ContentInset)
            : new Thickness(WidgetLayout.ContentInset, hasHero ? DetailFace.SectionGap : 0, WidgetLayout.ContentInset, 0);

        LayFacts(plan);
        ShowList(plan.List ? plan.ListHeight : 0);
        Fill();
    }

    /// <summary>The facts grid for a plan: its columns, its rows and each cell's style.</summary>
    private void LayFacts(DetailFacePlan plan)
    {
        var columns = Math.Max(1, plan.Columns);
        var rows = (plan.Facts + columns - 1) / columns;

        while (_facts.ColumnDefinitions.Count < columns)
        {
            _facts.ColumnDefinitions.Add(new ColumnDefinition());
        }

        while (_facts.ColumnDefinitions.Count > columns)
        {
            _facts.ColumnDefinitions.RemoveAt(_facts.ColumnDefinitions.Count - 1);
        }

        while (_facts.RowDefinitions.Count < rows)
        {
            _facts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        while (_facts.RowDefinitions.Count > rows)
        {
            _facts.RowDefinitions.RemoveAt(_facts.RowDefinitions.Count - 1);
        }

        foreach (var column in _facts.ColumnDefinitions)
        {
            column.Width = new GridLength(1, GridUnitType.Star);
        }

        _facts.ColumnSpacing = DetailFace.ColGap(plan.Style);
        _facts.RowSpacing = DetailFace.RowGap(plan.Style);

        while (_cells.Count < plan.Facts)
        {
            var cell = new FactCell();
            _cells.Add(cell);
            _facts.Children.Add(cell.Root);
        }

        for (var i = 0; i < _cells.Count; i++)
        {
            var shown = i < plan.Facts;
            _cells[i].Root.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;

            if (shown)
            {
                SetRow(_cells[i].Root, i / columns);
                SetColumn(_cells[i].Root, i % columns);
                _cells[i].Style(plan.Style);
            }
        }
    }

    /// <summary>Put the sheet's text into the elements the plan shows.</summary>
    private void Fill()
    {
        if (_plan is not { } plan)
        {
            return;
        }

        _hero.Text = _sheet.Hero ?? string.Empty;
        _hero.Foreground = _sheet.HeroAvailable ? _primaryBrush : _disabledBrush;
        ToolTipService.SetToolTip(_heroBlock, _sheet.HeroAvailable ? null : _sheet.Note);
        _caption.Text = _sheet.Caption ?? string.Empty;

        for (var i = 0; i < plan.Facts && i < _sheet.Facts.Count && i < _cells.Count; i++)
        {
            _cells[i].Show(_sheet.Facts[i], _primaryBrush, _secondaryBrush, _disabledBrush);
        }

        FillList();
    }

    /// <summary>One fact: its label and value, drawn in the plan's style.</summary>
    private sealed class FactCell
    {
        private readonly TextBlock _label = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        private readonly TextBlock _value = Fluent.Text(FluentType.Body);
        private FactStyle? _style;

        public FactCell()
        {
            Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_value, FontNumeralAlignment.Tabular);
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Root.Children.Add(_label);
            Root.Children.Add(_value);
        }

        public Grid Root { get; } = new();

        /// <summary>[S-2402] A line puts the value at the right of the label; a tile puts it under, larger.</summary>
        public void Style(FactStyle style)
        {
            if (_style == style)
            {
                return;
            }

            _style = style;
            var line = style == FactStyle.Line;

            _label.Apply(line ? FluentType.Body : FluentType.Caption);
            _value.Apply(style switch
            {
                FactStyle.LargeTile => FluentType.Title,
                FactStyle.Tile => FluentType.BodyLarge,
                _ => FluentType.Body,
            });

            SetRow(_value, line ? 0 : 1);
            SetColumn(_value, line ? 1 : 0);
            SetColumnSpan(_label, line ? 1 : 2);
            SetColumnSpan(_value, line ? 1 : 2);
            _value.HorizontalAlignment = line ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            _value.Margin = line ? new Thickness(Fluent.Space2, 0, 0, 0) : new Thickness(0);
        }

        public void Show(DetailFact fact, Brush primary, Brush secondary, Brush disabled)
        {
            _label.Text = fact.Label;
            _value.Text = fact.Value;
            _value.Foreground = fact.Available ? primary : disabled;
            _label.Foreground = secondary;
        }
    }
}
