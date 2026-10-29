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
/// could not be loaded", every line in its tooltip) and its one way out: ⚙ › Accounts when only the
/// user can mend it there ("Account gone", "sign in again", "access not granted"), else the page that
/// widens what the account may read ([S-9919]), else the widget's own (Try again, start a list on
/// this PC). Loading, offline and "nothing on" need no action and show none. No account at all is the
/// card's Set up row instead ([S-1862]).
/// </summary>
public sealed partial class KitStatePanel : Grid
{
    private const string Scope = "kit";

    /// <summary>Under this width the Accounts button says only "Accounts".</summary>
    private const double ShortLabelBelow = 200;

    private readonly StatusBlock _row = new();
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
        Children.Add(_row);
        SizeChanged += OnSizeChanged;
    }

    /// <summary>The state's row.</summary>
    public StatusBlock Row => _row;

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
        Draw();
        Visibility = Visibility.Visible;
    }

    /// <summary>[S-7040] A kit screen's state: its sentence, as a problem where it is one, with its way out.</summary>
    public void Show(KitScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        var problem = screen.Kind is KitScreenKind.Problem or KitScreenKind.AccountsGone or KitScreenKind.SignInRequired or KitScreenKind.Forbidden;
        Show(screen.Sentence, screen.OffersAccounts, screen.Grant, problem: problem, details: screen.Details);
    }

    public void Hide() => Visibility = Visibility.Collapsed;

    public void Detach()
    {
        SizeChanged -= OnSizeChanged;
        _run = null;
    }

    /// <summary>Open ⚙ › Accounts, the one place accounts are added and mended.</summary>
    public void OpenAccounts()
    {
        if (!_widgetHost.ShowAccounts())
        {
            Log.Info(Scope, "nothing here opens ⚙ › Accounts");
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
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
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
