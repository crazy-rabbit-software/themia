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
using Themia.Sdk.WinUI;
using Themia.Sdk;

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
/// <para>
/// [S-2500] The figures are the system hub's: the part watches its feeds from Attach and lets go in
/// Detach, and the view only lays out the sheets it is handed.
/// </para>
/// </summary>
public sealed partial class DetailView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetContextActions, IDetailHost
{
    private const string LogScope = "details";

    private readonly DetailPart _part;
    private readonly IWidgetHost _host;
    private readonly WidgetId _kind;

    // [S-3860] The shared hero readout: the main figure with its smaller unit, the caption its line under it.
    private readonly HeroReadout _heroBlock = new();
    private readonly TextBlock _caption;
    private readonly Grid _facts = new();
    private readonly List<StatPart> _cells = [];
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();

    private readonly Brush _primaryBrush = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White);
    private readonly Brush _disabledBrush = ThemeBrush.Of("TextFillColorDisabledBrush", Colors.Gray);
    private readonly Brush _labelBrush = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray);

    private WidgetConfig _config;
    private IWidgetSurface? _surface;
    private string? _title;
    private WidgetArea _area;
    private DetailSheet _sheet = DetailSheet.Waiting;
    private DetailFacePlan? _plan;

    // [S-1881] Until Attach: a parked view watches nothing, whatever config it is handed.
    private bool _detached = true;

    // The longest fact value the plan was made for: a longer one re-plans, so no value is ever cut.
    private int _valueChars;

    // The hero text last split, so a tick that did not change it does not touch the readout.
    private string? _heroText;

    public DetailView(IWidgetHost host, DetailPart part)
    {
        _host = host;
        ArgumentNullException.ThrowIfNull(part);
        _kind = part.Kind;
        _part = part;
        _config = WidgetConfig.CreateDefault(_kind);

        _heroBlock.SetValue("--", string.Empty, false);
        _caption = _heroBlock.AddLine(FluentType.Body);
        _caption.Visibility = Visibility.Collapsed;
        _heroBlock.Margin = new Thickness(HeroLayout.SideInset, HeroLayout.TopInset, HeroLayout.SideInset, 0);
        _facts.Margin = WidgetLayout.SideInset;

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });

        Children.Add(_heroBlock);
        Children.Add(_facts);

        BuildList();

        _empty = Fluent.EmptyState(ModuleCatalog.Of(_kind).Glyph, _emptyText, out _);
        _empty.Visibility = Visibility.Collapsed;
        SetRowSpan(_empty, 3);
        SetColumnSpan(_empty, 2);
        Children.Add(_empty);
    }

    // ---- IWidgetView ----------------------------------------------------------------------------

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        // [S-1881] The feeds are watched here, never in the constructor, so a parked view holds nothing.
        _detached = false;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config.Kind == _kind ? config : WidgetConfig.CreateDefault(_kind);

        if (_detached)
        {
            return;
        }

        if (_host.System is not { } hub)
        {
            // Only before the host has started: there is nothing to read from yet.
            Log.Warn(LogScope, $"no system hub for {_kind}");
            Present(DetailSheet.Waiting with { Note = Str.WidgetsB_Meters_Unavailable.Text() });
            return;
        }

        // [S-2506] The part presents what its feeds already hold at once; later reads call back.
        _part.Watch(hub, _config, this);
    }

    public void Detach()
    {
        _detached = true;

        // [S-2513] The feeds (and a ping window's probes) stay in the hub while the state wants them.
        _part.Detach();
        DetachList();
    }

    // ---- IDetailHost ----------------------------------------------------------------------------

    public void Present(DetailSheet sheet)
    {
        // [S-6332] A read that says what the tile shows already touches nothing.
        if (_detached || (_plan is not null && sheet.SameAs(_sheet)))
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
        if (!_detached)
        {
            _part.Refresh();
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

    /// <summary>
    /// A sheet with nothing but a note (the processes cannot be read) is the empty state; a thing not on
    /// this machine is not: its sheet is its own facts, dimmed ([S-1942]).
    /// </summary>
    private bool ShowsEmpty => _sheet.Hero is null && _sheet.Facts.Count == 0 && _sheet.Rows.Count == 0 && _sheet.Note is not null;

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
        var shape = new DetailShape(hasHero, _part.HeroCharacters, _sheet.Caption is not null, _sheet.Facts.Count, Math.Max(rows, _part.HasHero ? 0 : 1), _part.ListRowHeight, _valueChars, labels,
            WidgetLayout.ListInset.Top + WidgetLayout.ListInset.Bottom, _part.RowsGrow);
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

        // [S-3861] Stacked, the block stands where the plan balanced it: half the spare room above.
        _heroBlock.Margin = alone || side
            ? new Thickness(HeroLayout.SideInset, 0, HeroLayout.SideInset, 0)
            : new Thickness(HeroLayout.SideInset, HeroLayout.TopInset + plan.Top, HeroLayout.SideInset, 0);
        _caption.Visibility = plan.Caption ? Visibility.Visible : Visibility.Collapsed;
        _heroBlock.Lay(plan.Hero, icon: false, trailingBeside: true, linesBeside: false);

        _facts.Visibility = plan.Facts > 0 ? Visibility.Visible : Visibility.Collapsed;
        SetRow(_facts, side ? 0 : 1);
        SetRowSpan(_facts, side ? 3 : 1);
        SetColumn(_facts, side ? 1 : 0);
        _facts.VerticalAlignment = side ? VerticalAlignment.Center : VerticalAlignment.Top;
        _facts.Margin = side
            ? new Thickness(0, 0, WidgetLayout.ContentInset, WidgetLayout.ContentInset)
            : new Thickness(WidgetLayout.ContentInset, hasHero ? DetailFace.SectionGap : HeroLayout.TopInset + plan.Top, WidgetLayout.ContentInset, 0);

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
            var cell = new StatPart(plan.Style);
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
                _cells[i].Restyle(plan.Style);
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

        if (_sheet.Hero != _heroText)
        {
            _heroText = _sheet.Hero;
            var hero = HeroLayout.Split(_sheet.Hero);
            _heroBlock.SetValue(hero.Value, hero.Unit, hero.Spaced);
        }

        _heroBlock.ValueLine.Foreground = _sheet.HeroAvailable ? _primaryBrush : _disabledBrush;
        ToolTipService.SetToolTip(_heroBlock, _sheet.HeroAvailable || _sheet.Absent ? null : _sheet.Note);
        _caption.Text = _sheet.Caption ?? string.Empty;

        // [S-1940] Not on this machine: the whole tile says why, every part of it dimmed.
        UnavailableLook.Say(this, _sheet.Absent ? _sheet.Note : null);

        for (var i = 0; i < plan.Facts && i < _sheet.Facts.Count && i < _cells.Count; i++)
        {
            var fact = _sheet.Facts[i];
            _cells[i].Show(fact.Label, fact.Value);
            _cells[i].Figure.Foreground = fact.Available ? _primaryBrush : _disabledBrush;
            _cells[i].Label.Foreground = _sheet.Absent ? UnavailableLook.Text : _labelBrush;
        }

        FillList();
    }
}
