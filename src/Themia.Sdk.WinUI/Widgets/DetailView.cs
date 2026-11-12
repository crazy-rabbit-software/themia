using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets.Details;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-2400] The face every system detail widget shares, built from the family's blocks: a Readout
/// (the main figure and the line under it), fact rows (the value primary, the name secondary,
/// [S-6711]) and, for some kinds, a list of rows (processes, adapters). Where the figures come from
/// is the kind's <see cref="DetailPart"/>; where each goes at each size is <see cref="DetailFace"/>'s.
/// Elements are kept and re-filled between reads, never rebuilt per tick ([S-6332]).
/// <para>
/// [S-2500] The figures are the system hub's: the part watches its feeds from Attach and lets go in
/// Detach, and the view only lays out the sheets it is handed.
/// </para>
/// </summary>
public sealed partial class DetailView : Grid, IWidgetView, IWidgetOpens, IWidgetSizeAware, IWidgetHeaderContent, IWidgetContextActions, IDetailHost
{
    private const string LogScope = "details";

    private readonly DetailPart _part;
    private readonly IWidgetHost _host;
    private readonly WidgetId _kind;

    private readonly ReadoutBlock _hero = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly StackPanel _facts = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly List<RowBlock> _factRows = [];
    private readonly StatusBlock _empty = new() { VerticalAlignment = VerticalAlignment.Top };

    private WidgetConfig _config;
    private WidgetArea _area;
    private DetailSheet _sheet = DetailSheet.Waiting;
    private DetailFacePlan? _plan;

    // [S-1881] Until Attach: a parked view watches nothing, whatever config it is handed.
    private bool _detached = true;

    public DetailView(IWidgetHost host, DetailPart part)
    {
        _host = host;
        ArgumentNullException.ThrowIfNull(part);
        _kind = part.Kind;
        _part = part;
        _config = WidgetConfig.CreateDefault(_kind);

        _hero.Show("--", valueRole: ColourRole.TextDisabled);

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });

        Children.Add(_hero);
        Children.Add(_facts);

        BuildList();

        _empty.Visibility = Visibility.Collapsed;
        SetColumnSpan(_empty, 2);
        SetRowSpan(_empty, 3);
        Children.Add(_empty);
    }

    // ---- IWidgetView ----------------------------------------------------------------------------

    /// <inheritdoc />
    public CardOpen? Opens => _part.Opens(_config);

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

        // [S-6333] A new number of rows lays the tile out only when the plan for it differs.
        var reshaped = _plan is not { } plan || DetailFace.Relays(_sheet, sheet, plan, PlanFor);

        _sheet = sheet;

        if (reshaped)
        {
            Lay();
        }
        else
        {
            Fill();
        }
    }

    /// <summary>[S-2260] Which one the kind reads (a processor, an adapter, a host) as the header's label; the name stays the widget's.</summary>
    public void SetAbout(string? about) => _about.SetAbout(about);

    /// <summary>[S-2260] The header's trailing parts: the label of <see cref="SetAbout"/>.</summary>
    public FrameworkElement? HeaderTrailing => _about;

    private readonly HeaderParts _about = new();

    public void SampleNow()
    {
        if (!_detached)
        {
            _part.Refresh();
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

    /// <summary>
    /// [S-6714] A sheet with nothing but a note (the processes cannot be read) is the empty state, one
    /// Status row at the top; a thing not on this machine is not: its sheet is its own facts, disabled ([S-1942]).
    /// </summary>
    private bool ShowsEmpty => _sheet.Hero is null && _sheet.Facts.Count == 0 && _sheet.Rows.Count == 0 && _sheet.Note is not null;

    /// <summary>The figure's widest case, with the line under it when the sheet has one.</summary>
    private ReadoutText? HeroOf(DetailSheet sheet)
        => _part.HasHero && sheet.Hero is not null ? ReadoutRules.Split(_part.HeroWorst) with { LineUnder = sheet.Caption is not null } : null;

    /// <summary>[S-2402] The face <paramref name="sheet"/> gets in this tile.</summary>
    private DetailFacePlan PlanFor(DetailSheet sheet)
    {
        var rows = _part.MaxRows > 0 ? Math.Min(_part.MaxRows, sheet.Rows.Count) : sheet.Rows.Count;
        return DetailFace.Plan(_area, new DetailShape(HeroOf(sheet), sheet.Facts.Count, rows, DetailFace.ListLines(sheet.Rows)));
    }

    /// <summary>[S-2402] Plan the face for the area and the sheet's shape, then fill it.</summary>
    private void Lay()
    {
        if (!_area.IsMeasured)
        {
            return;
        }

        if (ShowsEmpty)
        {
            _empty.Show(StatusKind.Empty, ModuleCatalog.Of(_kind).Glyph, _sheet.Note ?? string.Empty);
            _empty.Visibility = Visibility.Visible;
            _hero.Visibility = Visibility.Collapsed;
            _facts.Visibility = Visibility.Collapsed;
            ShowList(null);
            _plan = null;
            return;
        }

        _empty.Visibility = Visibility.Collapsed;

        var plan = PlanFor(_sheet);
        _plan = plan;
        var hero = HeroOf(_sheet);

        ColumnDefinitions[0].Width = plan.Side ? new GridLength(plan.HeroColumn) : new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = plan.Side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        // The figure at the view's top, in the inset (on the side its column holds the inset on its left only).
        _hero.Visibility = hero is null ? Visibility.Collapsed : Visibility.Visible;
        SetRowSpan(_hero, plan.Side ? 3 : 1);
        _hero.Margin = plan.Side ? new Thickness(FamilySpace.Inset, 0, 0, 0) : Family.SideInset;
        if (hero is { } worst)
        {
            var (width, height) = MeterPlan.Content(_area);
            _hero.Size(_area, plan.Side ? plan.HeroColumn - FamilySpace.Inset : width, height, worst);
        }

        // The rows: under the figure, 8 down, or beside it from the top. They carry their own side inset.
        _facts.Visibility = plan.Facts > 0 ? Visibility.Visible : Visibility.Collapsed;
        SetRow(_facts, plan.Side ? 0 : 1);
        SetColumn(_facts, plan.Side ? 1 : 0);
        _facts.Margin = new Thickness(0, !plan.Side && hero is not null ? FamilySpace.S2 : 0, 0, 0);
        LayFacts(plan.Facts);

        ShowList(plan);
        Fill();
    }

    /// <summary>Keep a fact row for each fact the plan shows; the rest collapsed.</summary>
    private void LayFacts(int count)
    {
        while (_factRows.Count < count)
        {
            var row = new RowBlock(RowKind.Fact, lead: 0);
            _factRows.Add(row);
            _facts.Children.Add(row);
        }

        for (var i = 0; i < _factRows.Count; i++)
        {
            _factRows[i].Visibility = i < count ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>Put the sheet's text into the elements the plan shows.</summary>
    private void Fill()
    {
        if (_plan is not { } plan)
        {
            return;
        }

        var hero = ReadoutRules.Split(_sheet.Hero);
        _hero.Show(hero.Value, hero.Unit, _sheet.Caption, valueRole: _sheet.HeroAvailable && !_sheet.Absent ? ColourRole.TextPrimary : ColourRole.TextDisabled);
        var tip = _sheet.HeroAvailable || _sheet.Absent ? null : _sheet.Note;
        if (!Equals(ToolTipService.GetToolTip(_hero), tip))
        {
            ToolTipService.SetToolTip(_hero, tip);
        }

        // [S-1940] Not on this machine: the whole tile says why, every part of it disabled.
        UnavailableLook.Say(this, _sheet.Absent ? _sheet.Note : null);

        for (var i = 0; i < plan.Facts && i < _sheet.Facts.Count && i < _factRows.Count; i++)
        {
            var fact = _sheet.Facts[i];
            _factRows[i].Show(fact.Label, fact.Value);
            _factRows[i].Available = fact.Available && !_sheet.Absent;
        }

        FillList();
    }
}
