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
/// [S-2400] [S-11007] [S-11008] The face every system facts and list atom shares, built from the
/// family's blocks: fact rows (the value primary, the name secondary, [S-6711]) as the config picks
/// them, or a list of the config's rows (processes, adapters). Where the figures come from is the
/// kind's <see cref="DetailPart"/>; nothing appears with size.
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

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Children.Add(_facts);

        BuildList();

        _empty.Visibility = Visibility.Collapsed;
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

    /// <summary>[S-11007] The fact rows the tile shows: a facts atom's pick, else the sheet's own.</summary>
    private IReadOnlyList<DetailFact> FactsOf(DetailSheet sheet)
        => _part.Atom == DetailAtom.Facts && _config is FactsConfig facts
            ? FactsRules.Pick(Strings.Current, sheet, facts.Facts, _part.FactOptions)
            : sheet.Facts;

    /// <summary>[S-2402] The face <paramref name="sheet"/> gets in this tile.</summary>
    private DetailFacePlan PlanFor(DetailSheet sheet)
    {
        // [S-11007] [S-11008] An atom shows what its config says at every size: its picked facts, or
        // its list's rows (more scroll inside them, [S-11008]), from the view's top.
        switch (_part.Atom)
        {
            case DetailAtom.Facts:
                return new DetailFacePlan(FactsOf(sheet).Count, 0, 1);

            default:
                var rows = _config is Themia.Sdk.Kit.IKitRows list ? Themia.Sdk.Kit.KitSizing.ClampRows(list.Rows) : 1;
                return new DetailFacePlan(0, sheet.Rows.Count == 0 ? 0 : rows, Math.Clamp(_part.ListLines, 1, 3));
        }
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
            _facts.Visibility = Visibility.Collapsed;
            ShowList(null);
            _plan = null;
            return;
        }

        _empty.Visibility = Visibility.Collapsed;

        var plan = PlanFor(_sheet);
        _plan = plan;

        // The rows from the view's top. They carry their own side inset.
        _facts.Visibility = plan.Facts > 0 ? Visibility.Visible : Visibility.Collapsed;
        SetRow(_facts, 1);
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

        // [S-1940] Not on this machine: the whole tile says why, every part of it disabled.
        UnavailableLook.Say(this, _sheet.Absent ? _sheet.Note : null);

        var facts = FactsOf(_sheet);
        for (var i = 0; i < plan.Facts && i < facts.Count && i < _factRows.Count; i++)
        {
            var fact = facts[i];
            _factRows[i].Show(fact.Label, fact.Value);
            _factRows[i].Available = fact.Available && !_sheet.Absent;
        }

        FillList();
    }
}
