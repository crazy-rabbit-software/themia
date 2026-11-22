using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.App.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7060] [S-588] Every kit widget's empty and problem states (and the calendar's, the contacts',
/// the tasks', the week's and the next meeting's): the Status block's one row at the top of the
/// content, where its first row would be ([S-6714], DECISIONS 4) — the widget's glyph (the caution
/// warning for a problem), one sentence (several sources' problems in one, "World News and 2 more
/// could not be loaded", every line in its tooltip) and its one way out: settings › Accounts when only the
/// user can mend it there ("Account gone", "sign in again", "access not granted"), else the page that
/// widens what the account may read ([S-9919]), else the widget's own (Try again, start a list on
/// this PC). Loading, offline and "nothing on" need no action and show none. No account at all is the
/// card's Set up row instead ([S-1862]).
/// <para>
/// [S-12835] The row is the compact line over the list's face, never the face's place (WR-10): under it the list's
/// rows stand empty (<see cref="EmptyRows"/>, at the list's pitch, <see cref="RowPitch"/>), as many as its slot holds
/// whole, so the widget keeps its rows with nothing in them. A view that draws its own face under the row (the
/// calendar's month) gives the panel no room under it and no row is drawn.
/// </para>
/// </summary>
public sealed partial class KitStatePanel : Grid
{
    private const string Scope = "kit";

    /// <summary>Under this width the Accounts button says only "Accounts".</summary>
    private const double ShortLabelBelow = 200;

    /// <summary>[S-14260] The state's row: the list's first row's place, one of its items.</summary>
    private readonly StatusBlock _row = StatusBlock.Line();
    private readonly IWidgetHost _widgetHost;
    private readonly string _glyph;
    private Uri? _grantPage;
    private Action? _run;
    private (string Message, bool Accounts, Uri? Grant, (string Label, Action Run)? Action, bool Problem, IReadOnlyList<string> Details)? _shown;
    private bool _short;

    public KitStatePanel(string glyph, IWidgetHost host)
    {
        ArgumentNullException.ThrowIfNull(glyph);
        ArgumentNullException.ThrowIfNull(host);

        _widgetHost = host;
        _glyph = glyph;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        SetRow(_empty, 1);
        Children.Add(_row);
        Children.Add(_empty);
        SizeChanged += OnSizeChanged;
    }

    private readonly EmptyRows _empty = new();

    /// <summary>The state's row.</summary>
    public StatusBlock Row => _row;

    /// <summary>
    /// [S-12835] The pitch of the list's rows the face keeps under the row (unset: a one-line row's whole cells). [S-14810]
    /// The state's row stands in the first row's place at that one size, the rows under it at it too.
    /// </summary>
    public double RowPitch
    {
        get => _empty.Pitch;
        set
        {
            _empty.Pitch = value;
            ApplyRowHeight();
        }
    }

    /// <summary>
    /// [S-14811] The state's row's own height where it stands in another row's place than the list's first (a check list's
    /// add row: a row of one line, the card being its rows and that row); unset (NaN): the list's pitch.
    /// </summary>
    public double LineHeight
    {
        get => _lineHeight;
        set
        {
            _lineHeight = value;
            ApplyRowHeight();
        }
    }

    private double _lineHeight = double.NaN;

    /// <summary>The panel's height as laid: the room the state's row and the empty rows under it share.</summary>
    private double _height;

    private void ApplyRowHeight()
    {
        if (double.IsFinite(_lineHeight) && _lineHeight > 0)
        {
            _row.Height = _lineHeight;
            Fit();
            return;
        }

        // [S-14810] The state's row is the first of the list's rows at their one size (unset: a one-line row's optimal).
        var pitch = _empty.Pitch;
        _row.Height = double.IsFinite(pitch) && pitch > 0 ? pitch : LineRules.EdgeRow(ItemGrids.Of(this));
        Fit();
    }

    /// <summary>
    /// Show a sentence, as a problem (the warning) or a plain state (the widget's glyph), with its one
    /// action: the Accounts button when <paramref name="offerAccounts"/>, else the grant page's when
    /// there is one, else the widget's own (<paramref name="action"/>: its words and what it does).
    /// <paramref name="details"/> are the lines behind the sentence, for its tooltip.
    /// </summary>
    public void Show(string message, bool offerAccounts, Uri? grant = null, (string Label, Action Run)? action = null, bool problem = false, IReadOnlyList<string>? details = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        _shown = (message, offerAccounts, grant, action, problem, details ?? []);
        _grantPage = grant;
        _empty.Margin = default;
        Draw();
        Visibility = Visibility.Visible;
    }

    /// <summary>
    /// [S-12835] Nothing for the panel to say (the card's compact Set up line says it over the face): the list's rows
    /// alone, empty, from the card's top edge as the list's own would stand.
    /// </summary>
    public void ShowFace()
    {
        _shown = null;
        _run = null;
        _row.Hide();
        _empty.Margin = ListEdge.Margin;
        Visibility = Visibility.Visible;
    }

    /// <summary>[S-7040] A kit screen's state: its sentence, as a problem where it is one, with its way out.</summary>
    public void Show(KitScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        var problem = screen.Kind is KitScreenKind.Problem or KitScreenKind.AccountsGone or KitScreenKind.SignInRequired or KitScreenKind.Forbidden;
        Show(screen.Sentence, screen.OffersAccounts, screen.Grant, screen.Fix is { } fix ? (fix.Label, fix.Run) : null, problem, screen.Details);
    }

    public void Hide() => Visibility = Visibility.Collapsed;

    public void Detach()
    {
        SizeChanged -= OnSizeChanged;
        _run = null;
    }

    /// <summary>Open settings › Accounts, the one place accounts are added and mended.</summary>
    public void OpenAccounts()
    {
        if (!_widgetHost.ShowAccounts())
        {
            Log.Info(Scope, "nothing here opens settings › Accounts");
        }
    }

    private void Draw()
    {
        if (_shown is not { } shown)
        {
            return;
        }

        string? label = null;
        if (shown.Accounts)
        {
            label = (_short ? Str.WidgetUi_AccountsLabel : Str.WidgetsA_Office_OpenAccounts).Text();
            _run = OpenAccounts;
        }
        else if (shown.Grant is not null)
        {
            label = Str.Kit_GrantPage.Text();
            _run = OpenGrant;
        }
        else if (shown.Action is { } own)
        {
            label = own.Label;
            _run = own.Run;
        }
        else
        {
            _run = null;
        }

        _row.Show(shown.Problem ? StatusKind.Problem : StatusKind.Empty, _glyph, shown.Message, label, label is null ? null : () => _run?.Invoke());

        if (shown.Details.Count > 1)
        {
            ToolTipService.SetToolTip(_row.Sentence, string.Join("\n", shown.Details));
        }

        Fit();
    }

    /// <summary>
    /// [S-15784] A card too short for the state's line and any of the face under it (its least: one row, the line's whole
    /// height) keeps the face, never the line alone (WR-10: "it never swaps its face for a status line alone"): the rows
    /// stand from the card's top edge with nothing in them, the sentence on the panel's tooltip. Whether a card that
    /// cannot hold both should rather say why is the owner's to decide; until then the face stays.
    /// </summary>
    private void Fit()
    {
        if (_shown is not { } shown)
        {
            return;
        }

        // The line keeps its place in the layout (shown or not, the panel asks for the same height, so a panel its content
        // sizes never swings between the two); the face's rows stand over it from the card's top edge.
        var squeezed = _height > 0 && KitStateFit.Squeezed(_height, _row.Height, FamilySpace.Inset);
        _row.Opacity = squeezed ? 0 : 1;
        _row.IsHitTestVisible = !squeezed;
        SetRow(_empty, squeezed ? 0 : 1);
        SetRowSpan(_empty, squeezed ? 2 : 1);
        _empty.Margin = squeezed ? ListEdge.Margin : default;
        ToolTipService.SetToolTip(this, squeezed ? shown.Message : null);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (!args.NewSize.Height.Equals(_height))
        {
            _height = args.NewSize.Height;
            ApplyRowHeight();
            Fit();
        }

        // A narrow tile gets the short form, rather than a button that leaves no room for the sentence.
        var narrow = args.NewSize.Width is > 0 and < ShortLabelBelow;
        if (narrow != _short)
        {
            _short = narrow;
            if (_shown is { Accounts: true })
            {
                Draw();
            }
        }
    }

    /// <summary>[S-9919] The grant page, through the host's one launcher; a preview opens nothing.</summary>
    private void OpenGrant()
    {
        if (_grantPage is { } page && !_widgetHost.IsPreview)
        {
            _widgetHost.Launcher.Open(page.AbsoluteUri);
        }
    }
}
